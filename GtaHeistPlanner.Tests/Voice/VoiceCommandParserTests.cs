using GtaHeistPlanner.Core.Loot;
using GtaHeistPlanner.Core.Maps;
using GtaHeistPlanner.Core.Planning;
using GtaHeistPlanner.Core.Security;
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
    [InlineData("west painting 118 thousand 500", 118500)]
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
        Assert.True(command.ValueParse!.UsedThousandsUnit);
    }

    [Fact]
    public void NumericRemainderRequiresExplicitContinuationContext()
    {
        var parser = new VoiceCommandParser([WestPainting]);
        var context = new VoiceCommandContext
        {
            ScopeOutActive = true,
            NumericContinuationTargetId = WestPainting.Id,
            NumericContinuationBaseValue = 118000,
        };
        var command = Assert.IsType<ContinueLootValueCommand>(parser.Parse("five hundred", context, PlannerStage.Preparation).Command);
        Assert.Equal(500, command.Remainder);
        Assert.Null(parser.Parse("500", new() { ScopeOutActive = true }, PlannerStage.Preparation).Command);
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

    [Theory]
    [InlineData("tango down")]
    [InlineData("guard down")]
    [InlineData("guard dropped")]
    [InlineData("dropped guard")]
    [InlineData("dropped a guard")]
    [InlineData("took out guard")]
    [InlineData("took out a guard")]
    [InlineData("guard neutralized")]
    [InlineData("  GUARD DOWN! ")]
    public void GuardAliasesShareOneCommand(string phrase) =>
        Assert.IsType<IncrementGuardsDownVoiceCommand>(Parse(phrase).Command);

    [Theory]
    [InlineData("Front Desk")]
    [InlineData("front desk")]
    [InlineData("Front Desk down")]
    [InlineData("I got Front Desk")]
    [InlineData("Front Desk is down")]
    [InlineData("just took out Front Desk")]
    [InlineData("that's Front-Desk dealt with!")]
    public void NamedGuardAliasMatchesAsWholePhraseAnywhere(string phrase)
    {
        var guard = Guard("front-desk", "lower-floor", "Front Desk Guard", ["Front Desk"]);
        var command = Assert.IsType<DisableNamedGuardVoiceCommand>(new VoiceCommandParser(
            [WestPainting], guards: [guard]).Parse(phrase, new(), PlannerStage.HeistActivity).Command);
        Assert.Equal("front-desk", command.GuardId);
    }

    [Fact]
    public void NamedGuardRequiresWholePhraseAndRelevantInteriorStage()
    {
        var guard = Guard("rail", "upper-floor", "Rail Guard", ["Rail"]);
        var parser = new VoiceCommandParser([WestPainting], guards: [guard]);

        Assert.Null(parser.Parse("railing is clear", new(), PlannerStage.HeistActivity).Command);
        Assert.Null(parser.Parse("rail", new(), PlannerStage.HeistInfiltration).Command);
        Assert.Equal("rail", Assert.IsType<DisableNamedGuardVoiceCommand>(parser.Parse(
            "okay rail", new(), PlannerStage.HeistActivity).Command).GuardId);
    }

    [Fact]
    public void UnnamedGuardsAreIgnoredAndDuplicateAliasesAreAmbiguous()
    {
        var unnamed = Guard("main-floor-guard-01", "main-floor", "main-floor-guard-01", []);
        var first = Guard("desk-a", "main-floor", "First", ["Desk"]);
        var second = Guard("desk-b", "lower-floor", "Second", ["Desk"]);
        var parser = new VoiceCommandParser([WestPainting], guards: [unnamed, first, second]);

        Assert.Null(parser.Parse("main floor guard 01", new(), PlannerStage.HeistActivity).Command);
        var ambiguity = parser.Parse("desk down", new(), PlannerStage.HeistActivity);
        Assert.Equal(VoiceParseDisposition.Rejected, ambiguity.Disposition);
        Assert.Contains("desk-a", ambiguity.Error);
        Assert.Contains("desk-b", ambiguity.Error);
    }

    private static SecurityGuardDefinition Guard(string id, string mapId, string name, IReadOnlyList<string> aliases) =>
        new(id, mapId, name, .5, .5, []) { VoiceAliases = aliases };

    [Theory]
    [InlineData("camera down")]
    [InlineData("camera disabled")]
    [InlineData("disabled camera")]
    [InlineData("charlie down")]
    [InlineData("Camera Disabled!")]
    public void CameraAliasesShareOneCommand(string phrase) =>
        Assert.IsType<IncrementCamerasDownVoiceCommand>(Parse(phrase).Command);

    [Fact]
    public void NamedCameraAndShowroomButtonResolveToDedicatedCommands()
    {
        var camera = new GtaHeistPlanner.Core.Security.SecurityCameraDefinition(
            "showroom", "main-floor", "Showroom Camera", .5, .5, 0, 60, .2);
        var parser = new VoiceCommandParser([WestPainting], KortzMapCatalog.Maps, [camera]);

        Assert.Equal("showroom", Assert.IsType<DisableNamedCameraVoiceCommand>(parser.Parse(
            "I got Showroom Camera!", new(), PlannerStage.HeistActivity).Command).CameraId);
        Assert.IsType<DisableShowroomByButtonVoiceCommand>(parser.Parse(
            "shot the button", new(), PlannerStage.HeistInfiltration).Command);
    }

    [Fact]
    public void NamedCameraMatchingUsesStageVisibleMapCategory()
    {
        var exterior = new SecurityCameraDefinition("west-gate", "exterior-firstfloor", "West Gate Camera", .5, .5, 0, 60, .2);
        var interior = new SecurityCameraDefinition("showroom", "main-floor", "Showroom Camera", .5, .5, 0, 60, .2);
        var parser = new VoiceCommandParser([WestPainting], KortzMapCatalog.Maps, [exterior, interior]);

        Assert.Equal("west-gate", Assert.IsType<DisableNamedCameraVoiceCommand>(parser.Parse(
            "I got West Gate Camera", new(), PlannerStage.HeistInfiltration).Command).CameraId);
        Assert.Null(parser.Parse("Showroom Camera down", new(), PlannerStage.HeistInfiltration).Command);
        Assert.Equal("showroom", Assert.IsType<DisableNamedCameraVoiceCommand>(parser.Parse(
            "Showroom Camera", new(), PlannerStage.HeistActivity).Command).CameraId);
        Assert.Null(parser.Parse("West Gate Camera down", new(), PlannerStage.HeistActivity).Command);
    }

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

    [Theory]
    [InlineData("free Charlie", "3C")]
    [InlineData("tree sea", "3C")]
    [InlineData("for the", "4D")]
    [InlineData("one bee", "1B")]
    [InlineData("number two tunnel Charlie please", "2C")]
    [InlineData("33 Charlie", "3C")]
    [InlineData("43 Charlie", "3C")]
    [InlineData("52 Bravo", "2B")]
    public void SewerOnlySttAliasesNormalizeToConstrainedInstructions(string phrase, string expected)
    {
        Assert.True(SewerVoiceNormalizer.TryNormalize(phrase, out var route, out _));
        Assert.Equal(expected, route);
    }

    [Theory]
    [InlineData("sewer route")]
    [InlineData("sower route")]
    [InlineData("so we're route")]
    [InlineData("sewer")]
    [InlineData("sower")]
    public void SewerActivationAliasesWorkInInfiltration(string phrase)
    {
        var result = new VoiceCommandParser([WestPainting]).Parse(phrase, new(), PlannerStage.HeistInfiltration);
        Assert.IsType<EnterSewerRouteVoiceCommand>(result.Command);
    }

    [Theory]
    [InlineData("so")]
    [InlineData("so uh")]
    [InlineData("so we're")]
    [InlineData("so we're uh")]
    [InlineData("so we're up")]
    [InlineData("route")]
    public void AmbiguousSewerActivationDoesNotApplyGlobally(string phrase)
    {
        var result = new VoiceCommandParser([WestPainting]).Parse(phrase, new(), PlannerStage.Preparation);
        Assert.IsNotType<EnterSewerRouteVoiceCommand>(result.Command);
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
