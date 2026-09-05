using GtaHeistPlanner.Core.Maps;
using GtaHeistPlanner.Core.Security;

namespace GtaHeistPlanner.Tests.Security;

public sealed class NormalizedCameraGeometryTests
{
    [Fact]
    public void SectorIsExtremelyNarrowNearCamera()
    {
        var sector = Create(heading: 0, fov: 60, range: .4);

        var nearWidth = Distance(sector.NearLeft, sector.NearRight);
        var farWidth = Distance(sector.FarArc[0], sector.FarArc[^1]);

        Assert.Equal(.4 * NormalizedCameraGeometry.NearRadiusRatio, sector.NearRadius, 10);
        Assert.True(nearWidth < farWidth * .04);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(47)]
    [InlineData(180)]
    [InlineData(315)]
    public void BoundaryAnglesAreSymmetricAroundHeading(double heading)
    {
        const double fov = 70;
        var sector = Create(heading, fov, .3);

        AssertAngle(heading - fov / 2, sector.Origin, sector.FarArc[0]);
        AssertAngle(heading + fov / 2, sector.Origin, sector.FarArc[^1]);
    }

    [Fact]
    public void IncreasingFovWidensSector()
    {
        var narrow = Create(0, 30, .4);
        var wide = Create(0, 90, .4);

        Assert.True(Distance(wide.FarArc[0], wide.FarArc[^1]) >
                    Distance(narrow.FarArc[0], narrow.FarArc[^1]));
    }

    [Fact]
    public void IncreasingRangeExtendsArcWithoutChangingFov()
    {
        var shortSector = Create(25, 60, .2);
        var longSector = Create(25, 60, .5);

        Assert.Equal(.2, Distance(shortSector.Origin, shortSector.FarArc[0]), 10);
        Assert.Equal(.5, Distance(longSector.Origin, longSector.FarArc[0]), 10);
        Assert.Equal(Angle(shortSector.Origin, shortSector.FarArc[0]), Angle(longSector.Origin, longSector.FarArc[0]), 9);
        Assert.Equal(Angle(shortSector.Origin, shortSector.FarArc[^1]), Angle(longSector.Origin, longSector.FarArc[^1]), 9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(237)]
    public void SectorCenterlineAgreesWithCameraIconHeading(double heading)
    {
        var sector = Create(heading, 55, .25);

        AssertAngle(heading, sector.Origin, sector.CenterLineEnd);
    }

    private static NormalizedVisionSector Create(double heading, double fov, double range) =>
        NormalizedCameraGeometry.Create(new("camera", "map", "Camera", .5, .5, heading, fov, range));

    private static double Distance(MapPoint first, MapPoint second) =>
        Math.Sqrt(Math.Pow(second.X - first.X, 2) + Math.Pow(second.Y - first.Y, 2));

    private static double Angle(MapPoint origin, MapPoint point) =>
        NormalizeAngle(Math.Atan2(point.Y - origin.Y, point.X - origin.X) * 180 / Math.PI);

    private static void AssertAngle(double expected, MapPoint origin, MapPoint point) =>
        Assert.Equal(NormalizeAngle(expected), Angle(origin, point), 9);

    private static double NormalizeAngle(double angle) => (angle % 360 + 360) % 360;
}
