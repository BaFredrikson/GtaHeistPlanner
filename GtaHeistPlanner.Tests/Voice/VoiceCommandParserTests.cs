using GtaHeistPlanner.Core.Loot;
using GtaHeistPlanner.Voice;

namespace GtaHeistPlanner.Tests.Voice;

public sealed class VoiceCommandParserTests
{
    private static readonly LootSpawnDefinition WestPainting = new(
        "west-painting", "main-floor", "West Painting", LootType.Painting, .2, .3)
    {
        VoiceAliases = ["west painting"],
    };

    [Fact]
    public void ActivationAndDeactivationAreTypedCommands()
    {
        var parser = new VoiceCommandParser([WestPainting]);

        Assert.IsType<ActivateScopeOutCommand>(parser.Parse("scope out", ScopeOutSessionState.WaitingForActivation).Command);
        Assert.IsType<DeactivateScopeOutCommand>(parser.Parse("stop scope out", ScopeOutSessionState.Active).Command);
    }

    [Fact]
    public void UnrelatedTextBeforeActivationIsIgnored()
    {
        var result = new VoiceCommandParser([WestPainting]).Parse("west painting 118 thousand",
            ScopeOutSessionState.WaitingForActivation);

        Assert.Equal(VoiceParseDisposition.Ignored, result.Disposition);
    }

    [Theory]
    [InlineData("west painting, 118 thousand", 118000)]
    [InlineData("west painting one hundred eighteen thousand", 118000)]
    [InlineData("west painting 118000", 118000)]
    [InlineData("  WEST   PAINTING, 118 K! ", 118000)]
    public void ParsesAliasAndSpokenValue(string text, int expectedValue)
    {
        var command = Assert.IsType<RecordScopedLootCommand>(
            new VoiceCommandParser([WestPainting]).Parse(text, ScopeOutSessionState.Active).Command);

        Assert.Equal("west-painting", command.LootLocationId);
        Assert.Equal(expectedValue, command.ScopedValue);
    }

    [Fact]
    public void AliasWithoutValueIsAccepted()
    {
        var command = Assert.IsType<RecordScopedLootCommand>(
            new VoiceCommandParser([WestPainting]).Parse("West Painting", ScopeOutSessionState.Active).Command);

        Assert.Null(command.ScopedValue);
    }

    [Fact]
    public void UnknownAliasIsRejected()
    {
        var result = new VoiceCommandParser([WestPainting]).Parse("east sculpture 40 thousand",
            ScopeOutSessionState.Active);

        Assert.Equal(VoiceParseDisposition.Rejected, result.Disposition);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void AmbiguousAliasIsRejectedWithoutGuessing()
    {
        var other = new LootSpawnDefinition("other", "upper-floor", "Other", LootType.Painting, .4, .5)
        {
            VoiceAliases = ["west painting"],
        };

        var result = new VoiceCommandParser([WestPainting, other]).Parse("west painting 34 thousand",
            ScopeOutSessionState.Active);

        Assert.Equal(VoiceParseDisposition.Rejected, result.Disposition);
        Assert.Contains("ambiguous", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
