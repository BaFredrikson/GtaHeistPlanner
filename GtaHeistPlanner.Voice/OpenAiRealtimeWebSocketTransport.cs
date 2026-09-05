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
    private bool _receivedFirstEvent;
    private bool _sentFirstAudio;

    public event EventHandler<string>? PartialTranscript;
    public event EventHandler<string>? FinalTranscript;
    public event EventHandler<Exception>? Failed;
    public event EventHandler<string>? StateChanged;

    public async Task ConnectAsync(string apiKey, RealtimeTranscriptionOptions options, CancellationToken cancellationToken)
    {
        _socket.Options.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        StateChanged?.Invoke(this, ThreadMessage($"Opening transcription WebSocket: {OpenAiRealtimeTranscriptionProtocol.WebSocketEndpoint.PathAndQuery}"));
        await _socket.ConnectAsync(OpenAiRealtimeTranscriptionProtocol.WebSocketEndpoint, cancellationToken).ConfigureAwait(false);
        StateChanged?.Invoke(this, ThreadMessage($"WebSocket endpoint opened: {OpenAiRealtimeTranscriptionProtocol.WebSocketEndpoint}"));
        _receiveCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiveTask = ReceiveLoopAsync(_receiveCancellation.Token);
        var sessionUpdate = OpenAiRealtimeTranscriptionProtocol.CreateSessionUpdate(options.Keywords);
        StateChanged?.Invoke(this, ThreadMessage($"Sending {OpenAiRealtimeTranscriptionProtocol.SessionUpdateEventType}; model={OpenAiRealtimeTranscriptionProtocol.Model}"));
        await SendJsonAsync(sessionUpdate, OpenAiRealtimeTranscriptionProtocol.SessionUpdateEventType, cancellationToken).ConfigureAwait(false);
        StateChanged?.Invoke(this, ThreadMessage($"Transcription session configured; model={OpenAiRealtimeTranscriptionProtocol.Model}"));
    }

    public async ValueTask SendAudioAsync(ReadOnlyMemory<byte> pcm24KhzMono, CancellationToken cancellationToken)
    {
        if (pcm24KhzMono.IsEmpty || _socket.State != WebSocketState.Open) return;
        var message = OpenAiRealtimeTranscriptionProtocol.CreateAudioAppend(pcm24KhzMono.Span);
        var isFirstAudio = !_sentFirstAudio;
        if (isFirstAudio)
        {
            _sentFirstAudio = true;
            StateChanged?.Invoke(this, ThreadMessage($"Sending first {OpenAiRealtimeTranscriptionProtocol.AudioAppendEventType} (audio payload omitted)"));
        }
        await SendJsonAsync(message, OpenAiRealtimeTranscriptionProtocol.AudioAppendEventType, cancellationToken, isFirstAudio).ConfigureAwait(false);
    }

    public async ValueTask CommitAudioAsync(CancellationToken cancellationToken)
    {
        var message = OpenAiRealtimeTranscriptionProtocol.CreateAudioCommit();
        StateChanged?.Invoke(this, ThreadMessage($"Sending {OpenAiRealtimeTranscriptionProtocol.AudioCommitEventType}"));
        await SendJsonAsync(message, OpenAiRealtimeTranscriptionProtocol.AudioCommitEventType, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendJsonAsync(ReadOnlyMemory<byte> message, string messageType, CancellationToken cancellationToken, bool logCompletion = true)
    {
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _socket.SendAsync(message, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            if (logCompletion)
                StateChanged?.Invoke(this, ThreadMessage($"Outgoing WebSocket message completed: {messageType}"));
        }
        finally { _sendLock.Release(); }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        StateChanged?.Invoke(this, ThreadMessage("WebSocket receive loop started"));
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        StateChanged?.Invoke(this, ThreadMessage($"WebSocket close received. Status={_socket.CloseStatus}; reason={_socket.CloseStatusDescription ?? "(none)"}"));
                        if (!_closing) throw new WebSocketException($"OpenAI closed the connection: {_socket.CloseStatus} {_socket.CloseStatusDescription}");
                        return;
                    }
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
                if (!_receivedFirstEvent)
                {
                    _receivedFirstEvent = true;
                    StateChanged?.Invoke(this, ThreadMessage("First server event received"));
                }
                ProcessMessage(message.ToArray());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StateChanged?.Invoke(this, ThreadMessage("WebSocket receive loop cancelled"));
        }
        catch (Exception exception)
        {
            if (!_closing) Failed?.Invoke(this, exception);
        }
        finally { StateChanged?.Invoke(this, ThreadMessage("WebSocket receive loop exited")); }
    }

    private void ProcessMessage(ReadOnlyMemory<byte> json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : null;
            if (string.IsNullOrWhiteSpace(type))
                throw new InvalidDataException("OpenAI realtime server event did not contain a type.");
            StateChanged?.Invoke(this, ThreadMessage($"Server event: {type}"));
            switch (type)
            {
                case "session.created": StateChanged?.Invoke(this, "OpenAI transcription session created"); break;
                case "session.updated": StateChanged?.Invoke(this, "OpenAI transcription session ready"); break;
                case "conversation.item.input_audio_transcription.delta":
                    StateChanged?.Invoke(this, ThreadMessage("Transcript delta event"));
                    if (root.TryGetProperty("delta", out var delta)) PartialTranscript?.Invoke(this, delta.GetString() ?? "");
                    break;
                case "conversation.item.input_audio_transcription.completed":
                    StateChanged?.Invoke(this, ThreadMessage("Transcript completion event"));
                    if (root.TryGetProperty("transcript", out var transcript) && !string.IsNullOrWhiteSpace(transcript.GetString()))
                        FinalTranscript?.Invoke(this, transcript.GetString()!);
                    break;
                case "error":
                    var error = root.TryGetProperty("error", out var errorNode) ? errorNode : root;
                    var code = error.TryGetProperty("code", out var codeNode) ? codeNode.GetString() : null;
                    var detail = error.TryGetProperty("message", out var messageNode) ? messageNode.GetString() : "Unknown OpenAI realtime error.";
                    var errorText = error.GetRawText();
                    StateChanged?.Invoke(this, ThreadMessage($"OpenAI server error event: {errorText}"));
                    Failed?.Invoke(this, new InvalidOperationException($"OpenAI realtime error{(code is null ? "" : $" ({code})")}: {detail}. Full error: {errorText}"));
                    break;
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            Failed?.Invoke(this, new InvalidDataException("OpenAI returned a malformed realtime message.", exception));
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        _closing = true;
        StateChanged?.Invoke(this, ThreadMessage("WebSocket cancellation/disposal started"));
        _receiveCancellation?.Cancel();
        if (_socket.State == WebSocketState.Open)
        {
            using var closeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            closeTimeout.CancelAfter(TimeSpan.FromSeconds(2));
            try { await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Microphone stopped", closeTimeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (closeTimeout.IsCancellationRequested)
            {
                StateChanged?.Invoke(this, ThreadMessage("WebSocket graceful close timed out; aborting"));
                _socket.Abort();
            }
        }
        if (_receiveTask is not null)
            try { await _receiveTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        StateChanged?.Invoke(this, ThreadMessage($"WebSocket disposed. Status={_socket.CloseStatus}; reason={_socket.CloseStatusDescription ?? "(none)"}"));
    }

    public async ValueTask DisposeAsync()
    {
        try { await CloseAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
        _receiveCancellation?.Dispose();
        _sendLock.Dispose();
        _socket.Dispose();
    }

    private static string ThreadMessage(string operation) =>
        operation;
}
