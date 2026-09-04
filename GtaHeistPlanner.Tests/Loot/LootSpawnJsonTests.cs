using System.Text;
using GtaHeistPlanner.Core.Loot;

namespace GtaHeistPlanner.Tests.Loot;

public sealed class LootSpawnJsonTests
{
    [Fact]
    public void RoundTripPreservesDefinitionsAndUsesStringEnum()
    {
        var expected = new LootSpawnDefinition("main-west-01", "main-floor", "West Gallery 01", LootType.Painting, .42, .31);
        using var stream = new MemoryStream();

        LootSpawnJson.Save(stream, [expected]);
        var json = Encoding.UTF8.GetString(stream.ToArray());
        stream.Position = 0;
        var actual = LootSpawnJson.Load(stream);

        Assert.Contains("\"type\": \"Painting\"", json);
        Assert.Equal(expected, Assert.Single(actual.Spawns));
    }

    [Fact]
    public void LoadMigratesLegacyGenericWithoutChangingLocation()
    {
        const string json = """
            { "spawns": [{ "id": "kept", "mapId": "upper-floor", "name": "Loot 01", "type": "Generic", "x": 0.271, "y": 0.786 }] }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var spawn = Assert.Single(LootSpawnJson.Load(stream).Spawns);

        Assert.Equal(LootType.CoquardJewelry, spawn.Type);
        Assert.Equal("kept", spawn.Id);
        Assert.Equal("upper-floor", spawn.MapId);
        Assert.Equal(0.271, spawn.X);
        Assert.Equal(0.786, spawn.Y);
    }

    [Fact]
    public void ChangingCategoryPreservesAuthoredLocation()
    {
        var original = new LootSpawnDefinition("kept", "main-floor", "Loot 01", LootType.CoquardJewelry, .42, .31);

        var changed = original with { Type = LootType.LoadingBayCargo };

        Assert.Equal(original.Id, changed.Id);
        Assert.Equal(original.MapId, changed.MapId);
        Assert.Equal(original.X, changed.X);
        Assert.Equal(original.Y, changed.Y);
    }

    [Fact]
    public void LoadRejectsCoordinatesOutsideNormalizedMapSpace()
    {
        const string json = """
            { "spawns": [{ "id": "bad", "mapId": "main-floor", "name": "Bad", "type": "Generic", "x": 1.1, "y": 0.5 }] }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        Assert.Throws<InvalidDataException>(() => LootSpawnJson.Load(stream));
    }
}
