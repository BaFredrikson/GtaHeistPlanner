using GtaHeistPlanner.Core.Maps;

namespace GtaHeistPlanner.Core.Security;

public sealed record NormalizedVisionArea(MapPoint Origin, MapPoint Left, MapPoint Center, MapPoint Right);

public static class NormalizedCameraGeometry
{
    public static NormalizedVisionArea Create(SecurityCameraDefinition camera)
    {
        if (camera.FovDegrees is <= 0 or >= 180 || camera.Range <= 0)
            throw new ArgumentOutOfRangeException(nameof(camera));
        var origin = new MapPoint(camera.X, camera.Y);
        return new(origin,
            Endpoint(origin, camera.RotationDegrees - camera.FovDegrees / 2, camera.Range),
            Endpoint(origin, camera.RotationDegrees, camera.Range),
            Endpoint(origin, camera.RotationDegrees + camera.FovDegrees / 2, camera.Range));
    }

    private static MapPoint Endpoint(MapPoint origin, double degrees, double range)
    {
        var radians = degrees * Math.PI / 180;
        return new(origin.X + Math.Cos(radians) * range, origin.Y + Math.Sin(radians) * range);
    }
}
