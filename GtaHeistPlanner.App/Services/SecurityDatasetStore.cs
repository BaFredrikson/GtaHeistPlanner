using Avalonia.Platform;
using GtaHeistPlanner.Core.Security;

namespace GtaHeistPlanner.App.Services;

public sealed class SecurityDatasetStore
{
    private static readonly Uri BundledData = new("avares://GtaHeistPlanner.App/Data/kortz/kortz_security.json");
    public string FilePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GtaHeistPlanner", "data", "kortz_security.json");

    public SecurityDataset Load()
    {
        if (File.Exists(FilePath))
        {
            using var stream = File.OpenRead(FilePath);
            return SecurityDatasetJson.Load(stream);
        }
        using var bundled = AssetLoader.Open(BundledData);
        return SecurityDatasetJson.Load(bundled);
    }

    public void Save(SecurityDataset dataset)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        using var stream = File.Create(FilePath);
        SecurityDatasetJson.Save(stream, dataset);
    }
}
