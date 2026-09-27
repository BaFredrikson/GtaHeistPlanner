using GtaHeistPlanner.Core.Loot;
using GtaHeistPlanner.Core.Planning;

namespace GtaHeistPlanner.Tests.Planning;

public sealed class LootPlanningSummaryTests
{
    [Fact]
    public void CalculatesDeterministicScopedLootSummary()
    {
        var definitions = new[]
        {
            new LootSpawnDefinition("jewels", "main-floor", "Jewels", LootType.CoquardJewelry, .1, .1),
            new LootSpawnDefinition("glass", "upper-floor", "Glass", LootType.VerticalDisplayGlassCase, .2, .2, LootZoneCatalog.CrispGalleryId),
            new LootSpawnDefinition("drills", "basement", "Boxes", LootType.SafetyDepositBoxes, .3, .3),
        };
        var states = new[]
        {
            new LootSpawnState { SpawnId = "jewels", IsPresent = true, IsBuyersRequest = true },
            new LootSpawnState { SpawnId = "glass", IsPresent = true },
            new LootSpawnState { SpawnId = "drills", IsPresent = false },
        };

        var summary = LootPlanningSummaryCalculator.Calculate(definitions, states, 1);

        Assert.Equal(100, summary.BagCapacityPercent);
        Assert.Equal(3, summary.KnownLocationCount);
        Assert.Equal(2, summary.PresentCount);
        Assert.Equal(1, summary.BuyersRequestCount);
        Assert.Equal(128_000, summary.EstimatedMinValue);
        Assert.Equal(162_000, summary.EstimatedMaxValue);
        Assert.Equal(1, summary.InaccessibleCount);
        Assert.Equal(1, summary.GlassCutterCount);
        Assert.Equal(0, summary.PowerDrillsCount);
    }

    [Fact]
    public void SummaryGroupsCrispPaintingWithNormalPainting()
    {
        var definitions = new[]
        {
            new LootSpawnDefinition("normal", "main-floor", "Painting", LootType.Painting, .1, .1),
            new LootSpawnDefinition("crisp", "upper-floor", "Crisp Gallery Painting", LootType.Painting,
                .2, .2, LootZoneCatalog.CrispGalleryId),
        };
        var states = definitions.Select(definition => new LootSpawnState
        {
            SpawnId = definition.Id,
            IsPresent = true,
        });

        var summary = LootPlanningSummaryCalculator.Calculate(definitions, states, 2);

        var category = Assert.Single(summary.Categories);
        Assert.Equal(LootPlanningCategory.Painting, category.Category);
        Assert.Equal("Painting", category.Name);
        Assert.Equal(2, category.Count);
        Assert.Equal(242_000, category.MinValue);
        Assert.Equal(284_000, category.MaxValue);
    }
}
