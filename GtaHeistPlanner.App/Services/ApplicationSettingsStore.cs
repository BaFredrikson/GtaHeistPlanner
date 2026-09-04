using GtaHeistPlanner.Core.Settings;

namespace GtaHeistPlanner.App.Services;

public sealed class ApplicationSettingsStore
{
    public ApplicationSettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GtaHeistPlanner", "settings.json");
    }

    public string FilePath { get; }

    public ApplicationSettings Load()
    {
        if (!File.Exists(FilePath))
            return new ApplicationSettings();
        using var stream = File.OpenRead(FilePath);
        return ApplicationSettingsJson.Load(stream);
    }

    public void Save(ApplicationSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        using var stream = File.Create(FilePath);
        ApplicationSettingsJson.Save(stream, settings);
    }
}
