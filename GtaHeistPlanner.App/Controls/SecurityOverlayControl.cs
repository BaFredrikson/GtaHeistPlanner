using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System.Globalization;
using System.Windows.Input;
using GtaHeistPlanner.App.Models;
using GtaHeistPlanner.App.ViewModels;
using GtaHeistPlanner.Core.Loot;
using GtaHeistPlanner.Core.Maps;
using GtaHeistPlanner.Core.Overlays;

namespace GtaHeistPlanner.App.Controls;

public sealed class SecurityOverlayControl : Control
{
    private const double MapWidth = 185.1;
    private const double MapHeight = 227.65;
    private const double CameraIconDisplaySize = 16;
    private const double LootIconDisplaySize = 16;
    // camera.png points down (+Y / 90 degrees) before rotation; subtract 90 degrees
    // so its lens aligns with an angle measured from the screen's +X axis.
    private const double CameraIconBaseRotationOffsetDegrees = -90;
    private static readonly IBrush RouteBrush = new SolidColorBrush(Color.Parse("#D8E6F3"));
    private static readonly IBrush NodeBrush = new SolidColorBrush(Color.Parse("#48C9E8"));
    private static readonly IBrush TorchBrush = new SolidColorBrush(Color.Parse("#FFD35A"));
    private static readonly IBrush PatrolBrush = new SolidColorBrush(Color.Parse("#FF6B57"));
    private static readonly IBrush SecurityBrush = new SolidColorBrush(Color.Parse("#B68CFF"));
    private static readonly Pen RoutePen = new(RouteBrush, 2);
    private static readonly Pen OutlinePen = new(new SolidColorBrush(Color.Parse("#11151B")), 1.5);
    private static readonly IBrush CameraConeBrush = new SolidColorBrush(Color.Parse("#334CBFEA"));
    private static readonly Pen CameraConePen = new(new SolidColorBrush(Color.Parse("#AA55CFF4")), 1.25);
    private static readonly IconOverlayRenderer CameraIcon = new(
        new Uri("avares://GtaHeistPlanner.App/Assets/icons/camera.png"),
        CameraIconDisplaySize,
        // Crop camera.png's transparent 64x64 padding to its 24x32 visible artwork.
        new Rect(20, 16, 24, 32),
        Color.Parse("#FF4D4D"));
    private static readonly IconOverlayRenderer LootIcon = new(
        new Uri("avares://GtaHeistPlanner.App/Assets/icons/loot.png"), LootIconDisplaySize,
        new Rect(12, 2, 40, 60));
    private static readonly IconOverlayRenderer SpecialLootIcon = new(
        new Uri("avares://GtaHeistPlanner.App/Assets/icons/special_loot.png"), LootIconDisplaySize,
        new Rect(0, 0, 55, 64));
    private static readonly IconOverlayRenderer TruckCargoIcon = new(
        new Uri("avares://GtaHeistPlanner.App/Assets/icons/truckcargo.png"), 32,
        new Rect(0, 15, 64, 35));
    private static readonly Pen SelectedLootPen = new(new SolidColorBrush(Color.Parse("#F3C969")), 2);
    private static readonly Pen BuyersRequestPen = new(new SolidColorBrush(Color.Parse("#FF5BA8")), 2.5);
    private static readonly Pen LootedPen = new(new SolidColorBrush(Color.Parse("#E8EDF3")), 2.5);
    private static readonly IBrush LootLabelBackground = new SolidColorBrush(Color.Parse("#B810141A"));
    private static readonly IBrush LootLabelForeground = new SolidColorBrush(Color.Parse("#F2F5F8"));

    public static readonly StyledProperty<SecurityAnalysis?> AnalysisProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, SecurityAnalysis?>(nameof(Analysis));

    public static readonly StyledProperty<MapCalibration?> CalibrationProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, MapCalibration?>(nameof(Calibration));

    public static readonly StyledProperty<CameraOverlayData?> CameraDataProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, CameraOverlayData?>(nameof(CameraData));

