namespace GtaHeistPlanner.Core.Sewer;

public sealed record SewerResolvedStep(
    SewerInstruction Instruction,
    SewerConnection Connection,
    int? DestinationChamber,
    bool IsExit)
{
    public string PathId => Connection.PathId;
}
