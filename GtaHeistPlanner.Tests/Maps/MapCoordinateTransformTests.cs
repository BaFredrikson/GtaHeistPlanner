using GtaHeistPlanner.Core.Maps;

namespace GtaHeistPlanner.Tests.Maps;

public sealed class MapCoordinateTransformTests
{
    [Fact]
    public void Transform_AppliesOriginFlipScaleRotationAndOffsetInOrder()
    {
        var calibration = new MapCalibration
        {
            SourceOriginX = 10,
            SourceOriginY = 20,
            ScaleX = 2,
            ScaleY = 3,
            FlipY = true,
            RotationDegrees = 90,
            OffsetX = 100,
            OffsetY = 200,
        };

        var result = MapCoordinateTransform.Transform(new MapPoint(12, 21), calibration);

        Assert.Equal(103, result.X, 10);
        Assert.Equal(204, result.Y, 10);
    }

    [Fact]
    public void Transform_DoesNotMutateSourcePoint()
    {
        var source = new MapPoint(-2250, 275);

        _ = MapCoordinateTransform.Transform(source, new MapCalibration { OffsetX = 4 });

        Assert.Equal(new MapPoint(-2250, 275), source);
    }
}
