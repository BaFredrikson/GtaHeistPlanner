using System.Text.Json;
using System.Text.Json.Serialization;

namespace GtaHeistPlanner.Core.Loot;

internal sealed class LootTypeJsonConverter : JsonConverter<LootType>
{
    public override LootType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString() ?? throw new JsonException("Loot type must be a string.");
        return value switch
        {
            // Legacy mappings preserve authored positions while giving old broad types
            // a safe, editable Kortz-specific category.
            "Generic" => LootType.CoquardJewelry,
            "Cash" => LootType.SafetyDepositBoxes,
            "Artwork" => LootType.Painting,
            _ when Enum.TryParse<LootType>(value, true, out var type) => type,
            _ => throw new JsonException($"Unknown loot type '{value}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, LootType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
