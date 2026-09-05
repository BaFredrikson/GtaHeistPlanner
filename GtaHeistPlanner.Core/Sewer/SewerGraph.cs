namespace GtaHeistPlanner.Core.Sewer;

public sealed record SewerGraph(
    int? StartChamber,
    IReadOnlyList<SewerPath> Paths,
    IReadOnlyList<SewerConnection> Connections,
    string? EntrancePathId = null,
    int ExitChamber = 4,
    string? ExitPathId = null);
