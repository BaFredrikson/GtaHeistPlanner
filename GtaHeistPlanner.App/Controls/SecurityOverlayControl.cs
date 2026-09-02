using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using GtaHeistPlanner.App.Models;
using GtaHeistPlanner.Core.Maps;

namespace GtaHeistPlanner.App.Controls;

public sealed class SecurityOverlayControl : Control
{
    private const double MapWidth = 185.1;
    private const double MapHeight = 227.65;
    private static readonly IBrush RouteBrush = new SolidColorBrush(Color.Parse("#D8E6F3"));
    private static readonly IBrush NodeBrush = new SolidColorBrush(Color.Parse("#48C9E8"));
    private static readonly IBrush TorchBrush = new SolidColorBrush(Color.Parse("#FFD35A"));
    private static readonly IBrush PatrolBrush = new SolidColorBrush(Color.Parse("#FF6B57"));
    private static readonly IBrush SecurityBrush = new SolidColorBrush(Color.Parse("#B68CFF"));
    private static readonly Pen RoutePen = new(RouteBrush, 2);
    private static readonly Pen OutlinePen = new(new SolidColorBrush(Color.Parse("#11151B")), 1.5);

    public static readonly StyledProperty<SecurityAnalysis?> AnalysisProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, SecurityAnalysis?>(nameof(Analysis));

    public static readonly StyledProperty<MapCalibration?> CalibrationProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, MapCalibration?>(nameof(Calibration));

    public static readonly StyledProperty<string?> DiagnosticTextProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, string?>(nameof(DiagnosticText), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    static SecurityOverlayControl()
    {
        AffectsRender<SecurityOverlayControl>(AnalysisProperty, CalibrationProperty);
    }

    public SecurityAnalysis? Analysis
    {
        get => GetValue(AnalysisProperty);
        set => SetValue(AnalysisProperty, value);
    }

    public MapCalibration? Calibration
    {
        get => GetValue(CalibrationProperty);
        set => SetValue(CalibrationProperty, value);
    }

    public string? DiagnosticText
    {
        get => GetValue(DiagnosticTextProperty);
        set => SetValue(DiagnosticTextProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Analysis is null || Calibration is null)
            return;

        var mapRect = GetMapRect();
        foreach (var chain in Analysis.GuardChains.Where(chain => chain.ChainIndex == 7))
        {
            var nodes = chain.Nodes.ToDictionary(node => node.NodeIndex);
            foreach (var edge in chain.Edges)
            {
                if (!nodes.TryGetValue(edge.NodeIndexFrom, out var from) ||
                    !nodes.TryGetValue(edge.NodeIndexTo, out var to))
                    continue;
                context.DrawLine(RoutePen, ToScreen(from.Position, mapRect), ToScreen(to.Position, mapRect));
            }
        }

        foreach (var point in Analysis.SecurityScenarioPoints)
        {
            var center = ToScreen(point.Position, mapRect);
            if (IsPatrol(point.ScenarioType))
                context.DrawRectangle(PatrolBrush, OutlinePen, new Rect(center.X - 5, center.Y - 5, 10, 10));
            else
                context.DrawEllipse(SecurityBrush, OutlinePen, center, 3.5, 3.5);
        }

        foreach (var chain in Analysis.GuardChains.Where(chain => chain.ChainIndex == 7))
        {
            foreach (var node in chain.Nodes)
            {
                var center = ToScreen(node.Position, mapRect);
                var isTorch = node.ScenarioType.Equals("world_human_security_shine_torch", StringComparison.OrdinalIgnoreCase);
                context.DrawEllipse(isTorch ? TorchBrush : NodeBrush, OutlinePen, center,
                    isTorch ? 6 : 4.5, isTorch ? 6 : 4.5);
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Analysis is null || Calibration is null)
            return;

        var pointer = e.GetPosition(this);
        var mapRect = GetMapRect();
        var candidates = Analysis.SecurityScenarioPoints.Select(point => new MarkerInfo(
                $"Point {point.SourcePointIndex}", point.ScenarioType, point.Position))
            .Concat(Analysis.GuardChains.Where(chain => chain.ChainIndex == 7)
                .SelectMany(chain => chain.Nodes)
                .Select(node => new MarkerInfo($"Node {node.NodeIndex}", node.ScenarioType, node.Position)));

        var closest = candidates
            .Select(marker => new { Marker = marker, Screen = ToScreen(marker.Position, mapRect) })
            .Select(item => new { item.Marker, item.Screen, Distance = Distance(pointer, item.Screen) })
            .Where(item => item.Distance <= 12)
            .OrderBy(item => item.Distance)
            .FirstOrDefault();

        if (closest is null)
        {
            DiagnosticText = null;
            return;
        }

        var transformed = MapCoordinateTransform.Transform(
            new MapPoint(closest.Marker.Position.X, closest.Marker.Position.Y), Calibration);
        DiagnosticText = $"{closest.Marker.Id} · {closest.Marker.ScenarioType}\n" +
            $"GTA ({closest.Marker.Position.X:F3}, {closest.Marker.Position.Y:F3}) → " +
            $"planner ({transformed.X:F4}, {transformed.Y:F4})";
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        DiagnosticText = null;
    }

    private Point ToScreen(AnalysisCoordinate point, Rect mapRect)
    {
        var transformed = MapCoordinateTransform.Transform(new MapPoint(point.X, point.Y), Calibration!);
        return new Point(mapRect.X + transformed.X * mapRect.Width, mapRect.Y + transformed.Y * mapRect.Height);
    }

    private Rect GetMapRect()
    {
        var scale = Math.Min(Bounds.Width / MapWidth, Bounds.Height / MapHeight);
        var width = MapWidth * scale;
        var height = MapHeight * scale;
        return new Rect((Bounds.Width - width) / 2, (Bounds.Height - height) / 2, width, height);
    }

    private static bool IsPatrol(string scenarioType) =>
        scenarioType.Equals("world_human_guard_patrol", StringComparison.OrdinalIgnoreCase);

    private static double Distance(Point left, Point right) =>
        Math.Sqrt(Math.Pow(left.X - right.X, 2) + Math.Pow(left.Y - right.Y, 2));

    private sealed record MarkerInfo(string Id, string ScenarioType, AnalysisCoordinate Position);
}
