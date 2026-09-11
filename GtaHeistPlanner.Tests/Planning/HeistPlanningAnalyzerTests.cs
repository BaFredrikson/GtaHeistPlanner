using GtaHeistPlanner.Core.Loot;
using GtaHeistPlanner.Core.Planning;

namespace GtaHeistPlanner.Tests.Planning;

public sealed class HeistPlanningAnalyzerTests
{
    [Fact]
    public void ExactScopedValueOverridesEstimateButCargoAndBoxesRemainEstimated()
    {
        var analysis = Analyze(1,
            Loot("painting", LootType.Painting), State("painting", value: 118000),
            Loot("cargo", LootType.LoadingBayCargo), State("cargo", value: 999999),
            Loot("boxes", LootType.SafetyDepositBoxes), State("boxes", value: 999999));

        Assert.Equal(118000, analysis.KnownExactValue);
        Assert.Equal(110000, analysis.EstimatedMinValue);
        Assert.Equal(152000, analysis.EstimatedMaxValue);
        Assert.Null(analysis.RecommendedHaul.SelectedLoot.Single(item => item.LootId == "cargo").KnownExactValue);
        Assert.DoesNotContain(analysis.RecommendedHaul.SelectedLoot, item => item.LootId == "boxes");
    }

    [Fact]
    public void CrispAccessibilityDependsOnCrewAndInaccessibleLootIsNeverOptimized()
    {
        var definition = Loot("crisp", LootType.Painting, LootZoneCatalog.CrispGalleryId);
        var solo = HeistPlanningAnalyzer.Analyze([definition], [State("crisp", value: 150000)], 1);
        var duo = HeistPlanningAnalyzer.Analyze([definition], [State("crisp", value: 150000)], 2);
        Assert.Equal(1, solo.InaccessibleScopedCount);
        Assert.Empty(solo.RecommendedHaul.SelectedLoot);
        Assert.Equal(1, duo.AccessibleScopedCount);
        Assert.Contains(duo.RecommendedHaul.SelectedLoot, item => item.LootId == "crisp");
    }

    [Theory]
    [InlineData(1, 100)] [InlineData(2, 200)] [InlineData(4, 400)]
    public void CrewCapacityIsPlayerCountTimesOneHundred(int players, int capacity) =>
        Assert.Equal(capacity, HeistPlanningAnalyzer.Analyze([], [], players).CrewCapacityPercent);

    [Fact]
    public void BuyersRequestIsReservedAndOverCapacityConflictIsExplicit()
    {
        var candidates = new[]
        {
            Candidate("buyer", 50, 40000, buyer: true), Candidate("normal", 50, 200000), Candidate("small", 50, 100000),
        };
        var result = HeistPlanningAnalyzer.Optimize(candidates, 100);
        Assert.Contains(result.SelectedLoot, item => item.LootId == "buyer");
        Assert.Contains(result.SelectedLoot, item => item.LootId == "normal");

        var conflict = HeistPlanningAnalyzer.Optimize([
            Candidate("b1", 50, 100, true), Candidate("b2", 50, 100, true), Candidate("b3", 50, 100, true)], 100);
        Assert.True(conflict.BuyersRequestCapacityConflict);
        Assert.NotNull(conflict.Diagnostic);
        Assert.True(conflict.BagUsagePercent <= 100);
    }

    [Fact]
    public void KnapsackChoosesBestCombinationAndExactValuesAffectSelection()
    {
        var combined = HeistPlanningAnalyzer.Optimize([
            Candidate("large", 50, 100000), Candidate("small-a", 20, 60000), Candidate("small-b", 20, 60000)], 50);
        Assert.Equal(["small-a", "small-b"], combined.SelectedLoot.Select(item => item.LootId).Order().ToArray());
        Assert.True(combined.BagUsagePercent <= 50);

        var exactWins = HeistPlanningAnalyzer.Optimize([
            Candidate("exact", 50, 200000), Candidate("estimated-a", 20, 60000), Candidate("estimated-b", 20, 60000)], 50);
        Assert.Equal("exact", Assert.Single(exactWins.SelectedLoot).LootId);
    }

    [Fact]
    public void CargoDrivesAlphaMailOnlyWhenSelected()
    {
        var cargoSelected = Analyze(1, Loot("cargo", LootType.LoadingBayCargo), State("cargo"));
        Assert.Equal(InfiltrationEntry.AlphaMail, cargoSelected.EntryRecommendation.Entry);

        var values = new List<object>();
        for (var index = 0; index < 10; index++)
        {
            var id = $"buyer-{index}";
            values.Add(Loot(id, LootType.CoquardJewelry)); values.Add(State(id, buyer: true));
        }
        values.Add(Loot("cargo", LootType.LoadingBayCargo)); values.Add(State("cargo"));
        var cargoExcluded = Analyze(1, values.ToArray());
        Assert.DoesNotContain(cargoExcluded.RecommendedHaul.SelectedLoot, item => item.LootId == "cargo");
        Assert.Equal(InfiltrationEntry.Skylight, cargoExcluded.EntryRecommendation.Entry);
    }

