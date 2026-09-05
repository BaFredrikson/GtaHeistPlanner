using System.Threading.Channels;
using System.Text;

namespace GtaHeistPlanner.Voice;

public sealed class OpenAiRealtimeSpeechRecognitionService : ISpeechRecognitionService
{
    private readonly Func<IRealtimeTranscriptionTransport> _transportFactory;
    private readonly Func<string?> _apiKeyProvider;
    private IRealtimeTranscriptionTransport? _transport;
    private CancellationTokenSource? _sessionCancellation;
    private Channel<byte[]>? _audio;
    private Task? _audioPump;
    private Pcm16MonoResampler? _resampler;
    private long _generation;
    private readonly StringBuilder _partialTranscript = new();

    public event EventHandler<SpeechRecognizedEventArgs>? SpeechRecognized;
    public event EventHandler<PartialTranscriptEventArgs>? PartialTranscriptChanged;
    public event EventHandler<SpeechRecognitionFailedEventArgs>? RecognitionFailed;
    public string StateDescription { get; private set; } = "Stopped";
    public VoiceDiagnosticTrace Diagnostics { get; }

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
        _audio = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
        _sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        transport.PartialTranscript += OnPartial;
        transport.FinalTranscript += OnFinal;
        transport.Failed += OnFailed;
        transport.StateChanged += OnStateChanged;
        try
        {
            Diagnostics.Record($"Connecting to OpenAI realtime transcription. Capture PCM: {format.SampleRate} Hz, {format.BitsPerSample}-bit, mono.");
            await transport.ConnectAsync(apiKey, new RealtimeTranscriptionOptions(keywords), _sessionCancellation.Token);
            Diagnostics.Record("OpenAI connected. Transmitted PCM: 24000 Hz, 16-bit, mono.");
            StateDescription = "Listening (OpenAI gpt-live-transcribe)";
            _audioPump = PumpAudioAsync(generation, _sessionCancellation.Token);
        }
        catch (Exception exception)
        {
            Diagnostics.RecordException("OpenAI recognition startup", exception);
            await StopCoreAsync();
            throw;
        }
    }

    public void PushAudio(ReadOnlyMemory<byte> pcmAudio)
    {
        if (_audio is not null && !pcmAudio.IsEmpty)
            _audio.Writer.TryWrite(pcmAudio.ToArray());
    }

    private async Task PumpAudioAsync(long generation, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in _audio!.Reader.ReadAllAsync(cancellationToken))
            {
                if (generation != Volatile.Read(ref _generation)) return;
                var converted = _resampler!.Convert(frame);
                if (converted.Length > 0) await _transport!.SendAudioAsync(converted, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { OnFailed(this, exception); }
    }

    private void OnPartial(object? sender, string text)
    {
        _partialTranscript.Append(text);
        var current = _partialTranscript.ToString();
        Diagnostics.Record($"Partial transcript: {current}");
        PartialTranscriptChanged?.Invoke(this, new(current));
    }

    private void OnFinal(object? sender, string text)
    {
        _partialTranscript.Clear();
        Diagnostics.Record($"Final transcript: {text}");
        SpeechRecognized?.Invoke(this, new(text, float.NaN));
    }

    private void OnStateChanged(object? sender, string state)
    {
        StateDescription = state;
        Diagnostics.Record(state);
    }

    private void OnFailed(object? sender, Exception exception)
    {
        StateDescription = "Recognition failed";
        Diagnostics.RecordException("OpenAI realtime recognition", exception);
        RecognitionFailed?.Invoke(this, new(exception));
    }

    public void Stop() => StopCoreAsync().GetAwaiter().GetResult();

    private async Task StopCoreAsync()
    {
        Interlocked.Increment(ref _generation);
        var transport = _transport;
        _transport = null;
        _audio?.Writer.TryComplete();
        _sessionCancellation?.Cancel();
        if (_audioPump is not null)
            try { await _audioPump; } catch (OperationCanceledException) { }
        if (transport is not null)
        {
            transport.PartialTranscript -= OnPartial;
            transport.FinalTranscript -= OnFinal;
            transport.Failed -= OnFailed;
            transport.StateChanged -= OnStateChanged;
            try { await transport.CloseAsync(CancellationToken.None); } finally { await transport.DisposeAsync(); }
        }
        _sessionCancellation?.Dispose();
        _sessionCancellation = null;
        _audioPump = null;
        _audio = null;
        _resampler = null;
        _partialTranscript.Clear();
        StateDescription = "Stopped";
        Diagnostics.Record("OpenAI transcription stopped.");
    }

    public void Dispose() => Stop();
}
