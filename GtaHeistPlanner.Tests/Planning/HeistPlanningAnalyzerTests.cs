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
    private static LootSpawnDefinition Loot(string id, LootType type, string? zone = null) => new(id, "main-floor", id, type, .5, .5, zone);
    private static LootSpawnState State(string id, int? value = null, bool buyer = false) => new() { SpawnId = id, IsPresent = true, ScopedValue = value, IsBuyersRequest = buyer };
    private static PlanningLootCandidate Candidate(string id, int bag, int exact, bool buyer = false) =>
        new(id, id, "main-floor", LootType.Painting, bag, true, 1, buyer, exact, exact, exact, OptionalPrep.None);
}
