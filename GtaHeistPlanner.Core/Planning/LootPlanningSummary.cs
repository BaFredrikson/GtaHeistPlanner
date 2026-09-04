using GtaHeistPlanner.Core.Loot;

namespace GtaHeistPlanner.Core.Planning;

public sealed record LootCategorySummary(string Name, int Count, int MinValue, int MaxValue);

public sealed record LootPlanningSummary(
    int PlayerCount,
    int BagCapacityPercent,
    int KnownLocationCount,
    int PresentCount,
    int BuyersRequestCount,
    int EstimatedMinValue,
    int EstimatedMaxValue,
    int InaccessibleCount,
    int GlassCutterCount,
    int PowerDrillsCount,
    IReadOnlyList<LootCategorySummary> Categories);

public static class LootPlanningSummaryCalculator
{
    public static LootPlanningSummary Calculate(
        IEnumerable<LootSpawnDefinition> definitions,
        IEnumerable<LootSpawnState> states,
        int playerCount)
    {
        if (playerCount is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(playerCount));

        var definitionList = definitions.ToList();
        var stateById = states.ToDictionary(state => state.SpawnId, StringComparer.Ordinal);
        var present = definitionList.Where(definition =>
            stateById.TryGetValue(definition.Id, out var state) && state.IsPresent).ToList();
        var entries = present.Select(definition => new
        {
            Definition = definition,
            Economics = LootEconomicsCatalog.Get(definition.Type, definition.ZoneId),
            State = stateById[definition.Id],
        }).ToList();

        var categories = entries
            .GroupBy(entry => entry.Economics.DisplayName)
            .Select(group => new LootCategorySummary(
                group.Key,
                group.Count(),
                group.Sum(entry => entry.State.ScopedValue ?? entry.Economics.MinValue),
                group.Sum(entry => entry.State.ScopedValue ?? entry.Economics.MaxValue)))
            .OrderBy(item => item.Name)
            .ToList();

        return new LootPlanningSummary(
            playerCount,
            playerCount * 100,
            definitionList.Count,
            present.Count,
            stateById.Values.Count(state => state.IsBuyersRequest),
            categories.Sum(category => category.MinValue),
            categories.Sum(category => category.MaxValue),
            present.Count(definition => (LootZoneCatalog.Get(definition.ZoneId)?.MinimumPlayers ?? 1) > playerCount),
            entries.Count(entry => entry.Economics.RequiredPrep == OptionalPrep.GlassCutter),
            entries.Count(entry => entry.Economics.RequiredPrep == OptionalPrep.PowerDrills),
            categories);
    }
}
