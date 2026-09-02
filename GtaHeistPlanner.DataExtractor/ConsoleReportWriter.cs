namespace GtaHeistPlanner.DataExtractor;

public static class ConsoleReportWriter
{
    public static void Write(ExtractionResult result)
    {
        Console.WriteLine($"Source: {result.Source}");
        Console.WriteLine($"Graph: {result.Graph.Nodes.Count} nodes, {result.Graph.Edges.Count} edges, {result.Graph.Chains.Count} chains");
        Console.WriteLine($"Guard-related chains: {result.GuardChains.Count}");
        foreach (var chain in result.GuardChains)
        {
            Console.WriteLine($"\nChain {chain.ChainIndex} (raw value: {chain.RawChainValue ?? "n/a"})");
            Console.WriteLine($"  Edge IDs: {string.Join(", ", chain.EdgeIds)}");
            Console.WriteLine($"  Node indices: {string.Join(", ", chain.NodeIndices)}");
            foreach (var node in chain.Nodes)
                Console.WriteLine($"  Node {node.NodeIndex}: ({node.Position.X}, {node.Position.Y}, {node.Position.Z}) {node.ScenarioType}");
            foreach (var edge in chain.Edges)
                Console.WriteLine($"  Edge {edge.EdgeIndex}: {edge.NodeIndexFrom} -> {edge.NodeIndexTo}; Action={edge.Action}, NavMode={edge.NavMode}, NavSpeed={edge.NavSpeed}");
        }

        Console.WriteLine($"\nStandalone security scenario points: {result.SecurityScenarioPoints.Count}");
        foreach (var point in result.SecurityScenarioPoints)
            Console.WriteLine($"  Point {point.SourcePointIndex}: {point.ScenarioType}, {point.ModelSet}, ({point.Position.X}, {point.Position.Y}, {point.Position.Z}), w={point.W}, time={point.TimeRange.Start}-{point.TimeRange.End}, leaves={point.TimeTillPedLeaves}, flags=[{string.Join(", ", point.Flags)}]");
        Console.WriteLine();
    }
}
