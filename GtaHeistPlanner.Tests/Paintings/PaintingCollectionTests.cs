using GtaHeistPlanner.App.Services;
using GtaHeistPlanner.Core.Paintings;
using GtaHeistPlanner.Core.Planning;

namespace GtaHeistPlanner.Tests.Paintings;

public sealed class PaintingCollectionTests
{
    [Fact]
    public void AuthoredCatalog_HasExpectedTargetsRewardAndUtf8Title()
    {
        var catalog = LoadAuthoredCatalog();

        Assert.Equal(27, catalog.Paintings.Count);
        Assert.Equal(26, catalog.HeistTargets.Count);
        Assert.Equal(PaintingAcquisitionType.CollectionReward, catalog.Reward.AcquisitionType);
        Assert.Equal("La Dernière Débauche", catalog.Reward.Name);
        Assert.Equal(27, catalog.Paintings.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Pricing_DerivesWeeklyAndPreservesUnknown()
    {
        Assert.Equal(1_372_800, PaintingPricing.FirstWeeklySaleValue(343_200));
        Assert.Null(PaintingPricing.FirstWeeklySaleValue(null));
    }

    [Fact]
    public void Collection_DeduplicatesProgressAndUnlocksRewardOnlyAtTwentySix()
    {
        var catalog = LoadAuthoredCatalog();
        var collection = new PaintingCollectionState(catalog);
        Assert.Equal(0, collection.CollectedCount);
        Assert.Equal(26, collection.RequiredCount);

        foreach (var painting in catalog.HeistTargets.Take(25)) Assert.True(collection.MarkCollected(painting.Id));
        Assert.False(collection.MarkCollected(catalog.HeistTargets[0].Id));
        Assert.Equal(25, collection.CollectedCount);
        Assert.False(collection.IsRewardUnlocked);

        Assert.True(collection.MarkCollected(catalog.HeistTargets[25].Id));
        Assert.True(collection.IsRewardUnlocked);
        Assert.Throws<InvalidOperationException>(() => collection.MarkCollected(catalog.Reward.Id));
        Assert.Equal(26, collection.CollectedCount);
    }

    [Fact]
    public void CollectionStore_PersistsSeparatelyAndSurvivesHeistDeletion()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"GtaHeistPlanner-paintings-{Guid.NewGuid():N}");
        try
        {
            var collectionPath = Path.Combine(directory, "painting-collection.json");
            var heistPath = Path.Combine(directory, "heists", "current-heist.json");
            var catalog = LoadAuthoredCatalog();
            var store = new PaintingCollectionStore(collectionPath);
            var collection = new PaintingCollectionState(catalog);
            collection.MarkCollected("i-fruit");
            store.Save(collection);

            var heistStore = new HeistSessionStore(heistPath);
            heistStore.Save(CreateSave("i-fruit", PrimaryTargetDisposition.Keep));
            heistStore.DeleteCurrent();

            Assert.NotEqual(Path.GetDirectoryName(heistPath), Path.GetDirectoryName(collectionPath));
            Assert.True(store.Load(catalog).IsCollected("i-fruit"));
            Assert.False(heistStore.Exists);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void PlanningIntent_PersistsButDoesNotCollectUntilCompletion()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"GtaHeistPlanner-painting-plan-{Guid.NewGuid():N}");
        try
        {
            var catalog = LoadAuthoredCatalog();
            var collection = new PaintingCollectionState(catalog);
            var store = new HeistSessionStore(Path.Combine(directory, "current-heist.json"));
            store.Save(CreateSave("i-fruit", PrimaryTargetDisposition.Keep));
            var loaded = store.Load();

            Assert.Equal("I, Fruit", catalog.Find(loaded.CurrentPrimaryPaintingId)?.Name);
            Assert.Equal(PrimaryTargetDisposition.Keep, loaded.PrimaryTargetDisposition);
            Assert.False(collection.IsCollected("i-fruit"));
            Assert.True(PaintingCollectionCompletion.Apply(collection, loaded.CurrentPrimaryPaintingId, loaded.PrimaryTargetDisposition));
            Assert.True(collection.IsCollected("i-fruit"));
            Assert.False(PaintingCollectionCompletion.Apply(collection, loaded.CurrentPrimaryPaintingId, loaded.PrimaryTargetDisposition));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void SellingDoesNotAdvanceCollection()
    {
        var collection = new PaintingCollectionState(LoadAuthoredCatalog());
        Assert.False(PaintingCollectionCompletion.Apply(collection, "i-fruit", PrimaryTargetDisposition.Sell));
        Assert.Equal(0, collection.CollectedCount);
    }

    private static PaintingCatalog LoadAuthoredCatalog()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "GtaHeistPlanner.App", "Data", "kortz", "kortz_paintings.json"));
        using var stream = File.OpenRead(path);
        return PaintingCatalog.Load(stream);
    }

    private static HeistSaveFile CreateSave(string id, PrimaryTargetDisposition disposition) => new()
    {
        HeistId = Guid.NewGuid(),
        CreatedAtUtc = DateTimeOffset.UtcNow,
        LastSavedAtUtc = DateTimeOffset.UtcNow,
        CurrentPrimaryPaintingId = id,
        PrimaryTargetDisposition = disposition,
    };
}
