namespace GtaHeistPlanner.Core.Sewer;

public sealed record SewerTraversalResult(
    bool IsComplete,
    int? CurrentChamber,
    IReadOnlyList<SewerResolvedStep> Steps,
    IReadOnlyList<string> PathIds,
    string? Error);
