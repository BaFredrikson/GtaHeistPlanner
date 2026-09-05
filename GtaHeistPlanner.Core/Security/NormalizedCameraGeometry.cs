using GtaHeistPlanner.Core.Maps;

namespace GtaHeistPlanner.Core.Security;

public sealed record NormalizedVisionSector(
    MapPoint Origin,
    MapPoint NearLeft,
    IReadOnlyList<MapPoint> FarArc,
    MapPoint NearRight,
    MapPoint CenterLineEnd,
    double NearRadius);

public static class NormalizedCameraGeometry
{
    public const int DefaultArcSegments = 18;
    public const double NearRadiusRatio = 0.03;

    public static NormalizedVisionSector Create(
        SecurityCameraDefinition camera,
        double mapAspectRatio = 1,
        int arcSegments = DefaultArcSegments)
    {
        if (camera.FovDegrees is <= 0 or >= 180 || camera.Range <= 0)
            throw new ArgumentOutOfRangeException(nameof(camera));
        if (!double.IsFinite(mapAspectRatio) || mapAspectRatio <= 0)
            throw new ArgumentOutOfRangeException(nameof(mapAspectRatio));
        if (arcSegments < 2)
            throw new ArgumentOutOfRangeException(nameof(arcSegments));

        var origin = new MapPoint(camera.X, camera.Y);
        var startAngle = camera.RotationDegrees - camera.FovDegrees / 2;
        var endAngle = camera.RotationDegrees + camera.FovDegrees / 2;
        var nearRadius = camera.Range * NearRadiusRatio;
        var arc = new List<MapPoint>(arcSegments + 1);
        for (var index = 0; index <= arcSegments; index++)
        {
            var angle = startAngle + (endAngle - startAngle) * index / arcSegments;
            arc.Add(Endpoint(origin, angle, camera.Range, mapAspectRatio));
        }

        return new(origin,
            Endpoint(origin, startAngle, nearRadius, mapAspectRatio),
            arc,
            Endpoint(origin, endAngle, nearRadius, mapAspectRatio),
            Endpoint(origin, camera.RotationDegrees, camera.Range, mapAspectRatio),
            nearRadius);
    }

    private static MapPoint Endpoint(MapPoint origin, double degrees, double range, double mapAspectRatio)
    {
        var radians = degrees * Math.PI / 180;
        // Range is expressed relative to map height. Compensating normalized X by
        // the fitted map's width/height ratio keeps angles and radii circular on screen.
        return new(
            origin.X + Math.Cos(radians) * range / mapAspectRatio,
            origin.Y + Math.Sin(radians) * range);
    }
}
