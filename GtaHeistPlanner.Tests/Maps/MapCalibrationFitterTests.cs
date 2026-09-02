using GtaHeistPlanner.Core.Maps;

namespace GtaHeistPlanner.Tests.Maps;

public sealed class MapCalibrationFitterTests
{
    [Fact]
    public void Fit_CentersBoundsAndPreservesTenPercentPadding()
    {
        MapPoint[] points = [new(10, 20), new(30, 60)];

        var calibration = MapCalibrationFitter.Fit(points, 0.1);
        var first = MapCoordinateTransform.Transform(points[0], calibration);
        var second = MapCoordinateTransform.Transform(points[1], calibration);

        Assert.Equal(20, calibration.SourceOriginX);
        Assert.Equal(40, calibration.SourceOriginY);
        Assert.Equal(calibration.ScaleX, calibration.ScaleY);
        Assert.Equal(0.3, first.X, 10);
        Assert.Equal(0.9, first.Y, 10);
        Assert.Equal(0.7, second.X, 10);
        Assert.Equal(0.1, second.Y, 10);
    }

    [Fact]
    public void Fit_RejectsEmptyDataset()
    {
        Assert.Throws<ArgumentException>(() => MapCalibrationFitter.Fit([]));
    }
}
