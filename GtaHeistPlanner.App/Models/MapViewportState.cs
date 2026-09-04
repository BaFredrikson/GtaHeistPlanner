using CommunityToolkit.Mvvm.ComponentModel;

namespace GtaHeistPlanner.App.Models;

public partial class MapViewportState : ObservableObject
{
    public const double MinimumZoom = 1;
    public const double MaximumZoom = 6;

    [ObservableProperty] public partial double Zoom { get; set; } = 1;
    [ObservableProperty] public partial double PanX { get; set; }
    [ObservableProperty] public partial double PanY { get; set; }

    public void SetZoom(double zoom)
    {
        Zoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);
        if (Zoom == MinimumZoom)
        {
            PanX = 0;
            PanY = 0;
        }
    }

    public void Reset()
    {
        Zoom = MinimumZoom;
        PanX = 0;
        PanY = 0;
    }
}
