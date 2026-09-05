namespace GtaHeistPlanner.Core.Sewer;

public static class SewerConnectionResolver
{
    public static SewerResolvedStep Resolve(SewerGraph graph, SewerInstruction instruction)
    {
        SewerGraphTraversal.Validate(graph);
        var connection = graph.Connections.SingleOrDefault(item => MatchesA(item, instruction) || MatchesB(item, instruction))
            ?? throw new InvalidOperationException($"Unknown sewer instruction {instruction}.");

        if (connection.State != SewerTunnelState.Traversable && connection.State != SewerTunnelState.Exit)
            throw new InvalidOperationException($"Sewer instruction {instruction} is {FormatState(connection.State)} and cannot be traversed.");

        var enteredFromA = MatchesA(connection, instruction);
        var destination = enteredFromA ? connection.ChamberB : connection.ChamberA;
        var isExit = connection.State == SewerTunnelState.Exit;
        if (isExit) destination = null;
        return new(instruction, connection, destination, isExit);
    }

    private static bool MatchesA(SewerConnection connection, SewerInstruction instruction) =>
        connection.ChamberA == instruction.Chamber && char.ToUpperInvariant(connection.TunnelFromA) == instruction.Tunnel;

    private static bool MatchesB(SewerConnection connection, SewerInstruction instruction) =>
        connection.ChamberB == instruction.Chamber && connection.TunnelFromB is { } tunnel &&
        char.ToUpperInvariant(tunnel) == instruction.Tunnel;

    private static string FormatState(SewerTunnelState state) => state switch
    {
        SewerTunnelState.AlwaysClosed => "always closed",
        SewerTunnelState.DeadEnd => "a dead end",
        SewerTunnelState.Unknown => "unknown",
        _ => state.ToString(),
    };
}
