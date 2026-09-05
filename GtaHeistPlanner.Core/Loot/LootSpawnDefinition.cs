namespace GtaHeistPlanner.Core.Loot;

public sealed record LootSpawnDefinition(
    string Id,
    string MapId,
    string Name,
    LootType Type,
    double X,
    double Y,
    string? ZoneId = null)
{
    public IReadOnlyList<string> VoiceAliases { get; init; } = [];
}
