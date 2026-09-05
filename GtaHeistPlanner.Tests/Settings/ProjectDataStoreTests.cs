using GtaHeistPlanner.App.Services;
using GtaHeistPlanner.Core.Loot;
using GtaHeistPlanner.Core.Security;

namespace GtaHeistPlanner.Tests.Settings;

public sealed class ProjectDataStoreTests
{
    [Fact]
    public void DefaultPathsPointAtProjectKortzData()
    {
        Assert.EndsWith(
            Path.Combine("GtaHeistPlanner.App", "Data", "kortz", "kortz_loot_spawns.json"),
            new LootSpawnStore().FilePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(
            Path.Combine("GtaHeistPlanner.App", "Data", "kortz", "kortz_security.json"),
            new SecurityDatasetStore().FilePath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StoresSaveAndReloadAtExplicitPaths()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"GtaHeistPlanner.Tests-{Guid.NewGuid():N}");
        try
        {
            var lootPath = Path.Combine(directory, "loot.json");
            var securityPath = Path.Combine(directory, "security.json");
            var lootStore = new LootSpawnStore(lootPath);
            var securityStore = new SecurityDatasetStore(securityPath);

            lootStore.Save(Array.Empty<LootSpawnDefinition>());
            securityStore.Save(new SecurityDataset([], [], []));

            Assert.Empty(lootStore.Load());
            var security = securityStore.Load();
            Assert.Empty(security.Cameras);
            Assert.Empty(security.Guards);
            Assert.Empty(security.Patrols);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
