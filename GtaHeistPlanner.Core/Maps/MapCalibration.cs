namespace GtaHeistPlanner.Core.Maps;

public sealed record MapCalibration
{
    public double SourceOriginX { get; init; }
    public double SourceOriginY { get; init; }
    public double ScaleX { get; init; } = 1;
    public double ScaleY { get; init; } = 1;
    public double OffsetX { get; init; }
    public double OffsetY { get; init; }
    public double RotationDegrees { get; init; }
    public bool FlipX { get; init; }
    public bool FlipY { get; init; }
}
