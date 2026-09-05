namespace GtaHeistPlanner.Core.Sewer;

public sealed record SewerConnection(
    string Id,
    int ChamberA,
    char TunnelFromA,
    int? ChamberB,
    char? TunnelFromB,
    string PathId,
    SewerTunnelState State = SewerTunnelState.Traversable);
