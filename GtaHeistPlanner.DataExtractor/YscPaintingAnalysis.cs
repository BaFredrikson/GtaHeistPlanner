using System.Text;

namespace GtaHeistPlanner.DataExtractor;

public static class YscPaintingAnalysis
{
    public static readonly IReadOnlyList<string> PaintingNames =
    [
        "Hare Oneself Think", "The Downfall of Rome", "Brother Brother", "A Cast of Characters", "Gone To Seed",
        "True Love", "Breathless", "Consumato", "I Hear Voices", "Winter, Nowhere in Particular",
        "The Girl With the Pearl Necklace", "Chat on Fruit", "Pumpkin", "Twindifference", "Stacks Study V", "I, Fruit",
        "To Beat About the Bush", "In Excess of Success", "Juiced", "A Winding Road Home", "Teckels", "Trust",
        "Until Death", "What Are Melons?", "The Outcome of Endeavour", "Mi O Melee", "La Derniﾃｨre Dﾃｩbauche"
    ];

    public static IReadOnlyList<PaintingMatch> Find(YscScript script) => PaintingNames.Select(name =>
    {
        var hash = Joaat(name);
        return new PaintingMatch(name, $"0x{hash:X8}",
            script.StringTable.Entries.Where(x => string.Equals(x.Text, name, StringComparison.OrdinalIgnoreCase)).Select(x => x.Index).ToArray(),
            script.Statics.Where(x => (uint)x.UnsignedInteger == hash).Select(x => x.Index).ToArray(),
            script.Globals.Where(x => (uint)x.UnsignedInteger == hash).Select(x => x.Index).ToArray(),
            script.Instructions.Where(x => x.ImmediateValues.Any(v => (uint)v == hash)).Select(x => x.Offset).ToArray());
    }).ToArray();

    public static uint Joaat(string value)
    {
        uint hash = 0;
        foreach (var b in Encoding.UTF8.GetBytes(value.ToLowerInvariant()))
        {
            hash += b;
            hash += hash << 10;
            hash ^= hash >> 6;
        }
        hash += hash << 3;
        hash ^= hash >> 11;
        hash += hash << 15;
        return hash;
    }
}
