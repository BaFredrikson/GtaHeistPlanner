using GtaHeistPlanner.App.Models;

namespace GtaHeistPlanner.Tests.Voice;

public sealed class VoiceTranscriptEntryTests
{
    [Fact]
    public void CommandsAreGreenAndUnparsedSpeechIsOrange()
    {
        Assert.Equal("#63E67A", new VoiceTranscriptEntry("scope out", true).Color);
        Assert.Equal("#F2A65A", new VoiceTranscriptEntry("unrelated speech", false).Color);
    }
}
