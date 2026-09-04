namespace GtaHeistPlanner.Core.Sewer;

public sealed record SewerTraversalResult(
    bool IsComplete,
    IReadOnlyList<string> VisitedNodeIds,
    IReadOnlyList<SewerConnection> TraversedConnections,
    string? Error);
