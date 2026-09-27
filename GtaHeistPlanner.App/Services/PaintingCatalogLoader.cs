using Avalonia.Platform;
using GtaHeistPlanner.Core.Paintings;

namespace GtaHeistPlanner.App.Services;

public static class PaintingCatalogLoader
{
    private static readonly Uri CatalogUri = new("avares://GtaHeistPlanner.App/Data/kortz/kortz_paintings.json");
    public static PaintingCatalog LoadKortz()
    {
        using var stream = AssetLoader.Open(CatalogUri);
        return PaintingCatalog.Load(stream);
    }
}
