using System.Windows.Input;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GtaHeistPlanner.App.Models;
using GtaHeistPlanner.App.ViewModels;
using GtaHeistPlanner.Core.Maps;
using GtaHeistPlanner.Core.Security;

namespace GtaHeistPlanner.App.Controls;

public sealed class ManualSecurityOverlayControl : Control
{
    private readonly Stopwatch _animationClock = Stopwatch.StartNew();
    private readonly DispatcherTimer _animationTimer;
    private static readonly IconOverlayRenderer CameraIcon = new(
        new Uri("avares://GtaHeistPlanner.App/Assets/icons/camera.png"), 18,
        new Rect(20, 16, 24, 32), Color.Parse("#FF4D4D"));
    private static readonly SvgIconOverlayRenderer GuardIcon = new(
        new Uri("avares://GtaHeistPlanner.App/Assets/icons/security.svg"), 18, 13,
        Color.Parse("#FF4D4D"));
    private static readonly IBrush VisionBrush = new SolidColorBrush(Color.Parse("#354CBFEA"));
    private static readonly Pen VisionPen = new(new SolidColorBrush(Color.Parse("#AA55CFF4")), 1.2);
    private static readonly Pen PatrolPen = new(new SolidColorBrush(Color.Parse("#D8E6F3")), 2);
    private static readonly Pen SelectedPen = new(new SolidColorBrush(Color.Parse("#F3C969")), 2);
    private string? _dragKind;
    private string? _dragId;
    private int _dragWaypointIndex = -1;

    public ManualSecurityOverlayControl()
    {
        _animationTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render,
            (_, _) => InvalidateVisual());
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _animationTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animationTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    public static readonly StyledProperty<IEnumerable<SecurityCameraViewModel>?> CamerasProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, IEnumerable<SecurityCameraViewModel>?>(nameof(Cameras));
    public static readonly StyledProperty<IEnumerable<SecurityGuardViewModel>?> GuardsProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, IEnumerable<SecurityGuardViewModel>?>(nameof(Guards));
    public static readonly StyledProperty<IEnumerable<SecurityPatrolViewModel>?> PatrolsProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, IEnumerable<SecurityPatrolViewModel>?>(nameof(Patrols));
    public static readonly StyledProperty<string?> MapIdProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, string?>(nameof(MapId));
    public static readonly StyledProperty<double> MapAspectRatioProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, double>(nameof(MapAspectRatio), 1);
    public static readonly StyledProperty<double> ViewportZoomProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, double>(nameof(ViewportZoom), 1);
    public static readonly StyledProperty<bool> ShowCamerasProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, bool>(nameof(ShowCameras));
    public static readonly StyledProperty<bool> ShowGuardsProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, bool>(nameof(ShowGuards));
    public static readonly StyledProperty<bool> IsEditModeProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, bool>(nameof(IsEditMode));
    public static readonly StyledProperty<SecurityCameraViewModel?> SelectedCameraProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, SecurityCameraViewModel?>(nameof(SelectedCamera));
    public static readonly StyledProperty<SecurityGuardViewModel?> SelectedGuardProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, SecurityGuardViewModel?>(nameof(SelectedGuard));
    public static readonly StyledProperty<SecurityPatrolViewModel?> SelectedPatrolProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, SecurityPatrolViewModel?>(nameof(SelectedPatrol));
    public static readonly StyledProperty<int> SelectedWaypointIndexProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, int>(nameof(SelectedWaypointIndex), -1);
    public static readonly StyledProperty<int> RevisionProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, int>(nameof(Revision));
    public static readonly StyledProperty<ICommand?> PlaceCommandProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, ICommand?>(nameof(PlaceCommand));
    public static readonly StyledProperty<ICommand?> MoveCommandProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, ICommand?>(nameof(MoveCommand));
    public static readonly StyledProperty<ICommand?> SelectCameraCommandProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, ICommand?>(nameof(SelectCameraCommand));
    public static readonly StyledProperty<ICommand?> SelectGuardCommandProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, ICommand?>(nameof(SelectGuardCommand));
    public static readonly StyledProperty<ICommand?> SelectWaypointCommandProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, ICommand?>(nameof(SelectWaypointCommand));
    public static readonly StyledProperty<ICommand?> MoveWaypointCommandProperty = AvaloniaProperty.Register<ManualSecurityOverlayControl, ICommand?>(nameof(MoveWaypointCommand));

    static ManualSecurityOverlayControl() => AffectsRender<ManualSecurityOverlayControl>(CamerasProperty, GuardsProperty,
        PatrolsProperty, MapIdProperty, MapAspectRatioProperty, ViewportZoomProperty, ShowCamerasProperty, ShowGuardsProperty,
        IsEditModeProperty, SelectedCameraProperty, SelectedGuardProperty, SelectedPatrolProperty,
        SelectedWaypointIndexProperty, RevisionProperty);

