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
}
