using GtaHeistPlanner.Core.Loot;
using GtaHeistPlanner.Core.Maps;
using GtaHeistPlanner.Core.Planning;
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

    [Fact]
    public void PendingLootTargetAcceptsNumericOnlyContinuation()
    {
        var context = new VoiceCommandContext { ScopeOutActive = true, PendingLootValueTargetId = WestPainting.Id };
        var command = Assert.IsType<SetPendingLootValueCommand>(
            new VoiceCommandParser([WestPainting]).Parse("one hundred eighteen thousand", context, PlannerStage.Preparation).Command);
        Assert.Equal(118000, command.ScopedValue);
    }

    [Fact]
    public void RandomNumberWithoutPendingLootIsNotAValueCommand()
    {
        var context = new VoiceCommandContext { ScopeOutActive = true };
        var result = new VoiceCommandParser([WestPainting]).Parse("118 thousand", context, PlannerStage.Preparation);
        Assert.Null(result.Command);
    }

    [Theory]
    [InlineData("plan out")]
    [InlineData("plan it")]
    [InlineData("pan out")]
    [InlineData("pan it")]
    [InlineData("overview")]
    public void PlanningAliasesAreDeterministic(string phrase) =>
        Assert.Equal(PlannerStage.Planning, Assert.IsType<ChangeStageVoiceCommand>(Parse(phrase).Command).Stage);

    [Theory]
    [InlineData("heist start")]
    [InlineData("start heist")]
    [InlineData("start infiltration")]
    [InlineData("infiltration start")]
    public void InfiltrationAliasesAreDeterministic(string phrase) =>
        Assert.Equal(PlannerStage.HeistInfiltration, Assert.IsType<ChangeStageVoiceCommand>(Parse(phrase).Command).Stage);

    [Theory]
    [InlineData("going down skylight")]
    [InlineData("using access codes")]
    [InlineData("alpha mail arriving")]
    public void ActivityAliasesAreDeterministic(string phrase) =>
        Assert.Equal(PlannerStage.HeistActivity, Assert.IsType<ChangeStageVoiceCommand>(Parse(phrase).Command).Stage);

    [Fact]
    public void BackUpExitsMapFocusAndDoesNotUndo() =>
        Assert.IsType<ExitMapFocusVoiceCommand>(Parse("back up").Command);

    [Fact]
    public void MapAliasAndUnknownMapAreHandledWithoutGuessing()
    {
        var parser = new VoiceCommandParser([WestPainting], KortzMapCatalog.Maps);
        Assert.Equal("main-floor", Assert.IsType<FocusMapVoiceCommand>(parser.Parse("pull up main floor", new(), PlannerStage.Preparation).Command).MapId);
        Assert.Equal(VoiceParseDisposition.Rejected, parser.Parse("pull up moon base", new(), PlannerStage.Preparation).Disposition);
    }

    [Theory]
    [InlineData("vault code 46 18 73")]
    [InlineData("vault code forty six eighteen seventy three")]
    public void ParsesInlineVaultCode(string phrase) =>
        Assert.Equal("46-18-73", Assert.IsType<SetVaultCodeVoiceCommand>(Parse(phrase).Command).VaultCode);

    [Fact]
    public void PendingModesTakePrecedenceBeforeLoot()
    {
        var parser = new VoiceCommandParser([WestPainting]);
        Assert.IsType<SetVaultCodeVoiceCommand>(parser.Parse("46 18 73", new() { AwaitingVaultCode = true }, PlannerStage.Preparation).Command);
        Assert.IsType<ApplySewerRouteVoiceCommand>(parser.Parse("2 C 1 B 4 D", new() { AwaitingSewerRoute = true }, PlannerStage.HeistInfiltration).Command);
    }

    [Theory]
    [InlineData("2 C 1 B 4 D", "2C 1B 4D")]
    [InlineData("two C one B four D", "2C 1B 4D")]
    [InlineData("two see one bee four dee", "2C 1B 4D")]
    [InlineData("two Charlie one Bravo four Delta", "2C 1B 4D")]
    [InlineData("Chamber TWO, c; chamber One b! FOUR d.", "2C 1B 4D")]
    public void PendingSewerRouteNormalizesDeterministically(string phrase, string expected)
    {
        var result = new VoiceCommandParser([WestPainting]).Parse(phrase,
            new() { AwaitingSewerRoute = true }, PlannerStage.HeistInfiltration);
        Assert.Equal(expected, Assert.IsType<ApplySewerRouteVoiceCommand>(result.Command).RouteText);
    }

    [Fact]
    public void DirectSewerRoutePrefixNormalizesWithoutPendingMode()
    {
        var result = Parse("sewer route two see one bee four dee");
        Assert.Equal("2C 1B 4D", Assert.IsType<ApplySewerRouteVoiceCommand>(result.Command).RouteText);
    }

    [Fact]
    public void GlobalCommandRetainsPrecedenceWhileAwaitingSewerRoute()
    {
        var result = new VoiceCommandParser([WestPainting]).Parse("undo",
            new() { AwaitingSewerRoute = true }, PlannerStage.HeistInfiltration);
        Assert.IsType<UndoVoiceCommand>(result.Command);
    }

    private static VoiceCommandParseResult Parse(string phrase) =>
        new VoiceCommandParser([WestPainting], KortzMapCatalog.Maps).Parse(phrase, new(), PlannerStage.Preparation);
}
