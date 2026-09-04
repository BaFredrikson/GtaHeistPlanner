using GtaHeistPlanner.Core.Loot;

namespace GtaHeistPlanner.Tests.Loot;

public sealed class LootRunStateTests
{
    [Fact]
    public void FourthBuyersRequestMarkerIsRejected()
    {
        var run = new LootRunState(Definitions(4));
        Assert.True(run.TrySetBuyersRequest("loot-1", true, out _));
        Assert.True(run.TrySetBuyersRequest("loot-2", true, out _));
        Assert.True(run.TrySetBuyersRequest("loot-3", true, out _));

        Assert.False(run.TrySetBuyersRequest("loot-4", true, out var error));
        Assert.Equal(3, run.BuyersRequestCount);
        Assert.Contains("limited to 3", error);
    }

    [Fact]
    public void ResetClearsRuntimeStateWithoutChangingDefinitions()
    {
        var definition = Definitions(1)[0];
        var run = new LootRunState([definition]);
        run.SetLootPresent(definition.Id, true);
        run.SetLooted(definition.Id, true);
        run.TrySetBuyersRequest(definition.Id, true, out _);

        run.ResetLootState();

        Assert.Equal(definition, Assert.Single(run.Definitions));
        var state = Assert.Single(run.States);
        Assert.False(state.IsPresent);
        Assert.False(state.IsLooted);
        Assert.False(state.IsBuyersRequest);
    }

    [Fact]
    public void RuntimeStateTransitionsAreAppliedBySpawnId()
    {
        var run = new LootRunState(Definitions(1));

        run.SetLootPresent("loot-1", true);
        run.SetLooted("loot-1", true);
        Assert.True(run.TrySetBuyersRequest("loot-1", true, out var error));

        var state = run.GetState("loot-1");
        Assert.True(state.IsPresent);
        Assert.True(state.IsLooted);
        Assert.True(state.IsBuyersRequest);
        Assert.Null(error);
    }

    private static List<LootSpawnDefinition> Definitions(int count) =>
        Enumerable.Range(1, count)
            .Select(index => new LootSpawnDefinition($"loot-{index}", "main-floor", $"Loot {index}", LootType.CoquardJewelry, .5, .5))
            .ToList();
}
