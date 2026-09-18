namespace GtaHeistPlanner.Core.Planning;

public static class DeveloperViewPolicy
{
    public static bool CanAuthorLoot(bool developerMode, StageViewPolicy policy, string mapId) =>
        developerMode && policy.AllowsOverlay(OverlayType.Loot, mapId);

    public static bool CanAuthorSecurity(bool developerMode, PlannerStage stage) =>
        developerMode && stage is PlannerStage.HeistInfiltration or PlannerStage.HeistActivity;

    public static bool CanAuthorInteractionMarkers(bool developerMode, StageViewPolicy policy, string mapId) =>
        developerMode && policy.AllowsOverlay(OverlayType.InteractionMarkers, mapId);
}
