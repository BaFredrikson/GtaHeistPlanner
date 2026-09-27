namespace GtaHeistPlanner.Core.Paintings;

public sealed class PaintingCollectionState
{
    private readonly PaintingCatalog _catalog;
    private readonly HashSet<string> _collected;

    public PaintingCollectionState(PaintingCatalog catalog, IEnumerable<string>? collectedPaintingIds = null)
    {
        _catalog = catalog;
        var targetIds = catalog.HeistTargets.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        _collected = (collectedPaintingIds ?? []).Where(targetIds.Contains).ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlyCollection<string> CollectedPaintingIds => _collected;
    public int CollectedCount => _collected.Count;
    public int RequiredCount => _catalog.HeistTargets.Count;
    public bool IsComplete => CollectedCount == RequiredCount;
    public bool IsRewardUnlocked => IsComplete;
    public bool IsCollected(string id) => _collected.Contains(id);
    public bool MarkCollected(string id)
    {
        var painting = _catalog.Find(id) ?? throw new ArgumentException($"Unknown painting ID '{id}'.", nameof(id));
        if (painting.AcquisitionType != PaintingAcquisitionType.HeistTarget)
            throw new InvalidOperationException("The collection reward is not a required collectible painting.");
        return _collected.Add(id);
    }
    public bool SetCollected(string id, bool collected) => collected ? MarkCollected(id) : _collected.Remove(id);
}

public enum PrimaryTargetDisposition { Sell, Keep }

public static class PaintingCollectionCompletion
{
    public static bool Apply(PaintingCollectionState collection, string? paintingId, PrimaryTargetDisposition disposition) =>
        disposition == PrimaryTargetDisposition.Keep && paintingId is not null && collection.MarkCollected(paintingId);
}
