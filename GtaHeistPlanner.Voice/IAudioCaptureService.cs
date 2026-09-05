namespace GtaHeistPlanner.Voice;

public sealed record PcmAudioFormat(int SampleRate, int BitsPerSample, int Channels)
{
    public int BlockAlign => Channels * BitsPerSample / 8;
    public int AverageBytesPerSecond => SampleRate * BlockAlign;
}

public sealed record PcmAudioFrameEventArgs(ReadOnlyMemory<byte> Data, int ActivityLevel);

public interface IAudioCaptureService : IDisposable
{
    event EventHandler<PcmAudioFrameEventArgs>? FrameCaptured;
    Stream PcmStream { get; }
    PcmAudioFormat Format { get; }
    string? ActiveDeviceId { get; }
    VoiceDiagnosticTrace Diagnostics { get; }
    void Start(string endpointId);
    void Stop();
}
