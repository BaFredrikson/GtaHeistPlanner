using System.Text.Json;
using System.Text.Json.Serialization;

namespace GtaHeistPlanner.Core.Sewer;

public static class SewerGraphJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static SewerGraph Load(Stream stream)
    {
        var graph = JsonSerializer.Deserialize<SewerGraph>(stream, Options)
            ?? throw new InvalidDataException("Sewer graph JSON was empty.");
        SewerGraphTraversal.Validate(graph);
        return graph;
    }

    public static void Save(Stream stream, SewerGraph graph)
    {
        SewerGraphTraversal.Validate(graph);
        JsonSerializer.Serialize(stream, graph, Options);
    }
}
