using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace GtaHeistPlanner.Voice;

public sealed class WasapiAudioCaptureService : IAudioCaptureService
{
    private WasapiRecorder? _capture;
    private MMDevice? _device;
    private BufferedPcmStream? _stream;
    private WaveFormat? _sourceFormat;
    private bool _receivedFirstFrame;

    public WasapiAudioCaptureService(VoiceDiagnosticTrace? diagnostics = null)
    {
        Diagnostics = diagnostics ?? new VoiceDiagnosticTrace();
    }

    public event EventHandler<PcmAudioFrameEventArgs>? FrameCaptured;
    public Stream PcmStream => _stream ?? throw new InvalidOperationException("Audio capture has not started.");
    public PcmAudioFormat Format { get; private set; } = new(16000, 16, 1);
    public string? ActiveDeviceId { get; private set; }
    public VoiceDiagnosticTrace Diagnostics { get; }

    public void Start(string endpointId)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("WASAPI audio capture is only available on Windows.");
        try
        {
            StartWindows(endpointId);
        }
        catch (Exception exception)
        {
            Diagnostics.RecordException("WASAPI capture startup", exception);
            throw;
        }
    }

    [SupportedOSPlatform("windows")]
    private void StartWindows(string endpointId)
    {
        if (_capture is not null)
            return;

        using var enumerator = new MMDeviceEnumerator();
        var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToArray();
        var selected = AudioEndpointSelector.ResolveExact(endpointId,
            endpoints.Select(endpoint => new AudioEndpointDescriptor(endpoint.ID, endpoint.FriendlyName)));
        Diagnostics.Record($"Endpoint resolved: '{selected.Name}' ({selected.Id}).");
        _device = enumerator.GetDevice(selected.Id);
        _capture = new WasapiRecorderBuilder()
            .WithDevice(_device)
            .WithSharedMode()
            .WithEventSync()
            .WithBufferLength(40)
            .Build();
        Diagnostics.Record("WASAPI capture created.");
        _sourceFormat = _capture.WaveFormat is WaveFormatExtensible extensible
            ? extensible.ToStandardWaveFormat()
            : _capture.WaveFormat;
        ValidateSourceFormat(_sourceFormat);

        Format = new(_sourceFormat.SampleRate, 16, 1);
        _stream?.Dispose();
        _stream = new BufferedPcmStream(Diagnostics);
        _capture.DataAvailable += (buffer, _, _, _) => OnDataAvailable(buffer);
        _capture.RecordingStopped += OnRecordingStopped;
        _capture.StartRecording();
        Diagnostics.Record("WASAPI capture started.");
        ActiveDeviceId = selected.Id;
    }

    [SupportedOSPlatform("windows")]
    private void OnDataAvailable(ReadOnlySpan<byte> buffer)
    {
        if (_sourceFormat is null || _stream is null || buffer.Length == 0)
            return;
        var pcm = ConvertToMonoPcm16(buffer, _sourceFormat);
        if (!_receivedFirstFrame)
        {
            _receivedFirstFrame = true;
            Diagnostics.Record($"First PCM frame received: {pcm.Length} bytes, {Format.SampleRate} Hz, 16-bit mono.");
        }
        _stream.WriteFrame(pcm);
        FrameCaptured?.Invoke(this, new(pcm, CalculateActivity(pcm)));
    }

    [SupportedOSPlatform("windows")]
    private void OnRecordingStopped(object? sender, StoppedEventArgs e) => _stream?.Complete();

    private static void ValidateSourceFormat(WaveFormat format)
    {
        if (format.Channels <= 0 || format.Encoding is not (WaveFormatEncoding.Pcm or WaveFormatEncoding.IeeeFloat) ||
            format.BitsPerSample is not (16 or 24 or 32))
            throw new NotSupportedException($"Unsupported capture sample format: {format}.");
    }

    private static byte[] ConvertToMonoPcm16(ReadOnlySpan<byte> source, WaveFormat format)
    {
        var bytesPerSample = format.BitsPerSample / 8;
        var frameSize = bytesPerSample * format.Channels;
        var frameCount = source.Length / frameSize;
        var output = new byte[frameCount * 2];
        for (var frame = 0; frame < frameCount; frame++)
        {
            double sum = 0;
            for (var channel = 0; channel < format.Channels; channel++)
            {
                var offset = frame * frameSize + channel * bytesPerSample;
                sum += ReadSample(source[offset..], format.Encoding, format.BitsPerSample);
            }
            var sample = (short)Math.Clamp(sum / format.Channels * short.MaxValue, short.MinValue, short.MaxValue);
            output[frame * 2] = (byte)sample;
            output[frame * 2 + 1] = (byte)(sample >> 8);
        }
        return output;
    }

    private static double ReadSample(ReadOnlySpan<byte> source, WaveFormatEncoding encoding, int bits)
    {
        if (encoding == WaveFormatEncoding.IeeeFloat && bits == 32)
            return BitConverter.ToSingle(source[..4]);
        return bits switch
        {
            16 => BitConverter.ToInt16(source[..2]) / 32768d,
            24 => ((source[0] | source[1] << 8 | source[2] << 16) << 8 >> 8) / 8388608d,
            32 => BitConverter.ToInt32(source[..4]) / 2147483648d,
            _ => throw new NotSupportedException(),
        };
    }

    private static int CalculateActivity(ReadOnlySpan<byte> pcm)
    {
        var peak = 0;
        for (var index = 0; index + 1 < pcm.Length; index += 2)
            peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(pcm[index..(index + 2)])));
        return Math.Clamp((int)Math.Round(peak / 32767d * 100), 0, 100);
    }

    public void Stop()
    {
        if (!OperatingSystem.IsWindows())
            return;
        StopWindows();
    }

    [SupportedOSPlatform("windows")]
    private void StopWindows()
    {
        if (_capture is not null)
        {
            _capture.RecordingStopped -= OnRecordingStopped;
            _capture.StopRecording();
            _capture.Dispose();
        }
        _stream?.Complete();
        _capture = null;
        _device?.Dispose();
        _device = null;
        _sourceFormat = null;
        ActiveDeviceId = null;
        _receivedFirstFrame = false;
    }

    public void Dispose()
    {
        Stop();
        _stream?.Dispose();
        _stream = null;
    }
}
