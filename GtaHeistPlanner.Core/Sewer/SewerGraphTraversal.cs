namespace GtaHeistPlanner.Core.Sewer;

public static class SewerGraphTraversal
{
    public static SewerTraversalResult Traverse(SewerGraph graph, IReadOnlyList<SewerInstruction> instructions)
    {
        Validate(graph);
        if (graph.StartChamber is null)
            return new(false, null, [], [], "The sewer start chamber has not been configured.");
        if (graph.EntrancePathId is null || graph.ExitPathId is null)
            return new(false, graph.StartChamber, [], [], "The sewer entrance and exit paths have not been configured.");

        var current = graph.StartChamber.Value;
        var steps = new List<SewerResolvedStep>();
        var pathIds = new List<string> { graph.EntrancePathId };
        for (var index = 0; index < instructions.Count; index++)
        {
            var instruction = instructions[index];
            if (instruction.Chamber != current)
                return Failed(current, steps, pathIds, $"Expected instruction for Chamber {current} but received Chamber {instruction.Chamber}.");
            SewerResolvedStep step;
            try { step = SewerConnectionResolver.Resolve(graph, instruction); }
            catch (InvalidOperationException exception) { return Failed(current, steps, pathIds, exception.Message); }
            steps.Add(step);
            pathIds.Add(step.PathId);
            if (step.IsExit)
                return Failed(null, steps, pathIds, $"Instruction {instruction} exits directly; the route must reach Chamber {graph.ExitChamber} before the configured exit segment.");
            current = step.DestinationChamber!.Value;
        }
        if (current != graph.ExitChamber)
            return Failed(current, steps, pathIds, $"Expected the spoken route to finish at Chamber {graph.ExitChamber} but reached Chamber {current}.");
        pathIds.Add(graph.ExitPathId);
        return Complete(steps, pathIds);
    }

    public static void Validate(SewerGraph graph)
    {
        if (graph.Paths is null || graph.Connections is null)
            throw new InvalidDataException("Legacy node/Left/Right sewer JSON is no longer supported. Define paths and chamber-letter connections.");
        var pathIds = graph.Paths.Select(path => path.Id).ToHashSet(StringComparer.Ordinal);
        if (pathIds.Count != graph.Paths.Count || pathIds.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Sewer path IDs must be non-empty and unique.");
        foreach (var path in graph.Paths)
            if (path.Points.Count < 2 || path.Points.Any(point => point.X is < 0 or > 1 || point.Y is < 0 or > 1))
                throw new InvalidDataException($"Sewer path '{path.Id}' requires at least two normalized points.");
        if (graph.EntrancePathId is not null && !pathIds.Contains(graph.EntrancePathId))
            throw new InvalidDataException($"Sewer entrance path '{graph.EntrancePathId}' does not exist.");
        if (graph.ExitPathId is not null && !pathIds.Contains(graph.ExitPathId))
            throw new InvalidDataException($"Sewer exit path '{graph.ExitPathId}' does not exist.");
        var connectionIds = graph.Connections.Select(item => item.Id).ToList();
        if (connectionIds.Any(string.IsNullOrWhiteSpace) || connectionIds.Distinct(StringComparer.Ordinal).Count() != connectionIds.Count)
            throw new InvalidDataException("Sewer connection IDs must be non-empty and unique.");
        foreach (var connection in graph.Connections)
        {
            if (!pathIds.Contains(connection.PathId))
                throw new InvalidDataException($"Sewer connection '{connection.Id}' references missing path '{connection.PathId}'.");
            _ = new SewerInstruction(connection.ChamberA, connection.TunnelFromA);
            if (connection.State is SewerTunnelState.Traversable && (connection.ChamberB is null || connection.TunnelFromB is null))
                throw new InvalidDataException($"Traversable sewer connection '{connection.Id}' requires two chamber-facing labels.");
            if (connection.State is SewerTunnelState.Exit && (connection.ChamberB is not null || connection.TunnelFromB is not null))
                throw new InvalidDataException($"Exit sewer connection '{connection.Id}' must not define a second chamber.");
            if (connection.ChamberB is { } chamberB && connection.TunnelFromB is { } tunnelB)
                _ = new SewerInstruction(chamberB, tunnelB);
        }
        var labels = graph.Connections.Select(item => new SewerInstruction(item.ChamberA, item.TunnelFromA))
            .Concat(graph.Connections.Where(item => item.ChamberB is not null && item.TunnelFromB is not null)
                .Select(item => new SewerInstruction(item.ChamberB!.Value, item.TunnelFromB!.Value)));
        if (labels.GroupBy(item => item).Any(group => group.Count() > 1))
            throw new InvalidDataException("Each chamber-letter sewer instruction must identify only one physical connection.");
    }

    private static SewerTraversalResult Complete(IReadOnlyList<SewerResolvedStep> steps, IReadOnlyList<string> pathIds) =>
        new(true, null, steps, pathIds, null);

    private static SewerTraversalResult Failed(int? chamber, IReadOnlyList<SewerResolvedStep> steps, IReadOnlyList<string> pathIds, string error) =>
        new(false, chamber, steps, pathIds, error);
}
