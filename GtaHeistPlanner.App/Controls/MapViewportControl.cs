using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using GtaHeistPlanner.App.Models;

namespace GtaHeistPlanner.App.Controls;

public sealed class MapViewportControl : ContentControl
{
    private Point? _lastPanPoint;

    public static readonly StyledProperty<double> ZoomProperty = AvaloniaProperty.Register<MapViewportControl, double>(
        nameof(Zoom), 1, defaultBindingMode: BindingMode.TwoWay, coerce: (_, value) => Math.Clamp(value, MapViewportState.MinimumZoom, MapViewportState.MaximumZoom));
    public static readonly StyledProperty<double> PanXProperty = AvaloniaProperty.Register<MapViewportControl, double>(nameof(PanX), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<double> PanYProperty = AvaloniaProperty.Register<MapViewportControl, double>(nameof(PanY), defaultBindingMode: BindingMode.TwoWay);

    static MapViewportControl()
    {
        AffectsArrange<MapViewportControl>(ZoomProperty, PanXProperty, PanYProperty);
        ZoomProperty.Changed.AddClassHandler<MapViewportControl>((control, _) => control.UpdateTransform());
        PanXProperty.Changed.AddClassHandler<MapViewportControl>((control, _) => control.UpdateTransform());
        PanYProperty.Changed.AddClassHandler<MapViewportControl>((control, _) => control.UpdateTransform());
    }

    public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public double PanX { get => GetValue(PanXProperty); set => SetValue(PanXProperty, value); }
    public double PanY { get => GetValue(PanYProperty); set => SetValue(PanYProperty, value); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateTransform();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var oldZoom = Zoom;
        var nextZoom = Math.Clamp(oldZoom * (e.Delta.Y > 0 ? 1.15 : 1 / 1.15), MapViewportState.MinimumZoom, MapViewportState.MaximumZoom);
        var pointer = e.GetPosition(this);
        var contentPoint = MapViewportMath.ToContent(pointer, oldZoom, PanX, PanY);
        PanX = pointer.X - contentPoint.X * nextZoom;
        PanY = pointer.Y - contentPoint.Y * nextZoom;
        Zoom = nextZoom;
        if (Zoom == MapViewportState.MinimumZoom) { PanX = 0; PanY = 0; }
        ClampPan();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed) return;
        _lastPanPoint = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_lastPanPoint is not { } previous) return;
        var current = e.GetPosition(this);
        PanX += current.X - previous.X;
        PanY += current.Y - previous.Y;
        ClampPan();
        _lastPanPoint = current;
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_lastPanPoint is null) return;
        _lastPanPoint = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void UpdateTransform()
    {
        if (Content is not Control content) return;
        content.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
        content.RenderTransform = new MatrixTransform(Matrix.CreateScale(Zoom, Zoom) * Matrix.CreateTranslation(PanX, PanY));
    }

    private void ClampPan()
    {
        if (Zoom <= 1) { PanX = 0; PanY = 0; return; }
        PanX = Math.Clamp(PanX, Bounds.Width * (1 - Zoom), 0);
        PanY = Math.Clamp(PanY, Bounds.Height * (1 - Zoom), 0);
    }
}
