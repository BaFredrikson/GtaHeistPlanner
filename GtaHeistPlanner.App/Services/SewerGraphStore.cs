using Avalonia.Platform;
using GtaHeistPlanner.Core.Sewer;

namespace GtaHeistPlanner.App.Services;

public sealed class SewerGraphStore
{
    private static readonly Uri BundledGraph = new("avares://GtaHeistPlanner.App/Data/kortz/kortz_sewer_graph.json");

    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GtaHeistPlanner", "data", "kortz_sewer_graph.json");

    public SewerGraph Load()
    {
        if (File.Exists(FilePath))
        {
            using var stream = File.OpenRead(FilePath);
            return SewerGraphJson.Load(stream);
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
