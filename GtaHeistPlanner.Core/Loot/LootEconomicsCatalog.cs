namespace GtaHeistPlanner.Core.Loot;

public static class LootEconomicsCatalog
{
    public static IReadOnlyList<LootEconomics> Standard { get; } =
    [
        new(LootType.CoquardJewelry, "Coquard Rings & Bracelets", 10, 28_000, 35_000, OptionalPrep.None),
        new(LootType.FertilityStatue, "Fertility Statue / Coquard Rings", 20, 50_000, 64_000, OptionalPrep.None),
        new(LootType.VerticalDisplayGlassCase, "Vertical Display Glass Case", 30, 77_500, 100_000, OptionalPrep.GlassCutter),
        new(LootType.Painting, "Painting", 50, 102_000, 122_000, OptionalPrep.None),
        new(LootType.LoadingBayCargo, "Loading Bay Cargo", 30, 105_000, 140_000, OptionalPrep.None),
        new(LootType.SafetyDepositBoxes, "Safety Deposit Boxes", 30, 5_000, 12_000, OptionalPrep.PowerDrills),
    ];

    private static readonly IReadOnlyDictionary<LootType, LootEconomics> CrispGallery =
        new Dictionary<LootType, LootEconomics>
        {
            [LootType.CoquardJewelry] = new(LootType.CoquardJewelry, "Coquard Rings", 10, 42_000, 53_000, OptionalPrep.None),
            [LootType.FertilityStatue] = new(LootType.FertilityStatue, "Fertility Statue / Premium Case", 20, 70_000, 96_000, OptionalPrep.None),
            [LootType.VerticalDisplayGlassCase] = new(LootType.VerticalDisplayGlassCase, "Vertical Display / Premium Case", 30, 100_000, 127_000, OptionalPrep.GlassCutter),
            [LootType.Painting] = new(LootType.Painting, "Crisp Gallery Painting", 50, 140_000, 162_000, OptionalPrep.None),
        };

    public static LootEconomics Get(LootType type, string? zoneId = null)
    {
        if (zoneId == LootZoneCatalog.CrispGalleryId && CrispGallery.TryGetValue(type, out var premium))
            return premium;
        return Standard.Single(item => item.Type == type);
    }
}
