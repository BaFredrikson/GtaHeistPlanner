namespace GtaHeistPlanner.Core.Maps;

public sealed record MapDefinition(
    string Id,
    string DisplayName,
    string SvgAssetPath,
    MapCategory Category);
