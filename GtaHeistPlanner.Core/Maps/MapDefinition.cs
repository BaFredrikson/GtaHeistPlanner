namespace GtaHeistPlanner.Core.Maps;

public sealed record MapDefinition(
    string Id,
    string DisplayName,
    string SvgAssetPath,
    MapCategory Category)
{
    public double AspectRatio => Id switch
    {
        "basement" => 1612.4 / 971,
        "sewer" => 1145.3 / 1009.05,
        "exterior-groundfloor" => 185.1 / 227.65,
        "exterior-firstfloor" or "exterior-maze" => 225.9 / 299.05,
        "exterior-second-floor" => 185.15 / 227.65,
        "exterior-rooftop" => 185.2 / 227.7,
        _ => 399.15 / 541.5,
    };
}
