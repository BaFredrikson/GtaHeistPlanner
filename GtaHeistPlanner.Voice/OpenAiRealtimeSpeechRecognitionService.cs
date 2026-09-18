using System.Threading.Channels;
using System.Text;
using System.Diagnostics;

namespace GtaHeistPlanner.Voice;

public sealed class OpenAiRealtimeSpeechRecognitionService : ISpeechTranscriptionProvider
{
    internal const int SpeechActivityThreshold = 3;
    internal const double CommitSilenceMilliseconds = 700;
    private readonly Func<IRealtimeTranscriptionTransport> _transportFactory;
    private readonly Func<string?> _apiKeyProvider;
    private IRealtimeTranscriptionTransport? _transport;
    private CancellationTokenSource? _sessionCancellation;
    private Channel<AudioFrame>? _audio;
    private Task? _audioPump;
    private Pcm16MonoResampler? _resampler;
    private long _generation;
    private readonly StringBuilder _partialTranscript = new();
    private readonly SemaphoreSlim _stopGate = new(1, 1);
    private readonly Stopwatch _sendClock = new();
    private long _audioBytesSent;
    private long _audioChunksSent;
    private long _lastSendTimestamp;

    public event EventHandler<SpeechRecognizedEventArgs>? SpeechRecognized;
    public event EventHandler<PartialTranscriptEventArgs>? PartialTranscriptChanged;
    public event EventHandler<SpeechRecognitionFailedEventArgs>? RecognitionFailed;
    public string StateDescription { get; private set; } = "Stopped";
    public VoiceDiagnosticTrace Diagnostics { get; }
    public GtaHeistPlanner.Core.Settings.TranscriptionProviderKind Kind => GtaHeistPlanner.Core.Settings.TranscriptionProviderKind.OpenAi;
    public TranscriptionProviderCapabilities Capabilities { get; } = new(true, true, false);