    [Fact]
    public void FullVaultBuyerHaulExcludesCargoAndRecommendsSewer()
    {
        var analysis = Analyze(1,
            Loot("vault-a", LootType.Painting, mapId: "basement"), State("vault-a", value: 100000, buyer: true),
            Loot("vault-b", LootType.Painting, mapId: "basement"), State("vault-b", value: 100000, buyer: true),
            Loot("cargo", LootType.LoadingBayCargo, mapId: "basement"), State("cargo"));
        Assert.Equal(100, analysis.RecommendedHaul.BagUsagePercent);
        Assert.Equal(["vault-a", "vault-b"], analysis.RecommendedHaul.SelectedLoot.Select(item => item.LootId).Order().ToArray());
        Assert.Equal(InfiltrationEntry.Sewer, analysis.EntryRecommendation.Entry);
        Assert.Equal(EntryRecommendationReason.VaultOnlyHaul, analysis.EntryRecommendation.Reason);
    }

    [Fact]
    public void CargoSelectedRecommendsAlphaMail()
    {
        var analysis = Analyze(1, Loot("cargo", LootType.LoadingBayCargo, mapId: "basement"), State("cargo"));
        Assert.Contains(analysis.RecommendedHaul.SelectedLoot, item => item.LootId == "cargo");
        Assert.Equal(InfiltrationEntry.AlphaMail, analysis.EntryRecommendation.Entry);
    }

    [Fact]
    public void ExcludedCargoWithUpperFloorHaulFallsBackToSkylight()
    {
        var analysis = Analyze(1,
            Loot("upper-a", LootType.Painting, mapId: "upper-floor"), State("upper-a", value: 100000, buyer: true),
            Loot("upper-b", LootType.Painting, mapId: "upper-floor"), State("upper-b", value: 100000, buyer: true),
            Loot("cargo", LootType.LoadingBayCargo, mapId: "basement"), State("cargo"));
        Assert.DoesNotContain(analysis.RecommendedHaul.SelectedLoot, item => item.LootId == "cargo");
        Assert.Equal(InfiltrationEntry.Skylight, analysis.EntryRecommendation.Entry);
    }

    [Fact]
    public void VaultOnlyHaulWithSpareCapacityStillRecommendsSewer()
    {
        var analysis = Analyze(1, Loot("vault", LootType.Painting, mapId: "lower-floor"), State("vault", value: 100000));
        Assert.True(analysis.RecommendedHaul.RemainingCapacityPercent > 0);
        Assert.Equal(InfiltrationEntry.Sewer, analysis.EntryRecommendation.Entry);
        Assert.Equal(["Basement / Vault"], analysis.EntryRecommendation.RequiredAreas);
    }

    [Fact]
    public void MixedBasementAndUpperFloorBuyerHaulUsesSkylight()
    {
        var analysis = Analyze(1,
            Loot("vault", LootType.Painting, mapId: "basement"), State("vault", value: 100000, buyer: true),
            Loot("upper", LootType.Painting, mapId: "upper-floor"), State("upper", value: 100000, buyer: true));
        Assert.Equal(100, analysis.RecommendedHaul.BagUsagePercent);
        Assert.Equal(InfiltrationEntry.Skylight, analysis.EntryRecommendation.Entry);
        Assert.Equal(EntryRecommendationReason.DefaultSkylightPreference, analysis.EntryRecommendation.Reason);
    }

    [Fact]
    public void RecommendationDoesNotMutateOptimizedHaul()
    {
        var haul = HeistPlanningAnalyzer.Optimize([Candidate("vault", 50, 100000) with { MapId = "basement" }], 100);
        var before = haul.SelectedLoot.ToArray();
        _ = EntryRecommendationService.Recommend(haul);
        Assert.Equal(before, haul.SelectedLoot);
    }

    [Fact]
    public void PowerDrillsUsesSpareCapacityAndPreservesBoxUncertainty()
    {
        var spare = Analyze(1,
            Loot("box-a", LootType.SafetyDepositBoxes), State("box-a"),
            Loot("box-b", LootType.SafetyDepositBoxes), State("box-b"));
        Assert.Equal(PrepRecommendationLevel.Recommended, spare.PowerDrills.Recommendation);
        Assert.Equal(10000, spare.PowerDrills.EstimatedMinValue);
        Assert.Equal(24000, spare.PowerDrills.EstimatedMaxValue);
        Assert.Equal(2, spare.PowerDrillsHaul.SelectedLoot.Count);

        var full = Analyze(1,
            Loot("painting-a", LootType.Painting), State("painting-a", value: 118000),
            Loot("painting-b", LootType.Painting), State("painting-b", value: 118000),
            Loot("box", LootType.SafetyDepositBoxes), State("box"));
        Assert.Equal(PrepRecommendationLevel.NotRecommended, full.PowerDrills.Recommendation);
    }

