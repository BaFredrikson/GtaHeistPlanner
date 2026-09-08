using System.Text.Json;
using System.Text.Json.Serialization;
using GtaHeistPlanner.Core.Planning;

namespace GtaHeistPlanner.App.Services;

public sealed class HeistSessionStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public HeistSessionStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GtaHeistPlanner", "heists", "current-heist.json");
    }

    public string FilePath { get; }
    public bool Exists => File.Exists(FilePath);

    public void Save(HeistSaveFile session)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(FilePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, session, Options);
                stream.Flush(true);
            }
            File.Move(temporaryPath, FilePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public HeistSaveFile Load(string? sourcePath = null)
    {
        using var stream = File.OpenRead(sourcePath ?? FilePath);
        var save = JsonSerializer.Deserialize<HeistSaveFile>(stream, Options)
            ?? throw new InvalidDataException("The heist save is empty.");
        Validate(save);
        return save;
    }

    public void DeleteCurrent() { if (File.Exists(FilePath)) File.Delete(FilePath); }

    private static void Validate(HeistSaveFile save)
    {
        if (save.SchemaVersion != HeistSaveFile.CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported heist save schema {save.SchemaVersion}; expected {HeistSaveFile.CurrentSchemaVersion}.");
        if (save.HeistId == Guid.Empty) throw new InvalidDataException("The heist save has no valid heist ID.");
        if (save.CreatedAtUtc == default || save.LastSavedAtUtc == default)
            throw new InvalidDataException("Saved heist timestamps are invalid.");
        if (save.PlayerCount is < 1 or > 4) throw new InvalidDataException("Saved player count must be between 1 and 4.");
        if (!Enum.IsDefined(save.Stage)) throw new InvalidDataException("Saved planner stage is invalid.");
        if (save.GuardsDown < 0 || save.CamerasDown is < 0 or > HeistSessionState.CameraDisableLimit)
            throw new InvalidDataException("Saved guard/camera counters are invalid.");
        if (save.LootStates.Count(entry => entry.Value.IsBuyersRequest) > GtaHeistPlanner.Core.Loot.LootRunState.BuyersRequestLimit)
            throw new InvalidDataException("Saved Buyer's Request count exceeds the heist limit.");
    }
}
