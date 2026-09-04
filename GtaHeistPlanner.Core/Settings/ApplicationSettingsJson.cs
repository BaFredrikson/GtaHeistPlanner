using System.Text.Json;

namespace GtaHeistPlanner.Core.Settings;

public static class ApplicationSettingsJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static ApplicationSettings Load(Stream stream) =>
        JsonSerializer.Deserialize<ApplicationSettings>(stream, Options) ?? new ApplicationSettings();

    public static void Save(Stream stream, ApplicationSettings settings) =>
        JsonSerializer.Serialize(stream, settings, Options);
}
