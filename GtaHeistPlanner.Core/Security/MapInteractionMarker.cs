namespace GtaHeistPlanner.Core.Security;

public sealed record MapInteractionMarker(
    string Id,
    string MapId,
    MapInteractionMarkerType Type,
    double X,
    double Y,
    string? DisplayName = null);

