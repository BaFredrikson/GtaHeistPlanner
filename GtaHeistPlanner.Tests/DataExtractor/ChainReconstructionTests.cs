using GtaHeistPlanner.DataExtractor;

namespace GtaHeistPlanner.Tests.DataExtractor;

public sealed class ChainReconstructionTests
{
    [Fact]
    public void ReconstructNodeIndices_FollowsOrderedEdges()
    {
        GraphEdge[] edges =
        [
            new(0, 4, 7, "0", "0", "1"),
            new(1, 7, 9, "0", "0", "1"),
        ];

        var nodes = ScenarioRegionExtractor.ReconstructNodeIndices([0, 1], edges, 3);

        Assert.Equal([4, 7, 9], nodes);
    }

    [Fact]
    public void ReconstructNodeIndices_PreservesFirstEncounterOrderForBranches()
    {
        GraphEdge[] edges =
        [
            new(0, 4, 7, "0", "0", "1"),
            new(1, 8, 9, "0", "0", "1"),
        ];

        var nodes = ScenarioRegionExtractor.ReconstructNodeIndices([0, 1], edges, 3);

        Assert.Equal([4, 7, 8, 9], nodes);
    }

    [Fact]
    public void ReconstructNodeIndices_CanTraverseEdgeAgainstStoredDirection()
    {
        GraphEdge[] edges =
        [
            new(0, 4, 7, "0", "0", "1"),
            new(1, 9, 7, "0", "0", "1"),
        ];

        var nodes = ScenarioRegionExtractor.ReconstructNodeIndices([0, 1], edges, 3);

        Assert.Equal([4, 7, 9], nodes);
    }

    [Fact]
    public void ReconstructNodeIndices_ThrowsForInvalidEdgeReference()
    {
        GraphEdge[] edges = [new(0, 4, 7, "0", "0", "1")];

        Assert.Throws<InvalidDataException>(() =>
            ScenarioRegionExtractor.ReconstructNodeIndices([1], edges, 3));
    }
}
