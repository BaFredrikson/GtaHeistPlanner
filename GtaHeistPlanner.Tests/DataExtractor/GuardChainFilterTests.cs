using GtaHeistPlanner.DataExtractor;

namespace GtaHeistPlanner.Tests.DataExtractor;

public sealed class GuardChainFilterTests
{
    [Fact]
    public void ContainsTargetScenario_MatchesAnyNodeInChain()
    {
        GraphNode[] nodes =
        [
            new(0, new Coordinate(0, 0, 0), "walk", false, true),
            new(1, new Coordinate(1, 0, 0), "world_human_guard_patrol", true, false),
        ];

        var matches = ScenarioRegionExtractor.ContainsTargetScenario(
            [0, 1], nodes, ScenarioRegionExtractor.GuardScenarioTypes);

        Assert.True(matches);
    }

    [Fact]
    public void ContainsTargetScenario_RejectsUnrelatedChain()
    {
        GraphNode[] nodes =
        [
            new(0, new Coordinate(0, 0, 0), "walk", false, true),
            new(1, new Coordinate(1, 0, 0), "world_human_smoking", true, false),
        ];

        var matches = ScenarioRegionExtractor.ContainsTargetScenario(
            [0, 1], nodes, ScenarioRegionExtractor.GuardScenarioTypes);

        Assert.False(matches);
    }
}
