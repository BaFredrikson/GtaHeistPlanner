using System.Text.Json;
using GtaHeistPlanner.Core.Maps;

namespace GtaHeistPlanner.App.Services;

public sealed class MapCalibrationStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GtaHeistPlanner", "config", "map-calibrations.json");

    public MapCalibration? Load(string mapId)
    {
        if (!File.Exists(FilePath))
            return null;
        using var stream = File.OpenRead(FilePath);
        var calibrations = JsonSerializer.Deserialize<Dictionary<string, MapCalibration>>(stream, Options);
        return calibrations?.GetValueOrDefault(mapId);
    }

    public void Save(string mapId, MapCalibration calibration)
    {
        Dictionary<string, MapCalibration> calibrations;
        if (File.Exists(FilePath))
        {
            using var input = File.OpenRead(FilePath);
            calibrations = JsonSerializer.Deserialize<Dictionary<string, MapCalibration>>(input, Options) ?? [];
        }
        else
        {
            calibrations = [];
        }

        calibrations[mapId] = calibration;
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        using var output = File.Create(FilePath);
        JsonSerializer.Serialize(output, calibrations, Options);
    }
}
