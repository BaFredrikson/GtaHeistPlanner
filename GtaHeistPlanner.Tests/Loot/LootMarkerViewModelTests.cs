using GtaHeistPlanner.App.ViewModels;
using GtaHeistPlanner.Core.Loot;

namespace GtaHeistPlanner.Tests.Loot;

public sealed class LootMarkerViewModelTests
{
    [Fact]
    public void EnteringExactScopedValueAlsoMarksLootPresent()
    {
        var definition = new LootSpawnDefinition("loot", "main-floor", "Painting", LootType.Painting, .2, .3);
        var state = new LootSpawnState { SpawnId = definition.Id };
        var marker = new LootMarkerViewModel(definition, state);

        marker.ScopedValue = 118000;

        Assert.True(marker.IsPresent);
        Assert.True(state.IsPresent);
        Assert.Equal(118000, state.ScopedValue);
    }
}
