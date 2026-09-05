using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace GtaHeistPlanner.Voice;

public sealed class OpenAiRealtimeWebSocketTransport : IRealtimeTranscriptionTransport
{
    private readonly ClientWebSocket _socket = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private CancellationTokenSource? _receiveCancellation;
    private Task? _receiveTask;
    private bool _closing;

    public event EventHandler<string>? PartialTranscript;
    public event EventHandler<string>? FinalTranscript;
    public event EventHandler<Exception>? Failed;
    public event EventHandler<string>? StateChanged;

    public async Task ConnectAsync(string apiKey, RealtimeTranscriptionOptions options, CancellationToken cancellationToken)
    {
        _socket.Options.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        await _socket.ConnectAsync(new Uri("wss://api.openai.com/v1/realtime?model=gpt-live-transcribe"), cancellationToken);
        StateChanged?.Invoke(this, "Connected; configuring transcription session");
        _receiveCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiveTask = ReceiveLoopAsync(_receiveCancellation.Token);

        var keywords = options.Keywords.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Replace('\r', ' ').Replace('\n', ' ').Replace("<", "").Replace(">", ""))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var message = JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = "session.update",
            session = new
            {
                type = "transcription",
                audio = new
                {
                    input = new
                    {
                        format = new { type = "audio/pcm", rate = 24000 },
                        transcription = new
                        {
                            model = "gpt-live-transcribe",
                            prompt = "GTA heist planning. Transcribe commands and monetary values exactly.",
                            keywords,
                            languages = new[] { "en" },
                            delay = "low"
                        },
                        turn_detection = new { type = "server_vad", threshold = 0.5, prefix_padding_ms = 300, silence_duration_ms = 500 }
                    }
                }
            }
        });
        await SendJsonAsync(message, cancellationToken);
    }

    public async ValueTask SendAudioAsync(ReadOnlyMemory<byte> pcm24KhzMono, CancellationToken cancellationToken)
    {
        if (pcm24KhzMono.IsEmpty || _socket.State != WebSocketState.Open) return;
        var message = JsonSerializer.SerializeToUtf8Bytes(new { type = "input_audio_buffer.append", audio = Convert.ToBase64String(pcm24KhzMono.Span) });
        await SendJsonAsync(message, cancellationToken);
    }

    private async Task SendJsonAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try { await _socket.SendAsync(message, WebSocketMessageType.Text, true, cancellationToken); }
        finally { _sendLock.Release(); }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (!_closing) throw new WebSocketException($"OpenAI closed the connection: {_socket.CloseStatus} {_socket.CloseStatusDescription}");
                        return;
                    }
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
                ProcessMessage(message.ToArray());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { if (!_closing) Failed?.Invoke(this, exception); }
    }

    private void ProcessMessage(ReadOnlyMemory<byte> json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : null;
            switch (type)
            {
                case "session.created": StateChanged?.Invoke(this, "OpenAI session created"); break;
                case "session.updated": StateChanged?.Invoke(this, "OpenAI transcription session ready"); break;
                case "conversation.item.input_audio_transcription.delta":
                    if (root.TryGetProperty("delta", out var delta)) PartialTranscript?.Invoke(this, delta.GetString() ?? "");
                    break;
                case "conversation.item.input_audio_transcription.completed":
                    if (root.TryGetProperty("transcript", out var transcript) && !string.IsNullOrWhiteSpace(transcript.GetString()))
                        FinalTranscript?.Invoke(this, transcript.GetString()!);
                    break;
                case "error":
                    var error = root.TryGetProperty("error", out var errorNode) ? errorNode : root;
                    var code = error.TryGetProperty("code", out var codeNode) ? codeNode.GetString() : null;
                    var detail = error.TryGetProperty("message", out var messageNode) ? messageNode.GetString() : "Unknown OpenAI realtime error.";
                    Failed?.Invoke(this, new InvalidOperationException($"OpenAI realtime error{(code is null ? "" : $" ({code})")}: {detail}"));
                    break;
            }
        }
        catch (JsonException exception) { Failed?.Invoke(this, new InvalidDataException("OpenAI returned a malformed realtime message.", exception)); }
    }

    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        _closing = true;
        _receiveCancellation?.Cancel();
        if (_socket.State == WebSocketState.Open)
            await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Microphone stopped", cancellationToken);
        if (_receiveTask is not null)
            try { await _receiveTask; } catch (OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        try { await CloseAsync(CancellationToken.None); } catch { }
        _receiveCancellation?.Dispose();
        _sendLock.Dispose();
        _socket.Dispose();
    }
}
