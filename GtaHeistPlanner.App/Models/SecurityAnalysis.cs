using System.Text.Json.Serialization;

namespace GtaHeistPlanner.App.Models;

public sealed record SecurityAnalysis(
    string Source,
    AnalysisGraph Graph,
    IReadOnlyList<AnalysisGuardChain> GuardChains,
    IReadOnlyList<AnalysisScenarioPoint> SecurityScenarioPoints);

public sealed record AnalysisGraph(
    IReadOnlyList<AnalysisNode> Nodes,
    IReadOnlyList<AnalysisEdge> Edges,
    IReadOnlyList<AnalysisChain> Chains);

public sealed record AnalysisNode(int NodeIndex, AnalysisCoordinate Position,
    string ScenarioType, bool HasIncomingEdges, bool HasOutgoingEdges);

public sealed record AnalysisEdge(int EdgeIndex, int NodeIndexFrom, int NodeIndexTo,
    string Action, string NavMode, string NavSpeed);

public sealed record AnalysisChain(int ChainIndex, string? RawChainValue,
    IReadOnlyList<int> EdgeIds, IReadOnlyList<int> NodeIndices);

public sealed record AnalysisGuardChain(int ChainIndex, string? RawChainValue,
    IReadOnlyList<int> EdgeIds, IReadOnlyList<int> NodeIndices,
    IReadOnlyList<AnalysisNode> Nodes, IReadOnlyList<AnalysisEdge> Edges);

public sealed record AnalysisScenarioPoint(int SourcePointIndex, string ScenarioType,
    string ModelSet, AnalysisCoordinate Position, double W, double Direction,
    AnalysisTimeRange TimeRange, int TimeTillPedLeaves, IReadOnlyList<string> Flags);

public sealed record AnalysisCoordinate(double X, double Y, double Z);

public sealed record AnalysisTimeRange(int Start, int End);
