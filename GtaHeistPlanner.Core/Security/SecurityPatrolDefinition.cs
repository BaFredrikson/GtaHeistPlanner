namespace GtaHeistPlanner.Core.Security;

public sealed record SecurityPatrolDefinition(
    string Id,
    string MapId,
    string Name,
    IReadOnlyList<PatrolWaypoint> Waypoints);
