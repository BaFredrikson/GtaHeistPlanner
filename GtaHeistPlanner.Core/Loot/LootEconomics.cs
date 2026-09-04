namespace GtaHeistPlanner.Core.Loot;

public sealed record LootEconomics(
    LootType Type,
    string DisplayName,
    int BagPercent,
    int MinValue,
    int MaxValue,
    OptionalPrep RequiredPrep);