    public static readonly StyledProperty<bool> ShowSecurityProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, bool>(nameof(ShowSecurity), true);

    public static readonly StyledProperty<bool> ShowCamerasProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, bool>(nameof(ShowCameras), true);

    public static readonly StyledProperty<bool> ShowCameraConesProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, bool>(nameof(ShowCameraCones), true);

    public static readonly StyledProperty<string?> DiagnosticTextProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, string?>(nameof(DiagnosticText), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<IEnumerable<LootMarkerViewModel>?> LootMarkersProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, IEnumerable<LootMarkerViewModel>?>(nameof(LootMarkers));
    public static readonly StyledProperty<string?> MapIdProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, string?>(nameof(MapId));
    public static readonly StyledProperty<double> MapAspectRatioProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, double>(nameof(MapAspectRatio), 1);
    public static readonly StyledProperty<double> ViewportZoomProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, double>(nameof(ViewportZoom), 1);
    public static readonly StyledProperty<bool> IsLootEditModeProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, bool>(nameof(IsLootEditMode));
    public static readonly StyledProperty<bool> ShowLootProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, bool>(nameof(ShowLoot), true);
    public static readonly StyledProperty<bool> ShowUnknownLootClearlyProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, bool>(nameof(ShowUnknownLootClearly));
    public static readonly StyledProperty<LootMarkerViewModel?> SelectedLootProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, LootMarkerViewModel?>(nameof(SelectedLoot));
    public static readonly StyledProperty<int> LootRevisionProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, int>(nameof(LootRevision));
    public static readonly StyledProperty<ICommand?> CreateLootCommandProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, ICommand?>(nameof(CreateLootCommand));
    public static readonly StyledProperty<ICommand?> SelectLootCommandProperty =
        AvaloniaProperty.Register<SecurityOverlayControl, ICommand?>(nameof(SelectLootCommand));

    static SecurityOverlayControl()
    {
        AffectsRender<SecurityOverlayControl>(AnalysisProperty, CalibrationProperty, CameraDataProperty,
            ShowSecurityProperty, ShowCamerasProperty, ShowCameraConesProperty, LootMarkersProperty,
            MapIdProperty, MapAspectRatioProperty, ViewportZoomProperty, IsLootEditModeProperty, ShowLootProperty,
            ShowUnknownLootClearlyProperty, SelectedLootProperty, LootRevisionProperty);
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

    public CameraOverlayData? CameraData
    {
        get => GetValue(CameraDataProperty);
        set => SetValue(CameraDataProperty, value);
    }

    public bool ShowSecurity
    {
        get => GetValue(ShowSecurityProperty);
        set => SetValue(ShowSecurityProperty, value);
    }

    public bool ShowCameras
    {
        get => GetValue(ShowCamerasProperty);
        set => SetValue(ShowCamerasProperty, value);
    }

    public bool ShowCameraCones
    {
        get => GetValue(ShowCameraConesProperty);
        set => SetValue(ShowCameraConesProperty, value);
    }

    public string? DiagnosticText
    {
        get => GetValue(DiagnosticTextProperty);
        set => SetValue(DiagnosticTextProperty, value);
    }

    public IEnumerable<LootMarkerViewModel>? LootMarkers { get => GetValue(LootMarkersProperty); set => SetValue(LootMarkersProperty, value); }
    public string? MapId { get => GetValue(MapIdProperty); set => SetValue(MapIdProperty, value); }
    public double MapAspectRatio { get => GetValue(MapAspectRatioProperty); set => SetValue(MapAspectRatioProperty, value); }
    public double ViewportZoom { get => GetValue(ViewportZoomProperty); set => SetValue(ViewportZoomProperty, value); }
    public bool IsLootEditMode { get => GetValue(IsLootEditModeProperty); set => SetValue(IsLootEditModeProperty, value); }
    public bool ShowLoot { get => GetValue(ShowLootProperty); set => SetValue(ShowLootProperty, value); }
    public bool ShowUnknownLootClearly { get => GetValue(ShowUnknownLootClearlyProperty); set => SetValue(ShowUnknownLootClearlyProperty, value); }
    public LootMarkerViewModel? SelectedLoot { get => GetValue(SelectedLootProperty); set => SetValue(SelectedLootProperty, value); }
    public int LootRevision { get => GetValue(LootRevisionProperty); set => SetValue(LootRevisionProperty, value); }
    public ICommand? CreateLootCommand { get => GetValue(CreateLootCommandProperty); set => SetValue(CreateLootCommandProperty, value); }
    public ICommand? SelectLootCommand { get => GetValue(SelectLootCommandProperty); set => SetValue(SelectLootCommandProperty, value); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        // A transparent drawing gives the otherwise-empty overlay a hit-test surface,
        // allowing edit mode to create the first marker anywhere on the map.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        if (Calibration is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var mapRect = GetMapRect();
        var cameraCones = CameraData?.Cameras.ToDictionary(
            camera => camera.Id,
            camera => CameraConeGeometry.Transform(CameraConeGeometry.CreateWorldSpace(camera), Calibration));
        if (ShowCameras && ShowCameraCones && CameraData is not null && CameraData.MapId == MapId)
        {
            foreach (var camera in CameraData.Cameras)
                DrawCameraCone(context, cameraCones![camera.Id], mapRect);
        }

        if (ShowSecurity && Analysis is not null)
        {
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

        if (ShowCameras && CameraData is not null && CameraData.MapId == MapId)
        {
            foreach (var camera in CameraData.Cameras)
            {
                var cone = cameraCones![camera.Id];
                var origin = ToScreenNormalized(cone.Origin, mapRect);
                var lookPoint = ToScreenNormalized(cone.CenterLineEnd, mapRect);
                var rotation = DeriveCameraIconRotation(origin, lookPoint);
                CameraIcon.Draw(context, origin, rotation);
            }
        }

        DrawLoot(context);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        var pointer = e.GetPosition(this);
        var marker = FindLootMarker(pointer);
        if (marker is not null)
        {
            SelectLootCommand?.Execute(marker.Id);
            e.Handled = true;
            return;
        }

        if (IsLootEditMode && TryToNormalized(pointer, out var normalized))
        {
            CreateLootCommand?.Execute(normalized);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Calibration is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var pointer = e.GetPosition(this);
        var loot = FindLootMarker(pointer);
        if (loot is not null)
        {
            var economics = loot.Economics;
            var requirement = economics.RequiredPrep == OptionalPrep.None ? "No optional prep" : $"Requires {FormatPrep(economics.RequiredPrep)}";
            var access = loot.Zone is null ? "Solo accessible" : $"{loot.Zone.DisplayName} · minimum {loot.Zone.MinimumPlayers} players";
            DiagnosticText = $"{loot.Name} ({loot.Id})\n{economics.DisplayName} · Bag {economics.BagPercent}% · ${economics.MinValue:N0}–${economics.MaxValue:N0}\n" +
                $"{requirement} · {access}\n{loot.MapId} · ({loot.X:F4}, {loot.Y:F4})\n" +
                $"{(loot.IsPresent ? "Present" : "Absent")} · {(loot.IsBuyersRequest ? "Buyer's Request" : "Normal")} · {(loot.IsLooted ? "Looted" : "Not looted")}";
            return;
        }

        var mapRect = GetMapRect();
        IEnumerable<MarkerInfo> candidates = [];
        if (ShowSecurity && Analysis is not null)
        {
            candidates = Analysis.SecurityScenarioPoints.Select(point => new MarkerInfo(
                    $"Point {point.SourcePointIndex} · {point.ScenarioType}", point.Position.X, point.Position.Y))
                .Concat(Analysis.GuardChains.Where(chain => chain.ChainIndex == 7)
                    .SelectMany(chain => chain.Nodes)
                    .Select(node => new MarkerInfo($"Node {node.NodeIndex} · {node.ScenarioType}", node.Position.X, node.Position.Y)));
        }
        if (ShowCameras && CameraData is not null && CameraData.MapId == MapId)
        {
            candidates = candidates.Concat(CameraData.Cameras.Select(camera =>
            {
                var cone = CameraConeGeometry.Transform(CameraConeGeometry.CreateWorldSpace(camera), Calibration);
                var screenAngle = DeriveCameraIconRotation(
                    ToScreenNormalized(cone.Origin, mapRect),
                    ToScreenNormalized(cone.CenterLineEnd, mapRect));
                return new MarkerInfo(
                    $"Camera {camera.Id} · {camera.Label}\nHeading {camera.HeadingDegrees:F1}°, FOV {camera.FovDegrees:F1}°, range {camera.Range:F1}, icon {screenAngle:F1}°",
                    camera.Position.X, camera.Position.Y);
            }));
        }

        var closest = candidates
            .Select(marker => new { Marker = marker, Screen = ToScreen(new MapPoint(marker.RawX, marker.RawY), mapRect) })
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
            new MapPoint(closest.Marker.RawX, closest.Marker.RawY), Calibration);
        DiagnosticText = $"{closest.Marker.Description}\n" +
            $"GTA ({closest.Marker.RawX:F3}, {closest.Marker.RawY:F3}) → " +
            $"planner ({transformed.X:F4}, {transformed.Y:F4})";
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        DiagnosticText = null;
    }

    private Point ToScreen(AnalysisCoordinate point, Rect mapRect)
    {
        return ToScreen(new MapPoint(point.X, point.Y), mapRect);
    }

    private Point ToScreen(MapPoint point, Rect mapRect)
    {
        var transformed = MapCoordinateTransform.Transform(point, Calibration!);
        return new Point(mapRect.X + transformed.X * mapRect.Width, mapRect.Y + transformed.Y * mapRect.Height);
    }

    private static void DrawCameraCone(DrawingContext context, CameraCone cone, Rect mapRect)
    {
        var origin = ToScreenNormalized(cone.Origin, mapRect);
        var left = ToScreenNormalized(cone.LeftBoundary, mapRect);
        var right = ToScreenNormalized(cone.RightBoundary, mapRect);
        var geometry = new StreamGeometry();
        using (var geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(origin, true);
            geometryContext.LineTo(left);
            geometryContext.LineTo(right);
            geometryContext.EndFigure(true);
        }
        context.DrawGeometry(CameraConeBrush, CameraConePen, geometry);
    }

    private static Point ToScreenNormalized(MapPoint point, Rect mapRect) =>
        new(mapRect.X + point.X * mapRect.Width, mapRect.Y + point.Y * mapRect.Height);

    private static double DeriveCameraIconRotation(Point origin, Point lookPoint) =>
        CameraFacingOrientation.DeriveRotationDegrees(
            new MapPoint(origin.X, origin.Y),
            new MapPoint(lookPoint.X, lookPoint.Y),
            CameraIconBaseRotationOffsetDegrees);

    private Rect GetMapRect()
    {
        var scale = Math.Min(Bounds.Width / MapWidth, Bounds.Height / MapHeight);
        var width = MapWidth * scale;
        var height = MapHeight * scale;
        return new Rect((Bounds.Width - width) / 2, (Bounds.Height - height) / 2, width, height);
    }

    private Rect GetLootMapRect()
    {
        var aspect = MapAspectRatio > 0 ? MapAspectRatio : 1;
        var width = Math.Min(Bounds.Width, Bounds.Height * aspect);
        var height = width / aspect;
        return new Rect((Bounds.Width - width) / 2, (Bounds.Height - height) / 2, width, height);
    }

    private IEnumerable<LootMarkerViewModel> CurrentLoot() =>
        LootMarkers?.Where(marker => marker.MapId == MapId) ?? [];

    private void DrawLoot(DrawingContext context)
    {
        if (!ShowLoot)
            return;
        var mapRect = GetLootMapRect();
        var markerScale = MapViewportMath.FixedMarkerScale(ViewportZoom);
        foreach (var marker in CurrentLoot())
        {
            if (!marker.IsPresent && !IsLootEditMode && !ShowUnknownLootClearly)
                continue;
            var center = new Point(mapRect.X + marker.X * mapRect.Width, mapRect.Y + marker.Y * mapRect.Height);
            var opacity = !marker.IsPresent && !ShowUnknownLootClearly ? 0.25 : marker.IsLooted ? 0.35 : 1;
            var renderer = marker.Type == LootType.LoadingBayCargo
                ? TruckCargoIcon
                : marker.IsBuyersRequest ? SpecialLootIcon : LootIcon;
            renderer.Draw(context, center, opacity: opacity, visualScale: markerScale);
            DrawLootLabel(context, marker.LabelText, center, markerScale, opacity);
            if (marker.Type == LootType.LoadingBayCargo && marker.IsBuyersRequest)
                context.DrawEllipse(null, MarkerPen(BuyersRequestPen), center, 14 * markerScale, 14 * markerScale);
            if (marker == SelectedLoot)
                context.DrawEllipse(null, MarkerPen(SelectedLootPen), center, 16 * markerScale, 16 * markerScale);
            if (marker.IsLooted)
            {
                context.DrawLine(MarkerPen(LootedPen), center + new Vector(-8, -8) * markerScale, center + new Vector(8, 8) * markerScale);
                context.DrawLine(MarkerPen(LootedPen), center + new Vector(8, -8) * markerScale, center + new Vector(-8, 8) * markerScale);
            }
        }
    }

    private static void DrawLootLabel(DrawingContext context, string text, Point markerCenter, double visualScale, double opacity)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold), 10 * visualScale, LootLabelForeground);
        var paddingX = 3 * visualScale;
        var paddingY = 1.5 * visualScale;
        var top = markerCenter.Y + 11 * visualScale;
        var background = new Rect(markerCenter.X - formatted.Width / 2 - paddingX, top,
            formatted.Width + paddingX * 2, formatted.Height + paddingY * 2);
        using (context.PushOpacity(opacity))
        {
            context.DrawRectangle(LootLabelBackground, null, background, 3 * visualScale, 3 * visualScale);
            context.DrawText(formatted, new Point(markerCenter.X - formatted.Width / 2, top + paddingY));
        }
    }