    [Fact]
    public void UnselectedScopedGlassDoesNotRecommendCutterForFullBuyerHaul()
    {
        var analysis = Analyze(1,
            Loot("buyer-a", LootType.Painting, mapId: "basement"), State("buyer-a", value: 110000, buyer: true),
            Loot("buyer-b", LootType.Painting, mapId: "basement"), State("buyer-b", value: 110000, buyer: true),
            Loot("glass", LootType.VerticalDisplayGlassCase, mapId: "upper-floor"), State("glass", value: 95000));

        Assert.DoesNotContain(analysis.RecommendedHaul.SelectedLoot, item => item.LootId == "glass");
        Assert.Equal(PrepRecommendationLevel.NotRecommended, analysis.GlassCutter.Recommendation);
        Assert.Equal(0, analysis.GlassCutter.TargetCount);
        Assert.Contains("recommended haul", analysis.GlassCutter.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MorePlayersCanBringGlassCaseIntoRecommendedHaul()
    {
        object[] loot =
        [
            Loot("buyer-a", LootType.Painting), State("buyer-a", value: 110000, buyer: true),
            Loot("buyer-b", LootType.Painting), State("buyer-b", value: 110000, buyer: true),
            Loot("glass", LootType.VerticalDisplayGlassCase), State("glass", value: 95000),
        ];
        var solo = Analyze(1, loot);
        var duo = Analyze(2, loot);

        Assert.Equal(PrepRecommendationLevel.NotRecommended, solo.GlassCutter.Recommendation);
        Assert.Equal(PrepRecommendationLevel.Required, duo.GlassCutter.Recommendation);
        Assert.Equal(1, duo.GlassCutter.TargetCount);
        Assert.Equal(30, duo.GlassCutter.BagPercent);
    }

    [Fact]
    public void BetterSameCapacityCombinationCanExcludeGlassCase()
    {
        var values = new List<object>();
        for (var index = 0; index < 10; index++)
        {
            var id = $"rings-{index}";
            values.Add(Loot(id, LootType.CoquardJewelry));
            values.Add(State(id, value: 40000));
        }
        values.Add(Loot("glass", LootType.VerticalDisplayGlassCase));
        values.Add(State("glass", value: 95000));

        var analysis = Analyze(1, values.ToArray());
        Assert.DoesNotContain(analysis.RecommendedHaul.SelectedLoot, item => item.LootId == "glass");
        Assert.Equal(PrepRecommendationLevel.NotRecommended, analysis.GlassCutter.Recommendation);
    }

    [Fact]
    public void MultipleSelectedGlassCasesDriveOnlySelectedPrepTotals()
    {
        var analysis = Analyze(2,
            Loot("glass-a", LootType.VerticalDisplayGlassCase), State("glass-a", value: 90000),
            Loot("glass-b", LootType.VerticalDisplayGlassCase), State("glass-b", value: 95000));

        Assert.Equal(PrepRecommendationLevel.Required, analysis.GlassCutter.Recommendation);
        Assert.Equal(2, analysis.GlassCutter.TargetCount);
        Assert.Equal(60, analysis.GlassCutter.BagPercent);
        Assert.Equal(185000, analysis.GlassCutter.KnownExactValue);
        Assert.Equal(0, analysis.GlassCutter.EstimatedMinValue);
        Assert.Equal(0, analysis.GlassCutter.EstimatedMaxValue);
    }

    [Fact]
    public void AnalysisDoesNotMutateRunState()
    {
        var definition = Loot("painting", LootType.Painting);
        var state = State("painting", value: 118000, buyer: true);
        _ = HeistPlanningAnalyzer.Analyze([definition], [state], 1);
        Assert.True(state.IsPresent);
        Assert.True(state.IsBuyersRequest);
        Assert.False(state.IsLooted);
        Assert.Equal(118000, state.ScopedValue);
    }

    private static PlanningAnalysis Analyze(int players, params object[] values)
    {
        var definitions = values.OfType<LootSpawnDefinition>();
        var states = values.OfType<LootSpawnState>();
        return HeistPlanningAnalyzer.Analyze(definitions, states, players);
    }
    private static LootSpawnDefinition Loot(string id, LootType type, string? zone = null, string mapId = "main-floor") => new(id, mapId, id, type, .5, .5, zone);
    private static LootSpawnState State(string id, int? value = null, bool buyer = false) => new() { SpawnId = id, IsPresent = true, ScopedValue = value, IsBuyersRequest = buyer };
    private static PlanningLootCandidate Candidate(string id, int bag, int exact, bool buyer = false) =>
        new(id, id, "main-floor", LootType.Painting, bag, true, 1, buyer, exact, exact, exact, OptionalPrep.None);
}
