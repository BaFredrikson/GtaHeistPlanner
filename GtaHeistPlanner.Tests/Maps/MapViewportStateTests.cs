using GtaHeistPlanner.App.Models;

namespace GtaHeistPlanner.Tests.Maps;

public sealed class MapViewportStateTests
{
    [Fact]
    public void ZoomIsClampedToSupportedRange()
    {
        var viewport = new MapViewportState();

        viewport.SetZoom(100);
        Assert.Equal(6, viewport.Zoom);

        viewport.SetZoom(.1);
        Assert.Equal(1, viewport.Zoom);
    }

    [Fact]
    public void ResetRestoresFittedUnpannedView()
    {
        var viewport = new MapViewportState { Zoom = 3, PanX = -120, PanY = -80 };

        viewport.Reset();

        Assert.Equal(1, viewport.Zoom);
        Assert.Equal(0, viewport.PanX);
        Assert.Equal(0, viewport.PanY);
    }
}
