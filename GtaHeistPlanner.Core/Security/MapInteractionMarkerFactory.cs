namespace GtaHeistPlanner.Core.Security;

public static class MapInteractionMarkerFactory
{
    public static MapInteractionMarker Create(
        MapInteractionMarkerType type,
        string mapId,
        double x,
        double y,
        IEnumerable<string> existingIds)
    {
        if (string.IsNullOrWhiteSpace(mapId)) throw new ArgumentException("A map ID is required.", nameof(mapId));
        if (x is < 0 or > 1 || y is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(x), "Marker coordinates must be normalized between 0 and 1.");

        var prefix = type.ToString().ToLowerInvariant();
        var used = existingIds.ToHashSet(StringComparer.Ordinal);
        var sequence = 1;
        string id;
        do id = $"{mapId}-{prefix}-{sequence++:00}"; while (used.Contains(id));
        return new(id, mapId, type, x, y, MapInteractionMarkerIcons.DefaultDisplayName(type));
    }
}
