namespace GtaHeistPlanner.Core.Maps;

public static class MapCalibrationFitter
{
    public static MapCalibration Fit(IEnumerable<MapPoint> worldPoints, double paddingFraction = 0.1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(paddingFraction, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(paddingFraction, 0.5);

        var points = worldPoints.ToArray();
        if (points.Length == 0)
            throw new ArgumentException("At least one point is required to fit a calibration.", nameof(worldPoints));

        var minX = points.Min(point => point.X);
        var maxX = points.Max(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxY = points.Max(point => point.Y);
        var rangeX = maxX - minX;
        var rangeY = maxY - minY;
        var usableSize = 1 - paddingFraction * 2;
        var scale = Math.Min(
            rangeX > 0 ? usableSize / rangeX : double.PositiveInfinity,
            rangeY > 0 ? usableSize / rangeY : double.PositiveInfinity);

        if (!double.IsFinite(scale))
            scale = 1;

        return new MapCalibration
        {
            SourceOriginX = (minX + maxX) / 2,
            SourceOriginY = (minY + maxY) / 2,
            ScaleX = scale,
            ScaleY = scale,
            OffsetX = 0.5,
            OffsetY = 0.5,
            FlipY = true,
        };
    }
}
