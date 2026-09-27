using System.Text.Json;
using GtaHeistPlanner.Core.Paintings;

namespace GtaHeistPlanner.App.Services;

public sealed class PaintingCollectionStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public PaintingCollectionStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GtaHeistPlanner", "painting-collection.json");
    }

    public string FilePath { get; }

    public PaintingCollectionState Load(PaintingCatalog catalog)
    {
        if (!File.Exists(FilePath)) return new PaintingCollectionState(catalog);
        using var stream = File.OpenRead(FilePath);
        var document = JsonSerializer.Deserialize<PaintingCollectionDocument>(stream, Options)
            ?? throw new InvalidDataException("The painting collection file is empty.");
        if (document.Version != 1) throw new InvalidDataException($"Unsupported painting collection version {document.Version}.");
        return new PaintingCollectionState(catalog, document.CollectedPaintingIds);
    }

    public void Save(PaintingCollectionState state)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(FilePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new PaintingCollectionDocument
                {
                    CollectedPaintingIds = state.CollectedPaintingIds.Order(StringComparer.Ordinal).ToArray(),
                }, Options);
                stream.Flush(true);
            }
            File.Move(temporary, FilePath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private sealed class PaintingCollectionDocument
    {
        public int Version { get; init; } = 1;
        public IReadOnlyList<string> CollectedPaintingIds { get; init; } = [];
    }
}
