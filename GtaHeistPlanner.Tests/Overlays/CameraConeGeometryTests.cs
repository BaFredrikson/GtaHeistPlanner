using GtaHeistPlanner.Core.Maps;
using GtaHeistPlanner.Core.Overlays;

namespace GtaHeistPlanner.Tests.Overlays;

public sealed class CameraConeGeometryTests
{
    [Fact]
    public void CreateWorldSpace_UsesGtaHeadingWithZeroAlongPositiveY()
    {
        var camera = Camera(heading: 0, fov: 90, range: 10);

        var cone = CameraConeGeometry.CreateWorldSpace(camera);

        Assert.Equal(0, cone.CenterLineEnd.X, 10);
        Assert.Equal(10, cone.CenterLineEnd.Y, 10);
        Assert.Equal(-Math.Sqrt(50), cone.LeftBoundary.X, 10);
        Assert.Equal(Math.Sqrt(50), cone.RightBoundary.X, 10);
    }

    [Fact]
    public void Transform_AppliesRotationAndFlipToHeadingGeometry()
    {
        var worldCone = CameraConeGeometry.CreateWorldSpace(Camera(heading: 0, fov: 60, range: 10));
        var calibration = new MapCalibration
        {
            FlipY = true,
            RotationDegrees = 90,
            ScaleX = 2,
            ScaleY = 2,
        };

        var transformed = CameraConeGeometry.Transform(worldCone, calibration);

        Assert.Equal(20, transformed.CenterLineEnd.X, 10);
        Assert.Equal(0, transformed.CenterLineEnd.Y, 10);
    }

    [Fact]
    public void CreateWorldSpace_RejectsInvalidFovAndRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CameraConeGeometry.CreateWorldSpace(Camera(0, 180, 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CameraConeGeometry.CreateWorldSpace(Camera(0, 60, 0)));
    }

    [Theory]
    [InlineData(10, 10, 10, 20, -90, 0)]
    [InlineData(10, 10, 20, 10, -90, -90)]
    [InlineData(10, 10, 10, 0, -90, 180)]
    public void DeriveRotationDegrees_AppliesDownwardFacingIconOffset(
        double originX, double originY, double lookX, double lookY,
        double baseOffset, double expected)
    {
        var rotation = CameraFacingOrientation.DeriveRotationDegrees(
            new MapPoint(originX, originY), new MapPoint(lookX, lookY), baseOffset);

        Assert.Equal(expected, rotation, 10);
    }

    [Fact]
    public void DeriveRotationDegrees_UsesPostTransformDirection()
    {
        var camera = Camera(heading: 0, fov: 60, range: 10);
        var cone = CameraConeGeometry.Transform(
            CameraConeGeometry.CreateWorldSpace(camera),
            new MapCalibration { FlipY = true, RotationDegrees = 90 });

        var rotation = CameraFacingOrientation.DeriveRotationDegrees(
            cone.Origin, cone.CenterLineEnd, baseRotationOffsetDegrees: -90);

        Assert.Equal(-90, rotation, 10);
    }

    private static CameraOverlayDefinition Camera(double heading, double fov, double range) =>
        new("camera-test", "exterior-firstfloor", new WorldPosition(0, 0, 12),
            heading, fov, range);
}
