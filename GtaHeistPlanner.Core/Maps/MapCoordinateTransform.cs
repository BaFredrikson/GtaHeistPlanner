namespace GtaHeistPlanner.Core.Maps;

public static class MapCoordinateTransform
{
    public static MapPoint Transform(MapPoint worldPoint, MapCalibration calibration)
    {
        var localX = worldPoint.X - calibration.SourceOriginX;
        var localY = worldPoint.Y - calibration.SourceOriginY;

        if (calibration.FlipX)
            localX = -localX;
        if (calibration.FlipY)
            localY = -localY;

        var scaledX = localX * calibration.ScaleX;
        var scaledY = localY * calibration.ScaleY;
        var radians = calibration.RotationDegrees * Math.PI / 180.0;
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);

        return new MapPoint(
            scaledX * cosine - scaledY * sine + calibration.OffsetX,
            scaledX * sine + scaledY * cosine + calibration.OffsetY);
    }
}
