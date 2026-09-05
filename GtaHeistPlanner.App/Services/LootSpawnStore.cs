using Avalonia.Platform;
using GtaHeistPlanner.Core.Loot;

namespace GtaHeistPlanner.App.Services;

public sealed class LootSpawnStore
{
    private static readonly Uri BundledCatalog =
        new("avares://GtaHeistPlanner.App/Data/kortz/kortz_loot_spawns.json");

    public LootSpawnStore(string? filePath = null)
    {
        FilePath = filePath ?? ProjectDataPath.KortzFile("kortz_loot_spawns.json");
    }

    public string FilePath { get; }

    public IReadOnlyList<LootSpawnDefinition> Load()
    {
        if (File.Exists(FilePath))
        {
            using var userStream = File.OpenRead(FilePath);
            return LootSpawnJson.Load(userStream).Spawns;
        }

        using var bundledStream = AssetLoader.Open(BundledCatalog);
        return LootSpawnJson.Load(bundledStream).Spawns;
    }

    public void Save(IEnumerable<LootSpawnDefinition> spawns)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        using var stream = File.Create(FilePath);
        LootSpawnJson.Save(stream, spawns.OrderBy(spawn => spawn.MapId).ThenBy(spawn => spawn.Id));
    }
}
