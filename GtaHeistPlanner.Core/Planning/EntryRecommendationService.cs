using GtaHeistPlanner.Core.Loot;

namespace GtaHeistPlanner.Core.Planning;

public enum LootArea { VaultBasement, MainFloor, UpperFloor, LoadingBay, Other }
public enum EntryRecommendationReason { LoadingBayCargoSelected, VaultOnlyHaul, DefaultSkylightPreference }

public static class LootAreaClassifier
{
    public static LootArea FromMapId(string mapId) => mapId switch
    {
        "basement" or "lower-floor" => LootArea.VaultBasement,
        "main-floor" => LootArea.MainFloor,
        "upper-floor" or "balcony" or "stairs" => LootArea.UpperFloor,
        _ => LootArea.Other,
    };

    public static string DisplayName(LootArea area) => area switch
    {
        LootArea.VaultBasement => "Basement / Vault",
        LootArea.MainFloor => "Main Floor",
        LootArea.UpperFloor => "Upper Floor",
        LootArea.LoadingBay => "Loading Bay",
        _ => "Other",
    };
}

public static class EntryRecommendationService
{
    public static EntryRecommendation Recommend(HaulRecommendation haul)
    {
        ArgumentNullException.ThrowIfNull(haul);
        var selected = haul.SelectedLoot;
        var areas = selected.Select(AreaOf).Distinct().ToArray();
        if (selected.Any(item => item.Type == LootType.LoadingBayCargo))
            return new(InfiltrationEntry.AlphaMail, EntryRecommendationReason.LoadingBayCargoSelected,
                ["Loading Bay Cargo is included in the recommended haul."],
                areas.Append(LootArea.LoadingBay).Distinct().Select(LootAreaClassifier.DisplayName).ToArray());
        if (selected.Count > 0 && areas.All(area => area == LootArea.VaultBasement))
            return new(InfiltrationEntry.Sewer, EntryRecommendationReason.VaultOnlyHaul,
                ["All recommended loot is in the vault/basement area; upper floors can be skipped."],
                areas.Select(LootAreaClassifier.DisplayName).ToArray());
        return new(InfiltrationEntry.Skylight, EntryRecommendationReason.DefaultSkylightPreference,
            ["No stronger route-specific advantage was found; using the preferred Skylight entry."],
            areas.Select(LootAreaClassifier.DisplayName).ToArray());
    }

    private static LootArea AreaOf(PlanningLootCandidate item) =>
        item.Type == LootType.LoadingBayCargo ? LootArea.LoadingBay : LootAreaClassifier.FromMapId(item.MapId);
}
