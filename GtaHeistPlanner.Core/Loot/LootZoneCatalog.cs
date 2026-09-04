namespace GtaHeistPlanner.Core.Loot;

public static class LootZoneCatalog
{
    public const string CrispGalleryId = "crisp-gallery";

    public static LootZoneDefinition CrispGallery { get; } =
        new(CrispGalleryId, "Albert Crisp Gallery", 2);

    public static LootZoneDefinition? Get(string? id) => id switch
    {
        null or "" => null,
        CrispGalleryId => CrispGallery,
        _ => throw new KeyNotFoundException($"Unknown loot zone '{id}'."),
    };
}
