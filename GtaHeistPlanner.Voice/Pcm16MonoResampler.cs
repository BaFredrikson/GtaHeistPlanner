namespace GtaHeistPlanner.Voice;

public sealed class Pcm16MonoResampler
{
    private readonly double _step;
    private readonly List<short> _samples = [];
    private double _position;

    public Pcm16MonoResampler(int inputSampleRate, int outputSampleRate = 24_000)
    {
        if (inputSampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(inputSampleRate));
        if (outputSampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(outputSampleRate));
        _step = (double)inputSampleRate / outputSampleRate;
    }

    public byte[] Convert(ReadOnlySpan<byte> pcm16Mono)
    {
        if ((pcm16Mono.Length & 1) != 0)
            throw new ArgumentException("PCM16 data must contain complete two-byte samples.", nameof(pcm16Mono));

        for (var i = 0; i < pcm16Mono.Length; i += 2)
            _samples.Add((short)(pcm16Mono[i] | pcm16Mono[i + 1] << 8));

        var output = new List<short>((int)Math.Ceiling(_samples.Count / _step));
        while (_position + 1 < _samples.Count)
        {
            var left = (int)_position;
            var fraction = _position - left;
            output.Add((short)Math.Clamp(Math.Round(_samples[left] + (_samples[left + 1] - _samples[left]) * fraction), short.MinValue, short.MaxValue));
            _position += _step;
        }

        var consumed = (int)_position;
        if (consumed > 0)
        {
            _samples.RemoveRange(0, Math.Min(consumed, _samples.Count));
            _position -= consumed;
        }

        var bytes = new byte[output.Count * 2];
        for (var i = 0; i < output.Count; i++)
        {
            bytes[i * 2] = (byte)output[i];
            bytes[i * 2 + 1] = (byte)(output[i] >> 8);
        }
        return bytes;
    }
}
