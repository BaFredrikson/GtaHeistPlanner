using GtaHeistPlanner.Core.Maps;

namespace GtaHeistPlanner.Core.Overlays;

public static class CameraFacingOrientation
{
    public static double DeriveRotationDegrees(
        MapPoint transformedOrigin,
        MapPoint transformedLookPoint,
        double baseRotationOffsetDegrees)
    {
        var deltaX = transformedLookPoint.X - transformedOrigin.X;
        var deltaY = transformedLookPoint.Y - transformedOrigin.Y;
        if (Math.Abs(deltaX) < double.Epsilon && Math.Abs(deltaY) < double.Epsilon)
            throw new ArgumentException("Camera origin and transformed look point must not coincide.");

        var facingDegrees = Math.Atan2(deltaY, deltaX) * 180 / Math.PI;
        return NormalizeDegrees(facingDegrees + baseRotationOffsetDegrees);
    }

    private static double NormalizeDegrees(double degrees)
    {
        degrees %= 360;
        if (degrees <= -180)
            degrees += 360;
        else if (degrees > 180)
            degrees -= 360;
        return degrees;
    }
}
