using GtaHeistPlanner.Core.Maps;

namespace GtaHeistPlanner.Core.Sewer;

public sealed record SewerPath(string Id, IReadOnlyList<MapPoint> Points);
