namespace GtaHeistPlanner.Core.Sewer;

public sealed record SewerConnection(string FromNodeId, string ToNodeId, SewerTurn Turn);
