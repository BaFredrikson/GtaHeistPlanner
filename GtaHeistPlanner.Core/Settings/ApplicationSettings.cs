namespace GtaHeistPlanner.Core.Settings;

public sealed record ApplicationSettings
{
    public string? RecordingDeviceId { get; init; }
    public string? RecordingDeviceName { get; init; }
    public bool MicrophoneEnabled { get; init; } = true;
    public bool DeveloperMode { get; init; }
}
