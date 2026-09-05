using Avalonia.Platform;
using GtaHeistPlanner.Core.Sewer;

namespace GtaHeistPlanner.App.Services;

public sealed class SewerGraphStore
{
    private static readonly Uri BundledGraph = new("avares://GtaHeistPlanner.App/Data/kortz/kortz_sewer_graph.json");

    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GtaHeistPlanner", "data", "kortz_sewer_graph.json");
    public string? LastLoadWarning { get; private set; }

    public SewerGraph Load()
    {
        LastLoadWarning = null;
        if (File.Exists(FilePath))
        {
            try
            {
                using var stream = File.OpenRead(FilePath);
                return SewerGraphJson.Load(stream);
            }
            catch (InvalidDataException)
            {
                // Old Left/Right topology cannot be translated without inventing chamber labels.
                // Fall back to the intentionally empty bundled schema for manual authoring.
                LastLoadWarning = $"The user sewer topology uses the obsolete node/Left/Right schema. Replace '{FilePath}' with the chamber-letter schema.";
            }
        }
        using var bundled = AssetLoader.Open(BundledGraph);
        return SewerGraphJson.Load(bundled);
    }

    public void Save(SewerGraph graph)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        using var stream = File.Create(FilePath);
        SewerGraphJson.Save(stream, graph);
    }
}
