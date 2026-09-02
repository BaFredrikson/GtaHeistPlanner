using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace GtaHeistPlanner.DataExtractor;

public static class ScenarioRegionExtractor
{
    public static readonly IReadOnlySet<string> GuardScenarioTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "world_human_guard_patrol",
        "world_human_security_shine_torch",
    };

    public static ExtractionResult Extract(string sourcePath)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(sourcePath, LoadOptions.SetLineInfo);
        }
        catch (XmlException exception)
        {
            throw new XmlException($"'{sourcePath}' is not valid XML: {exception.Message}", exception);
        }

        var root = document.Root ?? throw new InvalidDataException("The XML document has no root element.");
        if (root.Name.LocalName != "CScenarioPointRegion")
        {
            throw Error(root, $"Expected root CScenarioPointRegion, found {root.Name.LocalName}.");
        }

        var lookups = Required(root, "LookUps");
        var typeNames = ReadLookup(lookups, "TypeNames");
        var modelSetNames = ReadLookup(lookups, "PedModelSetNames");
        var graphElement = Required(root, "ChainingGraph");
        var nodes = ReadNodes(Required(graphElement, "Nodes"));
        var edges = ReadEdges(Required(graphElement, "Edges"), nodes.Count);
        var chains = ReadChains(Required(graphElement, "Chains"), edges);

        var guardChains = chains
            .Where(chain => ContainsTargetScenario(chain.NodeIndices, nodes, GuardScenarioTypes))
            .Select(chain => new GuardChain(chain.ChainIndex, chain.RawChainValue,
                chain.EdgeIds, chain.NodeIndices,
                chain.NodeIndices.Select(index => nodes[index]).ToArray(),
                chain.EdgeIds.Select(index => edges[index]).ToArray()))
            .ToArray();

        return new ExtractionResult(Path.GetFileName(sourcePath),
            new ChainingGraph(nodes, edges, chains), guardChains,
            ReadSecurityPoints(root, typeNames, modelSetNames));
    }

    public static string GetSourceBaseName(string sourcePath)
    {
        var name = Path.GetFileName(sourcePath);
        if (name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            name = Path.GetFileNameWithoutExtension(name);
        if (name.EndsWith(".ymt", StringComparison.OrdinalIgnoreCase))
            name = Path.GetFileNameWithoutExtension(name);
        return name;
    }

    public static IReadOnlyList<int> ReconstructNodeIndices(
        IReadOnlyList<int> edgeIds, IReadOnlyList<GraphEdge> edges, int chainIndex)
    {
        if (edgeIds.Count == 0)
            return [];

        var result = new List<int>();
        var seen = new HashSet<int>();
        foreach (var edgeId in edgeIds)
        {
            var edge = ResolveEdge(edgeId, edges, chainIndex);
            if (seen.Add(edge.NodeIndexFrom))
                result.Add(edge.NodeIndexFrom);
            if (seen.Add(edge.NodeIndexTo))
                result.Add(edge.NodeIndexTo);
        }
        return result;
    }

    public static bool ContainsTargetScenario(IReadOnlyList<int> nodeIndices,
        IReadOnlyList<GraphNode> nodes, IReadOnlySet<string> targets) =>
        nodeIndices.Any(index => targets.Contains(nodes[index].ScenarioType));

    private static IReadOnlyList<string> ReadLookup(XElement lookups, string name) =>
        Required(lookups, name).Elements("Item").Select((item, index) =>
        {
            var value = item.Value.Trim();
            return value.Length > 0 ? value : throw Error(item, $"{name} lookup item {index} is empty.");
        }).ToArray();

    private static IReadOnlyList<GraphNode> ReadNodes(XElement container) =>
        container.Elements("Item").Select((item, index) =>
        {
            var scenarioType = Required(item, "ScenarioType").Value.Trim();
            if (scenarioType.Length == 0)
                throw Error(item, $"Node {index} has an empty ScenarioType.");
            return new GraphNode(index, ReadCoordinate(Required(item, "Position")), scenarioType,
                ReadBoolValue(Required(item, "HasIncomingEdges")),
                ReadBoolValue(Required(item, "HasOutgoingEdges")));
        }).ToArray();

    private static IReadOnlyList<GraphEdge> ReadEdges(XElement container, int nodeCount) =>
        container.Elements("Item").Select((item, index) =>
        {
            var from = ReadIntValue(Required(item, "NodeIndexFrom"));
            var to = ReadIntValue(Required(item, "NodeIndexTo"));
            ValidateIndex(from, nodeCount, $"Edge {index} source node");
            ValidateIndex(to, nodeCount, $"Edge {index} destination node");
            return new GraphEdge(index, from, to,
                ReadRawValue(Required(item, "Action")),
                ReadRawValue(Required(item, "NavMode")),
                ReadRawValue(Required(item, "NavSpeed")));
        }).ToArray();

    private static IReadOnlyList<GraphChain> ReadChains(XElement container, IReadOnlyList<GraphEdge> edges) =>
        container.Elements("Item").Select((item, index) =>
        {
            var edgeIds = ReadIntegerList(Required(item, "EdgeIds"), $"chain {index} EdgeIds");
            var rawValue = item.Elements().FirstOrDefault(element => element.Name.LocalName != "EdgeIds")
                ?.Attribute("value")?.Value;
            return new GraphChain(index, rawValue, edgeIds,
                ReconstructNodeIndices(edgeIds, edges, index));
        }).ToArray();

    private static IReadOnlyList<SecurityScenarioPoint> ReadSecurityPoints(XElement root,
        IReadOnlyList<string> typeNames, IReadOnlyList<string> modelSetNames)
    {
        var typeLookup = new LookupTable(typeNames, "Scenario type");
        var modelLookup = new LookupTable(modelSetNames, "Ped model set");

        return Required(Required(root, "Points"), "MyPoints").Elements("Item")
            .Select((item, index) => new { Item = item, Index = index })
            .Select(entry =>
            {
                var modelSet = modelLookup.Resolve(ReadIntValue(Required(entry.Item, "ModelSetId")));
                if (!string.Equals(modelSet, "security", StringComparison.OrdinalIgnoreCase))
                    return null;

                var vector = Required(entry.Item, "vPositionAndDirection");
                var w = ReadDoubleAttribute(vector, "w");
                var flags = Required(entry.Item, "Flags").Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return new SecurityScenarioPoint(entry.Index,
                    typeLookup.Resolve(ReadIntValue(Required(entry.Item, "iType"))), modelSet,
                    ReadCoordinate(vector), w, w,
                    new TimeRange(ReadIntValue(Required(entry.Item, "iTimeStartOverride")),
                        ReadIntValue(Required(entry.Item, "iTimeEndOverride"))),
                    ReadIntValue(Required(entry.Item, "iTimeTillPedLeaves")), flags);
            })
            .Where(point => point is not null)
            .Cast<SecurityScenarioPoint>()
            .ToArray();
    }

    private static GraphEdge ResolveEdge(int edgeId, IReadOnlyList<GraphEdge> edges, int chainIndex)
    {
        ValidateIndex(edgeId, edges.Count, $"Chain {chainIndex} edge");
        return edges[edgeId];
    }

    private static void ValidateIndex(int index, int count, string description)
    {
        if (index < 0 || index >= count)
            throw new InvalidDataException($"{description} index {index} is outside the valid range 0..{count - 1}.");
    }

    private static IReadOnlyList<int> ReadIntegerList(XElement element, string description)
    {
        var values = element.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var result = new int[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            if (!int.TryParse(values[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out result[index]))
                throw Error(element, $"Invalid integer '{values[index]}' in {description}.");
        }
        return result;
    }

    private static Coordinate ReadCoordinate(XElement element) => new(
        ReadDoubleAttribute(element, "x"), ReadDoubleAttribute(element, "y"), ReadDoubleAttribute(element, "z"));

    private static double ReadDoubleAttribute(XElement element, string name)
    {
        var text = RequiredAttribute(element, name);
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw Error(element, $"Attribute {name} has invalid number '{text}'.");
        return value;
    }

    private static int ReadIntValue(XElement element)
    {
        var text = ReadRawValue(element);
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw Error(element, $"Element {element.Name.LocalName} has invalid integer value '{text}'.");
        return value;
    }

    private static bool ReadBoolValue(XElement element)
    {
        var text = ReadRawValue(element);
        if (!bool.TryParse(text, out var value))
            throw Error(element, $"Element {element.Name.LocalName} has invalid Boolean value '{text}'.");
        return value;
    }

    private static string ReadRawValue(XElement element) => element.Attribute("value")?.Value
        ?? (element.Value.Trim().Length > 0 ? element.Value.Trim()
            : throw Error(element, $"Element {element.Name.LocalName} has no value."));

    private static string RequiredAttribute(XElement element, string name) => element.Attribute(name)?.Value
        ?? throw Error(element, $"Element {element.Name.LocalName} is missing attribute {name}.");

    private static XElement Required(XElement parent, string name) => parent.Element(name)
        ?? throw Error(parent, $"Element {parent.Name.LocalName} is missing required child {name}.");

    private static InvalidDataException Error(XElement element, string message)
    {
        var info = (IXmlLineInfo)element;
        return new InvalidDataException(message + (info.HasLineInfo() ? $" (line {info.LineNumber})" : string.Empty));
    }
}
