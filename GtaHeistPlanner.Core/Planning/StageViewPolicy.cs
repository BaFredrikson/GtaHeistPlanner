namespace GtaHeistPlanner.Core.Planning;

public sealed record StageViewPolicy(
    PlannerStage Stage,
    IReadOnlySet<string> AllowedMapIds,
    LootVisibilityMode LootVisibility,
    bool ShowEntryPoints,
    bool ShowExteriorGuards,
    bool ShowInteriorGuards,
    bool ShowExteriorCameras,
    bool ShowInteriorCameras)
{
    public bool AllowsMap(string mapId) => AllowedMapIds.Contains(mapId);

    public bool AllowsOverlay(OverlayType type, string mapId) => AllowsMap(mapId) && type switch
    {
        OverlayType.Loot => LootVisibility != LootVisibilityMode.Hidden,
        OverlayType.EntryPoints => ShowEntryPoints,
        OverlayType.ExteriorGuards => ShowExteriorGuards,
        OverlayType.InteriorGuards => ShowInteriorGuards,
        OverlayType.ExteriorCameras => ShowExteriorCameras,
        OverlayType.InteriorCameras => ShowInteriorCameras,
        _ => false,
    };
}
