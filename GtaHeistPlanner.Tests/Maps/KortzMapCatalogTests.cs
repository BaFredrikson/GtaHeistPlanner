using GtaHeistPlanner.Core.Maps;

namespace GtaHeistPlanner.Tests.Maps;

public sealed class KortzMapCatalogTests
{
    [Fact]
    public void DefaultMap_IsMainFloor()
    {
        var map = KortzMapCatalog.DefaultMap;

        Assert.Equal("main-floor", map.Id);
        Assert.Equal("Assets/maps/kortz/main-floor.svg", map.SvgAssetPath);
    }

    [Fact]
    public void Maps_HaveUniqueStableIdsAndAssetPaths()
    {
        Assert.Equal(KortzMapCatalog.Maps.Count, KortzMapCatalog.Maps.Select(map => map.Id).Distinct().Count());
        Assert.Equal(KortzMapCatalog.Maps.Count, KortzMapCatalog.Maps.Select(map => map.SvgAssetPath).Distinct().Count());
    }

    [Fact]
    public void GetById_ReturnsRequestedMap()
    {
        var map = KortzMapCatalog.GetById("sewer");

        Assert.Equal("Sewer", map.DisplayName);
        Assert.Equal(MapCategory.Access, map.Category);
    }

    [Fact]
    public void GetById_ThrowsForUnknownMap()
    {
        Assert.Throws<KeyNotFoundException>(() => KortzMapCatalog.GetById("missing"));
    }

    [Fact]
    public void ExteriorFirstFloor_HasOverlayStableIdAndUnchangedAsset()
    {
        var map = KortzMapCatalog.GetById("exterior-firstfloor");

        Assert.Equal("Exterior · First Floor", map.DisplayName);
        Assert.Equal("Assets/maps/kortz/exterior-firstfloor.svg", map.SvgAssetPath);
    }
}
