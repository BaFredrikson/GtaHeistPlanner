using GtaHeistPlanner.Core.Settings;

namespace GtaHeistPlanner.App.Services;

public sealed class ApplicationSettingsStore
{
    public ApplicationSettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GtaHeistPlanner", "settings.ini");
        LegacyJsonPath = Path.ChangeExtension(FilePath, ".json");
    }

    public string FilePath { get; }
    public string LegacyJsonPath { get; }

    public ApplicationSettings Load()
    {
        if (File.Exists(FilePath))
        {
            using var reader = File.OpenText(FilePath);
            return ApplicationSettingsIni.Load(reader);
        }
        if (!File.Exists(LegacyJsonPath)) return new ApplicationSettings();
        using var legacy = File.OpenRead(LegacyJsonPath);
        var migrated = ApplicationSettingsJson.Load(legacy);
        try { Save(migrated); }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
        return migrated;
    }

    public void Save(ApplicationSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        IniDocument document;
        if (File.Exists(FilePath))
        {
            using var reader = File.OpenText(FilePath);
            document = IniDocument.Load(reader);
        }
        else document = new IniDocument();
        ApplicationSettingsIni.Apply(document, settings);
        using var writer = File.CreateText(FilePath);
        document.Save(writer);
    }
}
