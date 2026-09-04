using GtaHeistPlanner.Core.Security;

namespace GtaHeistPlanner.Tests.Security;

public sealed class PatrolRouteEditorTests
{
    private static readonly PatrolWaypoint[] Route = [new(.1, .1), new(.2, .2), new(.3, .3)];

    [Fact]
    public void MoveReplacesOnlySelectedNodeAndPreservesOrder()
    {
        var moved = PatrolRouteEditor.Move(Route, 1, new(.8, .7));

        Assert.Equal(Route[0], moved[0]);
        Assert.Equal(new PatrolWaypoint(.8, .7), moved[1]);
        Assert.Equal(Route[2], moved[2]);
    }

    [Fact]
    public void DeleteMiddleReconnectsItsNeighborsByPreservingTheirOrder() =>
        Assert.Equal([Route[0], Route[2]], PatrolRouteEditor.Delete(Route, 1));

    [Theory]
    [InlineData(0, 0.2, 0.3)]
    [InlineData(2, 0.1, 0.2)]
    public void DeleteEndpointKeepsRemainingRouteOrdered(int index, double firstX, double lastX)
    {
        var remaining = PatrolRouteEditor.Delete(Route, index);
        Assert.Equal(firstX, remaining[0].X);
        Assert.Equal(lastX, remaining[^1].X);
    }
}
