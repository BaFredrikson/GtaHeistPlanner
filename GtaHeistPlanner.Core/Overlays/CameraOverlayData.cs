namespace GtaHeistPlanner.Core.Overlays;

public sealed record CameraOverlayData(
    string MapId,
    IReadOnlyList<CameraOverlayDefinition> Cameras);

public sealed record CameraOverlayDefinition(
    string Id,
    string MapId,
    WorldPosition Position,
    double HeadingDegrees,
    double FovDegrees,
    double Range,
    string? Label = null,
    string? Notes = null);

public sealed record WorldPosition(double X, double Y, double Z);
