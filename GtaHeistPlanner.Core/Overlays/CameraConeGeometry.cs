using GtaHeistPlanner.Core.Maps;

namespace GtaHeistPlanner.Core.Overlays;

public sealed record CameraCone(
    MapPoint Origin,
    MapPoint LeftBoundary,
    MapPoint CenterLineEnd,
    MapPoint RightBoundary);

public static class CameraConeGeometry
{
    public static CameraCone CreateWorldSpace(CameraOverlayDefinition camera)
    {
        if (camera.FovDegrees is <= 0 or >= 180)
            throw new ArgumentOutOfRangeException(nameof(camera), "Camera FOV must be between 0 and 180 degrees.");
        if (camera.Range <= 0)
            throw new ArgumentOutOfRangeException(nameof(camera), "Camera range must be greater than zero.");

        var origin = new MapPoint(camera.Position.X, camera.Position.Y);
        return new CameraCone(
            origin,
            Endpoint(origin, camera.HeadingDegrees - camera.FovDegrees / 2, camera.Range),
            Endpoint(origin, camera.HeadingDegrees, camera.Range),
            Endpoint(origin, camera.HeadingDegrees + camera.FovDegrees / 2, camera.Range));
    }

    public static CameraCone Transform(CameraCone worldCone, MapCalibration calibration) => new(
        MapCoordinateTransform.Transform(worldCone.Origin, calibration),
        MapCoordinateTransform.Transform(worldCone.LeftBoundary, calibration),
        MapCoordinateTransform.Transform(worldCone.CenterLineEnd, calibration),
        MapCoordinateTransform.Transform(worldCone.RightBoundary, calibration));

    private static MapPoint Endpoint(MapPoint origin, double headingDegrees, double range)
    {
        var radians = headingDegrees * Math.PI / 180;
        return new MapPoint(
            origin.X + Math.Sin(radians) * range,
            origin.Y + Math.Cos(radians) * range);
    }
}
