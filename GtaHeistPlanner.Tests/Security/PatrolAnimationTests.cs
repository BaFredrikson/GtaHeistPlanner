using GtaHeistPlanner.Core.Security;

namespace GtaHeistPlanner.Tests.Security;

public sealed class PatrolAnimationTests
{
    private static readonly PatrolWaypoint[] Route = [new(0, 0), new(1, 0), new(1, 1)];

    [Fact]
    public void GuardWaitsAtEachNodeForHalfASecond()
    {
        Assert.Equal(new(0, 0), PatrolAnimation.PositionAt(Route, .25, speed: 1));
        Assert.Equal(new(1, 0), PatrolAnimation.PositionAt(Route, 1.6, speed: 1));
        Assert.Equal(new(1, 1), PatrolAnimation.PositionAt(Route, 3.0, speed: 1));
    }

    [Fact]
    public void GuardTraversesRouteThenReturns()
    {
        Assert.Equal(new(.5, 0), PatrolAnimation.PositionAt(Route, 1.0, speed: 1));
        Assert.Equal(new(1, .5), PatrolAnimation.PositionAt(Route, 2.5, speed: 1));
        Assert.Equal(new(1, .5), PatrolAnimation.PositionAt(Route, 4.0, speed: 1));
        Assert.Equal(new(.5, 0), PatrolAnimation.PositionAt(Route, 5.5, speed: 1));
    }
}