    public IEnumerable<SecurityCameraViewModel>? Cameras { get => GetValue(CamerasProperty); set => SetValue(CamerasProperty, value); }
    public IEnumerable<SecurityGuardViewModel>? Guards { get => GetValue(GuardsProperty); set => SetValue(GuardsProperty, value); }
    public IEnumerable<SecurityPatrolViewModel>? Patrols { get => GetValue(PatrolsProperty); set => SetValue(PatrolsProperty, value); }
    public string? MapId { get => GetValue(MapIdProperty); set => SetValue(MapIdProperty, value); }
    public double MapAspectRatio { get => GetValue(MapAspectRatioProperty); set => SetValue(MapAspectRatioProperty, value); }
    public double ViewportZoom { get => GetValue(ViewportZoomProperty); set => SetValue(ViewportZoomProperty, value); }
    public bool ShowCameras { get => GetValue(ShowCamerasProperty); set => SetValue(ShowCamerasProperty, value); }
    public bool ShowGuards { get => GetValue(ShowGuardsProperty); set => SetValue(ShowGuardsProperty, value); }
    public bool IsEditMode { get => GetValue(IsEditModeProperty); set => SetValue(IsEditModeProperty, value); }
    public SecurityCameraViewModel? SelectedCamera { get => GetValue(SelectedCameraProperty); set => SetValue(SelectedCameraProperty, value); }
    public SecurityGuardViewModel? SelectedGuard { get => GetValue(SelectedGuardProperty); set => SetValue(SelectedGuardProperty, value); }
    public SecurityPatrolViewModel? SelectedPatrol { get => GetValue(SelectedPatrolProperty); set => SetValue(SelectedPatrolProperty, value); }
    public int SelectedWaypointIndex { get => GetValue(SelectedWaypointIndexProperty); set => SetValue(SelectedWaypointIndexProperty, value); }
    public int Revision { get => GetValue(RevisionProperty); set => SetValue(RevisionProperty, value); }
    public ICommand? PlaceCommand { get => GetValue(PlaceCommandProperty); set => SetValue(PlaceCommandProperty, value); }
    public ICommand? MoveCommand { get => GetValue(MoveCommandProperty); set => SetValue(MoveCommandProperty, value); }
    public ICommand? SelectCameraCommand { get => GetValue(SelectCameraCommandProperty); set => SetValue(SelectCameraCommandProperty, value); }
    public ICommand? SelectGuardCommand { get => GetValue(SelectGuardCommandProperty); set => SetValue(SelectGuardCommandProperty, value); }
    public ICommand? SelectWaypointCommand { get => GetValue(SelectWaypointCommandProperty); set => SetValue(SelectWaypointCommandProperty, value); }
    public ICommand? MoveWaypointCommand { get => GetValue(MoveWaypointCommandProperty); set => SetValue(MoveWaypointCommandProperty, value); }

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        var rect = MapRect();
        var markerScale = MapViewportMath.FixedMarkerScale(ViewportZoom);
        if (ShowGuards || IsEditMode)
        {
            foreach (var patrol in Patrols?.Where(item => item.MapId == MapId && (item.IsActive || IsEditMode)) ?? [])
            {
                for (var i = 1; i < patrol.Waypoints.Count; i++)
                    context.DrawLine(PatrolPen, Screen(patrol.Waypoints[i - 1].X, patrol.Waypoints[i - 1].Y, rect), Screen(patrol.Waypoints[i].X, patrol.Waypoints[i].Y, rect));
                for (var index = 0; index < patrol.Waypoints.Count; index++)
                {
                    var waypoint = patrol.Waypoints[index];
                    var center = Screen(waypoint.X, waypoint.Y, rect);
                    context.DrawEllipse(null, MarkerPen(PatrolPen), center, 3 * markerScale, 3 * markerScale);
                    if (IsEditMode && patrol == SelectedPatrol && index == SelectedWaypointIndex)
                        context.DrawEllipse(null, MarkerPen(SelectedPen), center, 8 * markerScale, 8 * markerScale);
                }
            }
            foreach (var guard in Guards?.Where(item => item.MapId == MapId && (item.IsActive || IsEditMode)) ?? [])
            {
                var position = AnimatedGuardPosition(guard);
                var center = Screen(position.X, position.Y, rect);
                using var opacity = context.PushOpacity(guard.IsActive ? 1 : .25);
                GuardIcon.Draw(context, center, markerScale);
                if (guard == SelectedGuard)
                    context.DrawEllipse(null, MarkerPen(SelectedPen), center, 11 * markerScale, 11 * markerScale);
            }
        }
        if (ShowCameras || IsEditMode)
        {
            foreach (var camera in Cameras?.Where(item => item.MapId == MapId && (item.IsActive || IsEditMode)) ?? [])
            {
                using var opacity = context.PushOpacity(camera.IsActive ? 1 : .25);
                var definition = camera.ToDomain();
                var cone = NormalizedCameraGeometry.Create(definition, MapAspectRatio);
                var geometry = new StreamGeometry();
                using (var gc = geometry.Open())
                {
                    gc.BeginFigure(Screen(cone.NearLeft.X, cone.NearLeft.Y, rect), true);
                    foreach (var arcPoint in cone.FarArc)
                        gc.LineTo(Screen(arcPoint.X, arcPoint.Y, rect));
                    gc.LineTo(Screen(cone.NearRight.X, cone.NearRight.Y, rect));
                    gc.EndFigure(true);
                }
                context.DrawGeometry(VisionBrush, VisionPen, geometry);
                var center = Screen(camera.X, camera.Y, rect);
                CameraIcon.Draw(context, center, camera.RotationDegrees - 90, visualScale: markerScale);
                if (camera == SelectedCamera) context.DrawEllipse(null, MarkerPen(SelectedPen), center, 13 * markerScale, 13 * markerScale);
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!IsEditMode || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var point = e.GetPosition(this);
        var rect = MapRect();
        foreach (var patrol in Patrols?.Where(item => item.MapId == MapId) ?? [])
        {
            for (var index = 0; index < patrol.Waypoints.Count; index++)
            {
                if (Distance(point, Screen(patrol.Waypoints[index].X, patrol.Waypoints[index].Y, rect)) > 10 * MapViewportMath.FixedMarkerScale(ViewportZoom)) continue;
                SelectWaypointCommand?.Execute(new SecurityWaypointSelection(patrol.Id, index));
                _dragKind = "waypoint";
                _dragId = patrol.Id;
                _dragWaypointIndex = index;
                e.Pointer.Capture(this);
                return;
            }
        }
        var camera = Cameras?.Where(item => item.MapId == MapId).OrderBy(item => Distance(point, Screen(item.X, item.Y, rect))).FirstOrDefault(item => Distance(point, Screen(item.X, item.Y, rect)) <= 14 * MapViewportMath.FixedMarkerScale(ViewportZoom));
        if (camera is not null) { SelectCameraCommand?.Execute(camera.Id); BeginDrag(e, "camera", camera.Id); return; }
        var guard = Guards?.Where(item => item.MapId == MapId).OrderBy(item => Distance(point, Screen(item.X, item.Y, rect))).FirstOrDefault(item => Distance(point, Screen(item.X, item.Y, rect)) <= 14 * MapViewportMath.FixedMarkerScale(ViewportZoom));
        if (guard is not null) { SelectGuardCommand?.Execute(guard.Id); BeginDrag(e, "guard", guard.Id); return; }
        if (Normalize(point, rect, out var normalized)) PlaceCommand?.Execute(normalized);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_dragId is not null && Normalize(e.GetPosition(this), MapRect(), out var point))
        {
            if (_dragKind == "waypoint")
                MoveWaypointCommand?.Execute(new SecurityWaypointMove(_dragId, _dragWaypointIndex, point.X, point.Y));
            else
                MoveCommand?.Execute(new SecurityMarkerMove(_dragKind!, _dragId, point.X, point.Y));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e) { _dragId = null; _dragKind = null; _dragWaypointIndex = -1; e.Pointer.Capture(null); }
    private void BeginDrag(PointerPressedEventArgs e, string kind, string id) { if (!IsEditMode) return; _dragKind = kind; _dragId = id; e.Pointer.Capture(this); }
    private Rect MapRect() { var width = Math.Min(Bounds.Width, Bounds.Height * MapAspectRatio); var height = width / MapAspectRatio; return new((Bounds.Width - width) / 2, (Bounds.Height - height) / 2, width, height); }
    private static Point Screen(double x, double y, Rect rect) => new(rect.X + x * rect.Width, rect.Y + y * rect.Height);
    private static bool Normalize(Point p, Rect rect, out MapPoint result) { if (!rect.Contains(p)) { result = default; return false; } result = new((p.X - rect.X) / rect.Width, (p.Y - rect.Y) / rect.Height); return true; }
    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    private Pen MarkerPen(Pen pen) => new(pen.Brush, pen.Thickness * MapViewportMath.FixedMarkerScale(ViewportZoom));

    private MapPoint AnimatedGuardPosition(SecurityGuardViewModel guard)
    {
        if (IsEditMode)
            return new MapPoint(guard.X, guard.Y);

        var patrol = Patrols?.FirstOrDefault(item =>
            item.MapId == MapId && item.IsActive && guard.PatrolIds.Contains(item.Id) && item.Waypoints.Count > 0);
        return patrol is null
            ? new MapPoint(guard.X, guard.Y)
            : PatrolAnimation.PositionAt(patrol.Waypoints, _animationClock.Elapsed.TotalSeconds);
    }
}