    private LootMarkerViewModel? FindLootMarker(Point point)
    {
        if (!ShowLoot)
            return null;
        var mapRect = GetLootMapRect();
        return CurrentLoot()
            .Where(marker => marker.IsPresent || IsLootEditMode || ShowUnknownLootClearly)
            .Select(marker => new
            {
                Marker = marker,
                Distance = Distance(point, new Point(mapRect.X + marker.X * mapRect.Width, mapRect.Y + marker.Y * mapRect.Height)),
            })
            .Where(item => item.Distance <= 16 * MapViewportMath.FixedMarkerScale(ViewportZoom))
            .OrderBy(item => item.Distance)
            .Select(item => item.Marker)
            .FirstOrDefault();
    }

    private Pen MarkerPen(Pen pen) => new(pen.Brush, pen.Thickness * MapViewportMath.FixedMarkerScale(ViewportZoom));

    private bool TryToNormalized(Point point, out MapPoint normalized)
    {
        var mapRect = GetLootMapRect();
        if (!mapRect.Contains(point))
        {
            normalized = default;
            return false;
        }
        normalized = new MapPoint((point.X - mapRect.X) / mapRect.Width, (point.Y - mapRect.Y) / mapRect.Height);
        return true;
    }

    private static bool IsPatrol(string scenarioType) =>
        scenarioType.Equals("world_human_guard_patrol", StringComparison.OrdinalIgnoreCase);

    private static double Distance(Point left, Point right) =>
        Math.Sqrt(Math.Pow(left.X - right.X, 2) + Math.Pow(left.Y - right.Y, 2));

    private static string FormatPrep(OptionalPrep prep) => prep switch
    {
        OptionalPrep.GlassCutter => "Glass Cutter",
        OptionalPrep.PowerDrills => "Power Drills",
        _ => "None",
    };

    private sealed record MarkerInfo(string Description, double RawX, double RawY);
}
