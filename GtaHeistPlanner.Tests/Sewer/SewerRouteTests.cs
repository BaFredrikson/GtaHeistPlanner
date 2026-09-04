using GtaHeistPlanner.Core.Sewer;

namespace GtaHeistPlanner.Tests.Sewer;

public sealed class SewerRouteTests
{
    [Theory]
    [InlineData("L R R L")]
    [InlineData("left right right left")]
    [InlineData("Escape route left right right left")]
    public void ParsesConstrainedTurnSequence(string text)
    {
        var route = SewerRouteParser.Parse(text);
        Assert.Equal([SewerTurn.Left, SewerTurn.Right, SewerTurn.Right, SewerTurn.Left], route.Turns);
    }

    [Fact]
    public void TraversesConfiguredConnectionsInOrder()
    {
        var graph = Graph();

        var result = SewerGraphTraversal.Traverse(graph, new SewerRoute([SewerTurn.Left, SewerTurn.Right]));

        Assert.True(result.IsComplete);
        Assert.Equal(["entrance", "a", "exit"], result.VisitedNodeIds);
        Assert.Equal(2, result.TraversedConnections.Count);
    }

    [Fact]
    public void MissingConnectionStopsAtLastResolvedNode()
    {
        var result = SewerGraphTraversal.Traverse(Graph(), new SewerRoute([SewerTurn.Right]));

        Assert.False(result.IsComplete);
        Assert.Equal(["entrance"], result.VisitedNodeIds);
        Assert.Empty(result.TraversedConnections);
        Assert.Contains("No Right connection", result.Error);
    }

    [Fact]
    public void MissingStartNodeReturnsDiagnosticInsteadOfGuessing()
    {
        var graph = new SewerGraph(null, [], []);

        var result = SewerGraphTraversal.Traverse(graph, new SewerRoute([SewerTurn.Left]));

        Assert.False(result.IsComplete);
        Assert.Contains("start node", result.Error);
    }

    private static SewerGraph Graph() => new(
        "entrance",
        [new("entrance", .1, .1), new("a", .3, .3), new("exit", .7, .7)],
        [new("entrance", "a", SewerTurn.Left), new("a", "exit", SewerTurn.Right)]);
}
