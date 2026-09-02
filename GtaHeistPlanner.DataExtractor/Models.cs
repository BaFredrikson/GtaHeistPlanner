namespace GtaHeistPlanner.DataExtractor;

public sealed record ExtractionResult(string Source, ChainingGraph Graph,
    IReadOnlyList<GuardChain> GuardChains,
    IReadOnlyList<SecurityScenarioPoint> SecurityScenarioPoints);

public sealed record ChainingGraph(IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges, IReadOnlyList<GraphChain> Chains);

public sealed record GraphNode(int NodeIndex, Coordinate Position, string ScenarioType,
    bool HasIncomingEdges, bool HasOutgoingEdges);

public sealed record GraphEdge(int EdgeIndex, int NodeIndexFrom, int NodeIndexTo,
    string Action, string NavMode, string NavSpeed);

public sealed record GraphChain(int ChainIndex, string? RawChainValue,
    IReadOnlyList<int> EdgeIds, IReadOnlyList<int> NodeIndices);

public sealed record GuardChain(int ChainIndex, string? RawChainValue,
    IReadOnlyList<int> EdgeIds, IReadOnlyList<int> NodeIndices,
    IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges);

public sealed record SecurityScenarioPoint(int SourcePointIndex, string ScenarioType,
    string ModelSet, Coordinate Position, double W, double Direction,
    TimeRange TimeRange, int TimeTillPedLeaves, IReadOnlyList<string> Flags);

public sealed record Coordinate(double X, double Y, double Z);

public sealed record TimeRange(int Start, int End);
