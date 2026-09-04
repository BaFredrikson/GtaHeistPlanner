using GtaHeistPlanner.Core.Loot;

namespace GtaHeistPlanner.Tests.Loot;

public sealed class LootEconomicsCatalogTests
{
    public static TheoryData<LootType, int, int, int, OptionalPrep> StandardEconomics => new()
    {
        { LootType.CoquardJewelry, 10, 28_000, 35_000, OptionalPrep.None },
        { LootType.FertilityStatue, 20, 50_000, 64_000, OptionalPrep.None },
        { LootType.VerticalDisplayGlassCase, 30, 77_500, 100_000, OptionalPrep.GlassCutter },
        { LootType.Painting, 50, 102_000, 122_000, OptionalPrep.None },
        { LootType.LoadingBayCargo, 30, 105_000, 140_000, OptionalPrep.None },
        { LootType.SafetyDepositBoxes, 30, 5_000, 12_000, OptionalPrep.PowerDrills },
    };

    [Theory]
    [MemberData(nameof(StandardEconomics))]
    public void StandardTypesExposeExpectedEconomics(
        LootType type, int bagPercent, int minValue, int maxValue, OptionalPrep prep)
    {
        var economics = LootEconomicsCatalog.Get(type);

        Assert.Equal(bagPercent, economics.BagPercent);
        Assert.Equal(minValue, economics.MinValue);
        Assert.Equal(maxValue, economics.MaxValue);
        Assert.Equal(prep, economics.RequiredPrep);
    }

    [Theory]
    [InlineData(LootType.CoquardJewelry, 10, 42_000, 53_000)]
    [InlineData(LootType.FertilityStatue, 20, 70_000, 96_000)]
    [InlineData(LootType.VerticalDisplayGlassCase, 30, 100_000, 127_000)]
    [InlineData(LootType.Painting, 50, 140_000, 162_000)]
    public void CrispGalleryUsesPremiumRangeAndSameBagSize(
        LootType type, int bagPercent, int minValue, int maxValue)
    {
        var economics = LootEconomicsCatalog.Get(type, LootZoneCatalog.CrispGalleryId);

        Assert.Equal(bagPercent, economics.BagPercent);
        Assert.Equal(minValue, economics.MinValue);
        Assert.Equal(maxValue, economics.MaxValue);
        Assert.Equal(2, LootZoneCatalog.CrispGallery.MinimumPlayers);
    }
}
