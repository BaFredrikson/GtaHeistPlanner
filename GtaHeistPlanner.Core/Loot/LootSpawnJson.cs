using System.Text.Json;
using System.Text.Json.Serialization;

namespace GtaHeistPlanner.Core.Loot;

public static class LootSpawnJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new LootTypeJsonConverter() },
    };

    public static LootSpawnCatalog Load(Stream stream)
    {
        var catalog = JsonSerializer.Deserialize<LootSpawnCatalog>(stream, Options)
            ?? throw new InvalidDataException("Loot spawn JSON was empty.");
        Validate(catalog.Spawns);
        return catalog;
    }

    public static void Save(Stream stream, IEnumerable<LootSpawnDefinition> spawns)
    {
        var spawnList = spawns.ToList();
        Validate(spawnList);
        JsonSerializer.Serialize(stream, new LootSpawnCatalog(spawnList), Options);
    }

    private static void Validate(IReadOnlyList<LootSpawnDefinition> spawns)
    {
        if (spawns.Any(spawn => string.IsNullOrWhiteSpace(spawn.Id) || string.IsNullOrWhiteSpace(spawn.MapId)))
            throw new InvalidDataException("Every loot spawn requires an ID and map ID.");
        if (spawns.Any(spawn => spawn.X is < 0 or > 1 || spawn.Y is < 0 or > 1))
            throw new InvalidDataException("Loot coordinates must be between 0 and 1.");
        if (spawns.Select(spawn => spawn.Id).Distinct(StringComparer.Ordinal).Count() != spawns.Count)
            throw new InvalidDataException("Loot spawn IDs must be unique.");
        foreach (var spawn in spawns)
            _ = LootZoneCatalog.Get(spawn.ZoneId);
    }
}
