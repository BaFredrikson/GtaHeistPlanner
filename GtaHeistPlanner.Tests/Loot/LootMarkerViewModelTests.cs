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

    [Fact]
    public void LabelUsesPlayerFacingNameRatherThanInternalId()
    {
        var definition = new LootSpawnDefinition("loot-main-07", "main-floor", "West Painting", LootType.Painting, .2, .3);
        var marker = new LootMarkerViewModel(definition, new LootSpawnState { SpawnId = definition.Id });

        Assert.Equal("West Painting", marker.LabelText);
        Assert.DoesNotContain(marker.Id, marker.LabelText, StringComparison.Ordinal);
    }
}
