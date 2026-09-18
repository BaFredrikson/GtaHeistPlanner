using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Channels;
using GtaHeistPlanner.Core.Settings;

namespace GtaHeistPlanner.Voice;

public sealed class OpenAiCompatibleTranscriptionProvider(Uri endpoint, string model, Func<string?> apiKeyProvider,
    HttpClient? httpClient = null, VoiceDiagnosticTrace? diagnostics = null) : ISpeechTranscriptionProvider
{
    private readonly HttpClient _http = httpClient ?? new HttpClient();
    private readonly bool _ownsHttp = httpClient is null;
    private readonly VoiceDiagnosticTrace _diagnostics = diagnostics ?? new();
    private Channel<AudioFrame>? _frames;
    private CancellationTokenSource? _cancellation;
    private Task? _pump;
    private PcmAudioFormat? _format;

    public event EventHandler<SpeechRecognizedEventArgs>? SpeechRecognized;
    public event EventHandler<PartialTranscriptEventArgs>? PartialTranscriptChanged { add { } remove { } }
    public event EventHandler<SpeechRecognitionFailedEventArgs>? RecognitionFailed;
    public TranscriptionProviderKind Kind => TranscriptionProviderKind.Custom;
    public TranscriptionProviderCapabilities Capabilities { get; } = new(false, false, false);
    public string StateDescription { get; private set; } = "Stopped";
    public VoiceDiagnosticTrace Diagnostics => _diagnostics;

    public Task StartAsync(PcmAudioFormat format, IReadOnlyCollection<string> keywords, CancellationToken cancellationToken = default)
    {
        if (format.BitsPerSample != 16 || format.Channels != 1) throw new NotSupportedException("Custom transcription requires mono PCM16 input.");
        if (string.IsNullOrWhiteSpace(model)) throw new InvalidOperationException("A custom transcription model is required.");
        _format = format;
        _frames = Channel.CreateBounded<AudioFrame>(new BoundedChannelOptions(16) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pump = PumpAsync(_cancellation.Token);
        StateDescription = $"Listening (Custom: {model})";
        return Task.CompletedTask;
    }

    public void PushAudio(ReadOnlyMemory<byte> pcmAudio, int activityLevel) => _frames?.Writer.TryWrite(new(pcmAudio.ToArray(), activityLevel));

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        var audio = new List<byte>(); var active = false; var silence = 0d;
        try
        {
            await foreach (var frame in _frames!.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var duration = frame.Data.Length * 1000d / _format!.AverageBytesPerSecond;
                if (frame.Activity >= 3) { active = true; silence = 0; } else if (active) silence += duration;
                if (!active) continue;
                audio.AddRange(frame.Data);
                if (silence < 700) continue;
                await TranscribeAsync(audio.ToArray(), cancellationToken).ConfigureAwait(false);
                audio.Clear(); active = false; silence = 0;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { StateDescription = "Custom provider error"; _diagnostics.RecordException("Custom transcription", exception); RecognitionFailed?.Invoke(this, new(exception)); }
    }

    private async Task TranscribeAsync(byte[] pcm, CancellationToken cancellationToken)
    {
        StateDescription = "Transcribing (Custom)";
        using var request = new HttpRequestMessage(HttpMethod.Post, TranscriptionsUri());
        AddAuthorization(request);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(model), "model");
        var wav = new ByteArrayContent(CreateWave(pcm, _format!));
        wav.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(wav, "file", "utterance.wav");
        request.Content = form;
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Custom endpoint rejected transcription ({(int)response.StatusCode}): {body}");
        using var json = JsonDocument.Parse(body);
        var text = json.RootElement.TryGetProperty("text", out var value) ? value.GetString()?.Trim() : null;
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Custom endpoint did not return the OpenAI-compatible 'text' field.");
        StateDescription = $"Listening (Custom: {model})";
        SpeechRecognized?.Invoke(this, new(text, float.NaN));
    }

    public async Task<string> TestAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint, "models")); AddAuthorization(request);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Custom endpoint connection failed ({(int)response.StatusCode}).");
        return "Custom OpenAI-compatible endpoint is reachable.";
    }

    private Uri TranscriptionsUri() => endpoint.AbsolutePath.EndsWith("/audio/transcriptions", StringComparison.OrdinalIgnoreCase)
        ? endpoint : new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/audio/transcriptions");
    private void AddAuthorization(HttpRequestMessage request) { var key = apiKeyProvider(); if (!string.IsNullOrWhiteSpace(key)) request.Headers.Authorization = new("Bearer", key); }

    private static byte[] CreateWave(byte[] pcm, PcmAudioFormat format)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(36 + pcm.Length); writer.Write("WAVEfmt "u8); writer.Write(16); writer.Write((short)1);
        writer.Write((short)format.Channels); writer.Write(format.SampleRate); writer.Write(format.AverageBytesPerSecond);
        writer.Write((short)format.BlockAlign); writer.Write((short)format.BitsPerSample); writer.Write("data"u8); writer.Write(pcm.Length); writer.Write(pcm);
        return stream.ToArray();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default) { _frames?.Writer.TryComplete(); _cancellation?.Cancel(); if (_pump is not null) try { await _pump.ConfigureAwait(false); } catch (OperationCanceledException) { } _cancellation?.Dispose(); _frames = null; _pump = null; StateDescription = "Stopped"; }
    public void Dispose() { _cancellation?.Cancel(); if (_ownsHttp) _http.Dispose(); }
    private sealed record AudioFrame(byte[] Data, int Activity);
}
