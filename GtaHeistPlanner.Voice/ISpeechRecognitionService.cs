namespace GtaHeistPlanner.Voice;

public sealed record SpeechRecognizedEventArgs(string Text, float Confidence);
public sealed record PartialTranscriptEventArgs(string Text);
public sealed record SpeechRecognitionFailedEventArgs(Exception Exception);
public interface ISpeechRecognitionService : IDisposable
{
    event EventHandler<SpeechRecognizedEventArgs>? SpeechRecognized;
    event EventHandler<PartialTranscriptEventArgs>? PartialTranscriptChanged;
    event EventHandler<SpeechRecognitionFailedEventArgs>? RecognitionFailed;
    string StateDescription { get; }
    VoiceDiagnosticTrace Diagnostics { get; }
    Task StartAsync(PcmAudioFormat format, IReadOnlyCollection<string> keywords, CancellationToken cancellationToken = default);
    void PushAudio(ReadOnlyMemory<byte> pcmAudio);
    void Stop();
}
