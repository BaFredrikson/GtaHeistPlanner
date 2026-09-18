namespace GtaHeistPlanner.Core.Settings;

public sealed record ApplicationSettings
{
    public string? RecordingDeviceId { get; init; }
    public string? RecordingDeviceName { get; init; }
    public bool MicrophoneEnabled { get; init; } = true;
    public bool DeveloperMode { get; init; }
    public TranscriptionProviderKind VoiceProvider { get; init; } = TranscriptionProviderKind.LocalWhisper;
    public string LocalWhisperModel { get; init; } = "small.en";
    public LocalWhisperCompute LocalWhisperCompute { get; init; } = LocalWhisperCompute.Auto;
    public string OpenAiTranscriptionModel { get; init; } = "gpt-live-transcribe";
    public string CustomTranscriptionEndpoint { get; init; } = string.Empty;
    public string CustomTranscriptionModel { get; init; } = string.Empty;
    public bool HasOpenAiApiKey { get; init; }
    public bool HasCustomApiKey { get; init; }
}
