using GtaHeistPlanner.Core.Security;

namespace GtaHeistPlanner.Tests.Security;

public sealed class NormalizedCameraGeometryTests
{
    [Fact]
    public void ZeroDegreeCameraPointsRightWithSymmetricTaper()
    {
        var camera = new SecurityCameraDefinition("camera", "map", "Camera", .5, .5, 0, 60, .2);

        var area = NormalizedCameraGeometry.Create(camera);

        Assert.Equal(.7, area.Center.X, 10);
        Assert.Equal(.5, area.Center.Y, 10);
        Assert.Equal(area.Left.X, area.Right.X, 10);
        Assert.Equal(.5 - area.Left.Y, area.Right.Y - .5, 10);
    }
}
