using GtaHeistPlanner.Voice;

namespace GtaHeistPlanner.Tests.Voice;

public sealed class Pcm16MonoResamplerTests
{
    [Fact]
    public void Convert_Resamples48KhzPcm16MonoToApproximately24Khz()
    {
        var input = new byte[480 * 2];
        for (var i = 0; i < 480; i++)
        {
            var sample = (short)(i * 20);
            input[i * 2] = (byte)sample;
            input[i * 2 + 1] = (byte)(sample >> 8);
        }

        var output = new Pcm16MonoResampler(48_000).Convert(input);

        Assert.InRange(output.Length / 2, 239, 240);
    }

    [Fact]
    public void Convert_RejectsIncompletePcm16Sample()
    {
        var resampler = new Pcm16MonoResampler(48_000);
        Assert.Throws<ArgumentException>(() => resampler.Convert([1]));
    }
}
