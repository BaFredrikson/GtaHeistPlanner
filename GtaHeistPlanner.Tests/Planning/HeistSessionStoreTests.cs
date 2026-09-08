using System.Text.Json;
using GtaHeistPlanner.App.Services;
using GtaHeistPlanner.Core.Planning;

namespace GtaHeistPlanner.Tests.Planning;

public sealed class HeistSessionStoreTests
{
    [Fact]
    public void SaveAndLoadRoundTripVersionedRuntimeOnlyData()
    {
        var path = TempPath();
        var store = new HeistSessionStore(path);
        store.Save(SaveFile());
        var loaded = store.Load();

        Assert.Equal(HeistSaveFile.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal(2, loaded.PlayerCount);
        Assert.Equal(PlannerStage.HeistActivity, loaded.Stage);
        Assert.Equal(118000, loaded.LootStates["west"].ScopedValue);
        Assert.Equal(["entrance", "route-a"], loaded.SewerRuntimeState.HighlightedPathIds);
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.False(json.RootElement.TryGetProperty("OpenAiApiKey", out _));
        Assert.False(json.RootElement.TryGetProperty("RecordingDeviceId", out _));
        Assert.False(json.RootElement.GetProperty("LootStates").GetProperty("west").TryGetProperty("X", out _));
    }

    [Fact]
    public void LoadingArchiveDoesNotModifyOrDeleteSource()
    {
        var current = TempPath();
        var archive = TempPath();
        new HeistSessionStore(archive).Save(SaveFile());
        var original = File.ReadAllText(archive);
        var loaded = new HeistSessionStore(current).Load(archive);
        Assert.Equal(2, loaded.PlayerCount);
        Assert.True(File.Exists(archive));
        Assert.Equal(original, File.ReadAllText(archive));
    }

    [Fact]
    public void CorruptFileFailsClearlyWithoutBeingOverwritten()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{not json");
        var original = File.ReadAllText(path);
        Assert.Throws<JsonException>(() => new HeistSessionStore(path).Load());
        Assert.Equal(original, File.ReadAllText(path));
    }

    private static HeistSaveFile SaveFile() => new()
    {
        HeistId = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UtcNow, LastSavedAtUtc = DateTimeOffset.UtcNow,
        PlayerCount = 2, Stage = PlannerStage.HeistActivity, VaultCode = "46-18-73", GuardsDown = 3, CamerasDown = 1,
        LootStates = new() { ["west"] = new(true, 118000, true, true) },
        SewerRuntimeState = new() { RouteInput = "2C", IsRouteComplete = true, HighlightedPathIds = ["entrance", "route-a"] },
    };

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"gta-heist-{Guid.NewGuid():N}", "current-heist.json");
}
