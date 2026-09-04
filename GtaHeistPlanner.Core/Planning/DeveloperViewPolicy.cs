namespace GtaHeistPlanner.Core.Planning;

public static class DeveloperViewPolicy
{
    public static bool CanAuthorLoot(bool developerMode, StageViewPolicy policy, string mapId) =>
        developerMode && policy.AllowsOverlay(OverlayType.Loot, mapId);
}
