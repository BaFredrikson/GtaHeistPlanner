using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GtaHeistPlanner.Core.Sewer;

namespace GtaHeistPlanner.App.Controls;

public sealed class SewerOverlayControl : Control
{
    private static readonly Pen PathPen = new(new SolidColorBrush(Color.Parse("#66788A9A")), 2);
    private static readonly Pen RoutePen = new(new SolidColorBrush(Color.Parse("#F3C969")), 5);
    private static readonly IBrush RouteMarkerBrush = new SolidColorBrush(Color.Parse("#F3C969"));

    public static readonly StyledProperty<IEnumerable<SewerPath>?> PathsProperty =
        AvaloniaProperty.Register<SewerOverlayControl, IEnumerable<SewerPath>?>(nameof(Paths));
    public static readonly StyledProperty<IEnumerable<string>?> HighlightedPathIdsProperty =
        AvaloniaProperty.Register<SewerOverlayControl, IEnumerable<string>?>(nameof(HighlightedPathIds));
    public static readonly StyledProperty<double> MapAspectRatioProperty =
        AvaloniaProperty.Register<SewerOverlayControl, double>(nameof(MapAspectRatio), 1);
    public static readonly StyledProperty<int> RevisionProperty =
        AvaloniaProperty.Register<SewerOverlayControl, int>(nameof(Revision));
    public static readonly StyledProperty<double> ViewportZoomProperty =
        AvaloniaProperty.Register<SewerOverlayControl, double>(nameof(ViewportZoom), 1);
    public static readonly StyledProperty<bool> IsRouteCompleteProperty =
        AvaloniaProperty.Register<SewerOverlayControl, bool>(nameof(IsRouteComplete));

    static SewerOverlayControl() => AffectsRender<SewerOverlayControl>(
        PathsProperty, HighlightedPathIdsProperty, MapAspectRatioProperty, RevisionProperty,
        ViewportZoomProperty, IsRouteCompleteProperty);

    public IEnumerable<SewerPath>? Paths { get => GetValue(PathsProperty); set => SetValue(PathsProperty, value); }
    public IEnumerable<string>? HighlightedPathIds { get => GetValue(HighlightedPathIdsProperty); set => SetValue(HighlightedPathIdsProperty, value); }
    public double MapAspectRatio { get => GetValue(MapAspectRatioProperty); set => SetValue(MapAspectRatioProperty, value); }
    public int Revision { get => GetValue(RevisionProperty); set => SetValue(RevisionProperty, value); }
    public double ViewportZoom { get => GetValue(ViewportZoomProperty); set => SetValue(ViewportZoomProperty, value); }
    public bool IsRouteComplete { get => GetValue(IsRouteCompleteProperty); set => SetValue(IsRouteCompleteProperty, value); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var pathList = Paths?.ToList() ?? [];
        var routeIds = HighlightedPathIds?.ToList() ?? [];
        var highlighted = routeIds.ToHashSet(StringComparer.Ordinal);
        var rect = GetMapRect();
        foreach (var path in pathList) DrawPath(context, path, rect, highlighted.Contains(path.Id) ? RoutePen : PathPen);
        if (routeIds.Count == 0) return;

        var firstPath = pathList.FirstOrDefault(path => path.Id == routeIds[0]);
        if (firstPath is not null)
            context.DrawEllipse(RouteMarkerBrush, null, ToScreen(firstPath.Points[0], rect),
                4 / Math.Max(1, ViewportZoom), 4 / Math.Max(1, ViewportZoom));

        var finalPath = IsRouteComplete ? pathList.FirstOrDefault(path => path.Id == routeIds[^1]) : null;
        if (finalPath is not null) DrawArrowhead(context, finalPath, rect);
    }

    private static void DrawPath(DrawingContext context, SewerPath path, Rect rect, Pen pen)
    {
        for (var index = 1; index < path.Points.Count; index++)
            context.DrawLine(pen, ToScreen(path.Points[index - 1], rect), ToScreen(path.Points[index], rect));
    }

    private void DrawArrowhead(DrawingContext context, SewerPath path, Rect rect)
    {
        var tip = ToScreen(path.Points[^1], rect);
        var previous = ToScreen(path.Points[^2], rect);
        var delta = tip - previous;
        var length = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
        if (length <= double.Epsilon) return;
        var forward = delta / length;
        var right = new Vector(-forward.Y, forward.X);
        var scale = 1 / Math.Max(1, ViewportZoom);
        var back = tip - forward * (11 * scale);
        var geometry = new StreamGeometry();
        using (var drawing = geometry.Open())
        {
            drawing.BeginFigure(tip, true);
            drawing.LineTo(back + right * (5 * scale));
            drawing.LineTo(back - right * (5 * scale));
            drawing.EndFigure(true);
        }
        context.DrawGeometry(RouteMarkerBrush, null, geometry);
    }

    private Rect GetMapRect()
    {
        var width = Math.Min(Bounds.Width, Bounds.Height * MapAspectRatio);
        var height = width / MapAspectRatio;
        return new((Bounds.Width - width) / 2, (Bounds.Height - height) / 2, width, height);
    }

    private static Point ToScreen(GtaHeistPlanner.Core.Maps.MapPoint point, Rect rect) =>
        new(rect.X + point.X * rect.Width, rect.Y + point.Y * rect.Height);
}
