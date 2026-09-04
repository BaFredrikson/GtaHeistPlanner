namespace GtaHeistPlanner.Core.Sewer;

public static class SewerGraphTraversal
{
    public static SewerTraversalResult Traverse(SewerGraph graph, SewerRoute route)
    {
        Validate(graph);
        if (graph.StartNodeId is null)
            return new(false, [], [], "The sewer entrance/start node has not been configured.");

        var current = graph.StartNodeId;
        var visited = new List<string> { current };
        var traversed = new List<SewerConnection>();
        foreach (var turn in route.Turns)
        {
            var connection = graph.Connections.SingleOrDefault(item => item.FromNodeId == current && item.Turn == turn);
            if (connection is null)
                return new(false, visited, traversed, $"No {turn} connection exists from sewer node '{current}'.");
            traversed.Add(connection);
            current = connection.ToNodeId;
            visited.Add(current);
        }
        return new(true, visited, traversed, null);
    }

    public static void Validate(SewerGraph graph)
    {
        var ids = graph.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        if (ids.Count != graph.Nodes.Count)
            throw new InvalidDataException("Sewer node IDs must be unique.");
        if (graph.Nodes.Any(node => node.MapX is < 0 or > 1 || node.MapY is < 0 or > 1))
            throw new InvalidDataException("Sewer node coordinates must be normalized between 0 and 1.");
        if (graph.StartNodeId is not null && !ids.Contains(graph.StartNodeId))
            throw new InvalidDataException($"Sewer start node '{graph.StartNodeId}' does not exist.");
        foreach (var connection in graph.Connections)
        {
            if (!ids.Contains(connection.FromNodeId) || !ids.Contains(connection.ToNodeId))
                throw new InvalidDataException($"Sewer connection '{connection.FromNodeId}' to '{connection.ToNodeId}' references a missing node.");
        }
        if (graph.Connections.GroupBy(item => (item.FromNodeId, item.Turn)).Any(group => group.Count() > 1))
            throw new InvalidDataException("A sewer node cannot have multiple outgoing connections for the same turn.");
    }
}
