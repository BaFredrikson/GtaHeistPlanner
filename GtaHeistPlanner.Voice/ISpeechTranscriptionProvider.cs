using GtaHeistPlanner.Core.Settings;

namespace GtaHeistPlanner.Voice;

public sealed record TranscriptionProviderCapabilities(
    bool SupportsPartialTranscripts,
    bool SupportsRealtimeStreaming,
    bool SupportsGpuSelection);

public interface ISpeechTranscriptionProvider : ISpeechRecognitionService
{
    TranscriptionProviderKind Kind { get; }
    TranscriptionProviderCapabilities Capabilities { get; }
    Task<string> TestAsync(CancellationToken cancellationToken = default);
}
