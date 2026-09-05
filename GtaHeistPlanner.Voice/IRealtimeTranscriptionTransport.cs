namespace GtaHeistPlanner.Voice;

public sealed record RealtimeTranscriptionOptions(IReadOnlyCollection<string> Keywords);

public interface IRealtimeTranscriptionTransport : IAsyncDisposable
{
    event EventHandler<string>? PartialTranscript;
    event EventHandler<string>? FinalTranscript;
    event EventHandler<Exception>? Failed;
    event EventHandler<string>? StateChanged;
    Task ConnectAsync(string apiKey, RealtimeTranscriptionOptions options, CancellationToken cancellationToken);
    ValueTask SendAudioAsync(ReadOnlyMemory<byte> pcm24KhzMono, CancellationToken cancellationToken);
    Task CloseAsync(CancellationToken cancellationToken);
}
