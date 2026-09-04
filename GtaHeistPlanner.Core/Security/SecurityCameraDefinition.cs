namespace GtaHeistPlanner.Core.Security;

public sealed record SecurityCameraDefinition(
    string Id,
    string MapId,
    string Name,
    double X,
    double Y,
    double RotationDegrees,
    double FovDegrees,
    double Range);
