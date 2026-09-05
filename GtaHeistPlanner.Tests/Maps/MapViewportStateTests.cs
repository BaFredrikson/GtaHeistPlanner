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

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, .5)]
    [InlineData(6, 1.0 / 6)]
    public void FixedMarkerScaleCancelsViewportZoom(double zoom, double expectedScale) =>
        Assert.Equal(expectedScale, MapViewportMath.FixedMarkerScale(zoom), 10);

    [Fact]
    public void ViewportRoundTripPreservesContentCoordinates()
    {
        var content = new Avalonia.Point(137.25, 82.5);
        var viewport = MapViewportMath.ToViewport(content, 3.2, -120, -45);

        var restored = MapViewportMath.ToContent(viewport, 3.2, -120, -45);

        Assert.Equal(content.X, restored.X, 10);
        Assert.Equal(content.Y, restored.Y, 10);
    }
}
