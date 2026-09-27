namespace GtaHeistPlanner.Core.Loot;

/// <summary>
/// Stable economic/gameplay grouping used by planning summaries. This deliberately
/// excludes authored item names and location/access modifiers such as Crisp Gallery.
/// </summary>
public enum LootPlanningCategory
{
    RingsJewelry,
    PremiumCase,
    VerticalDisplayCase,
    Painting,
    LoadingBayCargo,
    SafetyDepositBoxes,
}

public static class LootPlanningCategories
{
    public static LootPlanningCategory For(LootType type) => type switch
    {
        LootType.CoquardJewelry => LootPlanningCategory.RingsJewelry,
        LootType.FertilityStatue => LootPlanningCategory.PremiumCase,
        LootType.VerticalDisplayGlassCase => LootPlanningCategory.VerticalDisplayCase,
        LootType.Painting => LootPlanningCategory.Painting,
        LootType.LoadingBayCargo => LootPlanningCategory.LoadingBayCargo,
        LootType.SafetyDepositBoxes => LootPlanningCategory.SafetyDepositBoxes,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown loot type."),
    };

    public static string DisplayName(this LootPlanningCategory category) => category switch
    {
        LootPlanningCategory.RingsJewelry => "Rings / Jewelry",
        LootPlanningCategory.PremiumCase => "Premium Case",
        LootPlanningCategory.VerticalDisplayCase => "Vertical Display Case",
        LootPlanningCategory.Painting => "Painting",
        LootPlanningCategory.LoadingBayCargo => "Loading Bay Cargo",
        LootPlanningCategory.SafetyDepositBoxes => "Safety Deposit Boxes",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown planning category."),
    };
}
