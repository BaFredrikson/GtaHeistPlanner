using GtaHeistPlanner.Core.Maps;
using GtaHeistPlanner.Core.Sewer;

namespace GtaHeistPlanner.Tests.Sewer;

public sealed class SewerRouteTests
{
    [Theory]
    [InlineData("2C 3C 4D")]
    [InlineData("2 C 3 C 4 D")]
    [InlineData("2C, 3C, 4D")]
    [InlineData("chamber 2 C, chamber 3 C, chamber 4 D")]
    public void ParsesCommonRouteForms(string text) =>
        Assert.Equal([new SewerInstruction(2, 'C'), new(3, 'C'), new(4, 'D')], SewerRouteParser.Parse(text));

    [Fact]
    public void ParserNormalizesLowercaseTunnelLetters() =>
        Assert.Equal('C', SewerRouteParser.Parse("2c")[0].Tunnel);

    [Fact]
    public void BidirectionalLabelsResolveSamePhysicalPathWithOppositeDestinations()
    {
        var graph = Graph();

        var fromFive = SewerConnectionResolver.Resolve(graph, new(5, 'B'));
        var fromSix = SewerConnectionResolver.Resolve(graph, new(6, 'b'));

        Assert.Same(fromFive.Connection, fromSix.Connection);
        Assert.Equal("sewer-path-04", fromFive.PathId);
        Assert.Equal(fromFive.PathId, fromSix.PathId);
        Assert.Equal(6, fromFive.DestinationChamber);
        Assert.Equal(5, fromSix.DestinationChamber);
    }

    [Fact]
    public void ResolvesSequentialRouteAndPhysicalPathsInOrder()
    {
        var result = SewerGraphTraversal.Traverse(Graph(), SewerRouteParser.Parse("2C 3C"));

        Assert.True(result.IsComplete);
        Assert.Equal(["sewer-entrance", "sewer-path-01", "sewer-path-02", "sewer-exit"], result.PathIds);
        Assert.False(result.Steps[^1].IsExit);
        Assert.Null(result.CurrentChamber);
    }

    [Fact]
    public void InvalidChamberProgressionReturnsExactDiagnostic()
    {
        var result = SewerGraphTraversal.Traverse(Graph(), SewerRouteParser.Parse("2C 5B"));

        Assert.False(result.IsComplete);
        Assert.Equal("Expected instruction for Chamber 3 but received Chamber 5.", result.Error);
        Assert.Equal(["sewer-entrance", "sewer-path-01"], result.PathIds);
    }

    [Fact]
    public void ExitInstructionResolvesWithoutDestination()
    {
        var step = SewerConnectionResolver.Resolve(Graph(), new(4, 'D'));

        Assert.True(step.IsExit);
        Assert.Null(step.DestinationChamber);
        Assert.Equal("sewer-path-03", step.PathId);
    }

    [Theory]
    [InlineData('A', "always closed")]
    [InlineData('B', "dead end")]
    public void ClosedAndDeadEndInstructionsAreRejected(char tunnel, string diagnostic)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            SewerConnectionResolver.Resolve(Graph(), new(2, tunnel)));

        Assert.Contains(diagnostic, error.Message);
    }

    [Fact]
    public void UnknownInstructionIsRejectedClearly()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            SewerConnectionResolver.Resolve(Graph(), new(9, 'Z')));

        Assert.Equal("Unknown sewer instruction 9Z.", error.Message);
    }

    [Fact]
    public void JsonRoundTripPreservesDirectionalLabelsAndPathGeometry()
    {
        using var stream = new MemoryStream();
        SewerGraphJson.Save(stream, Graph());
        stream.Position = 0;

        var loaded = SewerGraphJson.Load(stream);

        Assert.Equal(2, loaded.StartChamber);
        Assert.Equal("sewer-path-04", loaded.Connections.Single(item => item.Id == "edge-5-6").PathId);
        Assert.Equal('B', loaded.Connections.Single(item => item.Id == "edge-5-6").TunnelFromB);
        Assert.Equal("sewer-entrance", loaded.EntrancePathId);
        Assert.Equal("sewer-exit", loaded.ExitPathId);
    }

    private static SewerGraph Graph() => new(2,
        [Path("sewer-path-01"), Path("sewer-path-02"), Path("sewer-path-03"), Path("sewer-path-04"), Path("sewer-path-05"), Path("sewer-path-06"), Path("sewer-entrance"), Path("sewer-exit")],
        [
            new("edge-2-3", 2, 'C', 3, 'A', "sewer-path-01"),
            new("edge-3-4", 3, 'C', 4, 'A', "sewer-path-02"),
            new("exit-4d", 4, 'D', null, null, "sewer-path-03", SewerTunnelState.Exit),
            new("edge-5-6", 5, 'B', 6, 'B', "sewer-path-04"),
            new("closed-2a", 2, 'A', null, null, "sewer-path-05", SewerTunnelState.AlwaysClosed),
            new("dead-2b", 2, 'B', null, null, "sewer-path-06", SewerTunnelState.DeadEnd),
        ], "sewer-entrance", 4, "sewer-exit");

    private static SewerPath Path(string id) => new(id, [new MapPoint(.1, .1), new(.9, .9)]);
}
