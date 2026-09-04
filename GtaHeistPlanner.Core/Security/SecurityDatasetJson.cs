using System.Text.Json;

namespace GtaHeistPlanner.Core.Security;

public static class SecurityDatasetJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static SecurityDataset Load(Stream stream)
    {
        var dataset = JsonSerializer.Deserialize<SecurityDataset>(stream, Options)
            ?? throw new InvalidDataException("Security dataset JSON was empty.");
        Validate(dataset);
        return dataset;
    }

    public static void Save(Stream stream, SecurityDataset dataset)
    {
        Validate(dataset);
        JsonSerializer.Serialize(stream, dataset, Options);
    }

    public static void Validate(SecurityDataset dataset)
    {
        var allIds = dataset.Cameras.Select(item => item.Id)
            .Concat(dataset.Guards.Select(item => item.Id))
            .Concat(dataset.Patrols.Select(item => item.Id)).ToList();
        if (allIds.Any(string.IsNullOrWhiteSpace) || allIds.Distinct(StringComparer.Ordinal).Count() != allIds.Count)
            throw new InvalidDataException("Security object IDs must be non-empty and globally unique.");

        foreach (var camera in dataset.Cameras)
        {
            ValidatePoint(camera.MapId, camera.X, camera.Y, camera.Id);
            if (camera.FovDegrees is <= 0 or >= 180)
                throw new InvalidDataException($"Camera '{camera.Id}' FOV must be between 0 and 180 degrees.");
            if (camera.Range is <= 0 or > 2)
                throw new InvalidDataException($"Camera '{camera.Id}' range must be between 0 and 2 normalized map units.");
        }
        var patrols = dataset.Patrols.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var patrol in dataset.Patrols)
            foreach (var waypoint in patrol.Waypoints)
                ValidatePoint(patrol.MapId, waypoint.X, waypoint.Y, patrol.Id);
        foreach (var guard in dataset.Guards)
        {
            ValidatePoint(guard.MapId, guard.X, guard.Y, guard.Id);
            foreach (var patrolId in guard.PatrolIds)
                if (!patrols.TryGetValue(patrolId, out var patrol) || patrol.MapId != guard.MapId)
                    throw new InvalidDataException($"Guard '{guard.Id}' references missing or cross-map patrol '{patrolId}'.");
        }
    }

    private static void ValidatePoint(string mapId, double x, double y, string id)
    {
        if (string.IsNullOrWhiteSpace(mapId))
            throw new InvalidDataException($"Security object '{id}' requires a map ID.");
        if (x is < 0 or > 1 || y is < 0 or > 1)
            throw new InvalidDataException($"Security object '{id}' coordinates must be normalized between 0 and 1.");
    }
}
