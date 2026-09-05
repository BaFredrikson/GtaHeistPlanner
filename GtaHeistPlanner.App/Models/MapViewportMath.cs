using Avalonia;

namespace GtaHeistPlanner.App.Models;

public static class MapViewportMath
{
    public static double FixedMarkerScale(double zoom) => 1 / Math.Max(MapViewportState.MinimumZoom, zoom);

    public static Point ToViewport(Point contentPoint, double zoom, double panX, double panY) =>
        new(contentPoint.X * zoom + panX, contentPoint.Y * zoom + panY);

    public static Point ToContent(Point viewportPoint, double zoom, double panX, double panY)
    {
        var safeZoom = Math.Max(MapViewportState.MinimumZoom, zoom);
        return new((viewportPoint.X - panX) / safeZoom, (viewportPoint.Y - panY) / safeZoom);
    }
}
