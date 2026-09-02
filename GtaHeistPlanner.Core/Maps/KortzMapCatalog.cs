namespace GtaHeistPlanner.Core.Maps;

public static class KortzMapCatalog
{
    public const string DefaultMapId = "main-floor";

    public static IReadOnlyList<MapDefinition> Maps { get; } =
    [
        new("main-floor", "Main Floor", "Assets/maps/kortz/main-floor.svg", MapCategory.Interior),
        new("upper-floor", "Upper Floor", "Assets/maps/kortz/upper-floor.svg", MapCategory.Interior),
        new("lower-floor", "Lower Floor", "Assets/maps/kortz/lower-floor.svg", MapCategory.Interior),
        new("basement", "Basement", "Assets/maps/kortz/basement.svg", MapCategory.Interior),
        new("balcony", "Balcony", "Assets/maps/kortz/balcony.svg", MapCategory.Interior),
        new("stairs", "Stairs", "Assets/maps/kortz/stairs.svg", MapCategory.Access),
        new("sewer", "Sewer", "Assets/maps/kortz/sewer.svg", MapCategory.Access),
        new("exterior-groundfloor", "Exterior · Ground Floor", "Assets/maps/kortz/exterior-groundfloor.svg", MapCategory.Exterior),
        new("exterior-firstfloor", "Exterior · First Floor", "Assets/maps/kortz/exterior-firstfloor.svg", MapCategory.Exterior),
        new("exterior-second-floor", "Exterior · Second Floor", "Assets/maps/kortz/exterior-secondfloor.svg", MapCategory.Exterior),
        new("exterior-rooftop", "Exterior · Rooftop", "Assets/maps/kortz/exterior-rooftop.svg", MapCategory.Exterior),
        new("exterior-maze", "Exterior · Maze", "Assets/maps/kortz/exterior-maze.svg", MapCategory.Exterior),
    ];

    public static MapDefinition DefaultMap => GetById(DefaultMapId);

    public static MapDefinition GetById(string id) =>
        Maps.FirstOrDefault(map => string.Equals(map.Id, id, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"No Kortz map has the ID '{id}'.");
}
