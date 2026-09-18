namespace GtaHeistPlanner.Core.Security;

public sealed record SecurityGuardDefinition(
    string Id,
    string MapId,
    string Name,
    double X,
    double Y,
    IReadOnlyList<string> PatrolIds)
{
    public IReadOnlyList<string> VoiceAliases { get; init; } = [];
}
