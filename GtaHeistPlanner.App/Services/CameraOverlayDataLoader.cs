using System.Text.Json;
using GtaHeistPlanner.Core.Overlays;

namespace GtaHeistPlanner.App.Services;

public static class CameraOverlayDataLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static CameraOverlayData LoadKortzExterior()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "kortz", "kortz_exterior_cameras.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("Kortz exterior camera data was not copied to the application output.", path);

        using var stream = File.OpenRead(path);
        return Load(stream, path);
    }

    public static CameraOverlayData Load(Stream stream, string sourceName = "camera JSON")
    {
        var data = JsonSerializer.Deserialize<CameraOverlayData>(stream, Options)
            ?? throw new InvalidDataException($"Camera data '{sourceName}' contains no data.");
        if (string.IsNullOrWhiteSpace(data.MapId))
            throw new InvalidDataException($"Camera data '{sourceName}' has no mapId.");

        var duplicateId = data.Cameras.GroupBy(camera => camera.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateId is not null)
            throw new InvalidDataException($"Camera data '{sourceName}' contains duplicate ID '{duplicateId}'.");

        foreach (var camera in data.Cameras)
        {
            if (string.IsNullOrWhiteSpace(camera.Id))
                throw new InvalidDataException($"Camera data '{sourceName}' contains an empty camera ID.");
            if (!string.Equals(camera.MapId, data.MapId, StringComparison.Ordinal))
                throw new InvalidDataException($"Camera '{camera.Id}' targets '{camera.MapId}', not dataset map '{data.MapId}'.");
            _ = CameraConeGeometry.CreateWorldSpace(camera);
        }
        return data;
    }
}
