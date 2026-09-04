namespace GtaHeistPlanner.Core.Sewer;

public sealed record SewerGraph(
    string? StartNodeId,
    IReadOnlyList<SewerNode> Nodes,
    IReadOnlyList<SewerConnection> Connections);