    public OpenAiRealtimeSpeechRecognitionService(
        Func<IRealtimeTranscriptionTransport>? transportFactory = null,
        Func<string?>? apiKeyProvider = null,
        VoiceDiagnosticTrace? diagnostics = null)
    {
        _transportFactory = transportFactory ?? (() => new OpenAiRealtimeWebSocketTransport());
        _apiKeyProvider = apiKeyProvider ?? (() => Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        Diagnostics = diagnostics ?? new VoiceDiagnosticTrace();
    }

    public async Task StartAsync(PcmAudioFormat format, IReadOnlyCollection<string> keywords, CancellationToken cancellationToken = default)
    {
        Diagnostics.RecordMilestone("OpenAI StartAsync entered");
        if (_transport is not null) return;
        var apiKey = _apiKeyProvider();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OpenAI API key is not configured.");
        if (format.BitsPerSample != 16 || format.Channels != 1)
            throw new NotSupportedException($"OpenAI transcription requires mono PCM16 input; capture supplied {format.BitsPerSample}-bit, {format.Channels} channel(s).");

        var generation = Interlocked.Increment(ref _generation);
        var transport = _transportFactory();
        _transport = transport;
        _resampler = new Pcm16MonoResampler(format.SampleRate);
        // Keep only a short realtime window (about 320 ms with the current 40 ms WASAPI buffer).
        _audio = Channel.CreateBounded<AudioFrame>(new BoundedChannelOptions(8) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
        _sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        transport.PartialTranscript += OnPartial;
        transport.FinalTranscript += OnFinal;
        transport.Failed += OnFailed;
        transport.StateChanged += OnStateChanged;
        try
        {
            Diagnostics.Record($"Connecting to OpenAI realtime transcription. Capture PCM: {format.SampleRate} Hz, {format.BitsPerSample}-bit, mono.");
            await transport.ConnectAsync(apiKey, new RealtimeTranscriptionOptions(keywords), _sessionCancellation.Token)
                .WaitAsync(TimeSpan.FromSeconds(15), _sessionCancellation.Token).ConfigureAwait(false);
            Diagnostics.Record("OpenAI connected. Transmitted PCM: 24000 Hz, 16-bit, mono.");
            Diagnostics.RecordMilestone("OpenAI StartAsync completed");
            StateDescription = "Listening (OpenAI gpt-live-transcribe)";
            _sendClock.Restart();
            _audioBytesSent = 0;
            _audioChunksSent = 0;
            _lastSendTimestamp = 0;
            _audioPump = PumpAudioAsync(generation, _sessionCancellation.Token);
        }
        catch (Exception exception)
        {
            Diagnostics.RecordException("OpenAI recognition startup", exception);
            await StopCoreAsync();
            throw;
        }
    }

    public void PushAudio(ReadOnlyMemory<byte> pcmAudio, int activityLevel)
    {
        if (_audio is not null && !pcmAudio.IsEmpty)
            _audio.Writer.TryWrite(new AudioFrame(pcmAudio.ToArray(), activityLevel));
    }

    private async Task PumpAudioAsync(long generation, CancellationToken cancellationToken)
    {
        Diagnostics.RecordMilestone("Audio send loop started");
        try
        {
            var speechActive = false;
            var silenceMilliseconds = 0d;
            await foreach (var frame in _audio!.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (generation != Volatile.Read(ref _generation)) return;
                var converted = _resampler!.Convert(frame.Data);
                if (converted.Length > 0)
                {
                    var frameDurationMs = converted.Length / 48d;
                    if (frame.ActivityLevel >= SpeechActivityThreshold)
                    {
                        speechActive = true;
                        silenceMilliseconds = 0;
                    }
                    else if (speechActive)
                    {
                        silenceMilliseconds += frameDurationMs;
                    }

                    if (!speechActive)
                        continue;

                    await _transport!.SendAudioAsync(converted, cancellationToken).ConfigureAwait(false);
                    var chunk = Interlocked.Increment(ref _audioChunksSent);
                    var totalBytes = Interlocked.Add(ref _audioBytesSent, converted.Length);
                    var now = _sendClock.ElapsedTicks;
                    var cadenceMs = _lastSendTimestamp == 0 ? 0 : (now - _lastSendTimestamp) * 1000d / Stopwatch.Frequency;
                    _lastSendTimestamp = now;
                    if (chunk == 1 || chunk % 25 == 0)
                    {
                        var chunkDurationMs = frameDurationMs;
                        var cumulativeMs = totalBytes / 48d;
                        Diagnostics.RecordMilestone($"Audio append sent: 24000 Hz PCM16 mono; bytes={converted.Length}; chunk={chunkDurationMs:F1} ms; cadence={cadenceMs:F1} ms; cumulative={cumulativeMs:F1} ms");
                    }

                    if (silenceMilliseconds >= CommitSilenceMilliseconds)
                    {
                        await _transport.CommitAudioAsync(cancellationToken).ConfigureAwait(false);
                        Diagnostics.RecordMilestone($"Audio buffer committed after {silenceMilliseconds:F0} ms local silence");
                        speechActive = false;
                        silenceMilliseconds = 0;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { OnFailed(this, exception); }
        finally { Diagnostics.RecordMilestone("Audio send loop exited"); }
    }

    private void OnPartial(object? sender, string text)
    {
        Diagnostics.RecordMilestone("Transcript delta callback");
        _partialTranscript.Append(text);
        var current = _partialTranscript.ToString();
        Diagnostics.Record($"Partial transcript: {current}");
        PartialTranscriptChanged?.Invoke(this, new(current));
    }

    private void OnFinal(object? sender, string text)
    {
        Diagnostics.RecordMilestone("Transcript final callback");
        _partialTranscript.Clear();
        Diagnostics.Record($"Final transcript: {text}");
        SpeechRecognized?.Invoke(this, new(text, float.NaN));
    }

    private void OnStateChanged(object? sender, string state)
    {
        StateDescription = state;
        Diagnostics.RecordMilestone(state);
    }

    private void OnFailed(object? sender, Exception exception)
    {
        StateDescription = "Recognition failed";
        Diagnostics.RecordException("OpenAI realtime recognition", exception);
        RecognitionFailed?.Invoke(this, new(exception));
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => StopCoreAsync(cancellationToken);

    public async Task<string> TestAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = _apiKeyProvider();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OpenAI API key is not configured.");
        await using var transport = _transportFactory();
        await transport.ConnectAsync(apiKey, new RealtimeTranscriptionOptions([]), cancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
        await transport.CloseAsync(cancellationToken).ConfigureAwait(false);
        return "OpenAI connection successful.";
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken = default)
    {
        await _stopGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Diagnostics.RecordMilestone("Recognition cancellation/disposal entered");
            Interlocked.Increment(ref _generation);
            var transport = _transport;
            _transport = null;
            _audio?.Writer.TryComplete();
            _sessionCancellation?.Cancel();
            if (_audioPump is not null)
                try { await _audioPump.ConfigureAwait(false); } catch (OperationCanceledException) { }
            if (transport is not null)
            {
                transport.PartialTranscript -= OnPartial;
                transport.FinalTranscript -= OnFinal;
                transport.Failed -= OnFailed;
                transport.StateChanged -= OnStateChanged;
                try { await transport.CloseAsync(cancellationToken).ConfigureAwait(false); }
                catch (Exception exception) { Diagnostics.RecordException("WebSocket close", exception); }
                try { await transport.DisposeAsync().ConfigureAwait(false); }
                catch (Exception exception) { Diagnostics.RecordException("WebSocket disposal", exception); }
            }
            _sessionCancellation?.Dispose();
            _sessionCancellation = null;
            _audioPump = null;
            _audio = null;
            _resampler = null;
            _partialTranscript.Clear();
            StateDescription = "Stopped";
            Diagnostics.RecordMilestone("Recognition cancellation/disposal completed");
        }
        finally { _stopGate.Release(); }
    }

    public void Dispose()
    {
        _sessionCancellation?.Cancel();
        var cleanup = StopAsync();
        if (!cleanup.IsCompletedSuccessfully)
            _ = cleanup.ContinueWith(task => Diagnostics.RecordException("Asynchronous recognition disposal", task.Exception!),
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    private sealed record AudioFrame(byte[] Data, int ActivityLevel);
}
