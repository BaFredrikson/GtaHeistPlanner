using GtaHeistPlanner.Voice;

namespace GtaHeistPlanner.Tests.Voice;

public sealed class TranscriptSanitizerTests
{
    [Theory]
    [InlineData("[BLANK AUDIO]")]
    [InlineData(" [BLANK AUDIO] ")]
    [InlineData("[blank audio]")]
    [InlineData("... [  blank   audio ] !!!")]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyAndKnownNonSpeechMarkersAreFiltered(string value)
    {
        Assert.False(TranscriptSanitizer.TrySanitize(value, out var sanitized));
        Assert.Equal(string.Empty, sanitized);
    }

    [Theory]
    [InlineData("undo")]
    [InlineData("three")]
    [InlineData("route")]
    [InlineData("camera")]
    public void LegitimateShortSpeechIsPreserved(string value)
    {
        Assert.True(TranscriptSanitizer.TrySanitize(value, out var sanitized));
        Assert.Equal(value, sanitized);
    }
}
