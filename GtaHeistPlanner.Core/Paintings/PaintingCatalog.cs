using System.Text.Json;
using System.Text.Json.Serialization;

namespace GtaHeistPlanner.Core.Paintings;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaintingAcquisitionType { HeistTarget, CollectionReward }

public sealed record PaintingDefinition(string Id, string Name, PaintingAcquisitionType AcquisitionType, int? RegularValue);

public sealed class PaintingCatalog
{
    public PaintingCatalog(IEnumerable<PaintingDefinition> paintings)
    {
        Paintings = paintings.ToArray();
        if (Paintings.Count == 0) throw new InvalidDataException("The painting catalog is empty.");
        var duplicate = Paintings.GroupBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null) throw new InvalidDataException($"Duplicate painting ID '{duplicate.Key}'.");
        if (Paintings.Any(x => string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Name)))
            throw new InvalidDataException("Painting IDs and names are required.");
        if (Paintings.Any(x => x.RegularValue < 0)) throw new InvalidDataException("Painting values cannot be negative.");
    }

    public IReadOnlyList<PaintingDefinition> Paintings { get; }
    public IReadOnlyList<PaintingDefinition> HeistTargets => Paintings.Where(x => x.AcquisitionType == PaintingAcquisitionType.HeistTarget).ToArray();
    public PaintingDefinition Reward => Paintings.Single(x => x.AcquisitionType == PaintingAcquisitionType.CollectionReward);
    public PaintingDefinition? Find(string? id) => id is null ? null : Paintings.FirstOrDefault(x => x.Id == id);

    public static PaintingCatalog Load(Stream stream)
    {
        var entries = JsonSerializer.Deserialize<PaintingDefinition[]>(stream, JsonOptions)
            ?? throw new InvalidDataException("The painting catalog is empty.");
        return new PaintingCatalog(entries);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
}

public static class PaintingPricing
{
    public static int? FirstWeeklySaleValue(int? regularValue) => regularValue is null ? null : checked(regularValue.Value * 4);
}
