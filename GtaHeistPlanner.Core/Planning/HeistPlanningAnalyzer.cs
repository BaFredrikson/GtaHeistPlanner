using GtaHeistPlanner.Core.Loot;

namespace GtaHeistPlanner.Core.Planning;

public enum PrepRecommendationLevel { Required, Recommended, Optional, NotRecommended, NotNeeded }
public enum InfiltrationEntry { Skylight, AlphaMail, AccessCodes }

public sealed record PlanningLootCandidate(
    string LootId, string Name, string MapId, LootType Type, int BagPercent, bool IsAccessible,
    int MinimumPlayers, bool IsBuyersRequest, int? KnownExactValue, int EstimatedMinValue,
    int EstimatedMaxValue, OptionalPrep RequiredPrep)
{
    public int ExpectedValue => KnownExactValue ?? (EstimatedMinValue + EstimatedMaxValue) / 2;
    public int PotentialMinValue => KnownExactValue ?? EstimatedMinValue;
    public int PotentialMaxValue => KnownExactValue ?? EstimatedMaxValue;
    public bool IsEstimated => KnownExactValue is null;
}

public sealed record PlanningLootCategorySummary(
    string Name, int TotalCount, int AccessibleCount, int InaccessibleCount, int TotalBagPercent,
    int KnownExactValue, int EstimatedMinValue, int EstimatedMaxValue);

public sealed record HaulRecommendation(
    IReadOnlyList<PlanningLootCandidate> SelectedLoot, int BagUsagePercent, int RemainingCapacityPercent,
    int KnownExactSubtotal, int EstimatedMinSubtotal, int EstimatedMaxSubtotal,
    int TotalMinPotentialValue, int TotalMaxPotentialValue, int BuyersRequestBagPercent,
    int NonBuyersRequestBagPercent, bool BuyersRequestCapacityConflict, string? Diagnostic);

public sealed record PrepRequirementSummary(
    string Name, PrepRecommendationLevel Recommendation, int TargetCount, int BagPercent,
    int KnownExactValue, int EstimatedMinValue, int EstimatedMaxValue, string Reason);

public sealed record EntryRecommendation(InfiltrationEntry Entry, IReadOnlyList<string> Reasons);

public sealed record PlanningAnalysis(
    int PlayerCount, int CrewCapacityPercent, int ScopedCount, int AccessibleScopedCount,
    int InaccessibleScopedCount, int AccessibleScopedBagPercent, int InaccessibleScopedBagPercent,
    int ExcessAccessibleBagPercent, int KnownExactValue, int EstimatedMinValue, int EstimatedMaxValue,
    int PotentialMinValue, int PotentialMaxValue, int BuyersRequestCount, int BuyersRequestBagPercent,
    IReadOnlyList<string> InaccessibleBuyersRequestIds, IReadOnlyList<PlanningLootCategorySummary> Categories,
    HaulRecommendation RecommendedHaul, HaulRecommendation PowerDrillsHaul, PrepRequirementSummary GlassCutter,
    PrepRequirementSummary PowerDrills, EntryRecommendation EntryRecommendation);

public static class HeistPlanningAnalyzer
{
    public static PlanningAnalysis Analyze(IEnumerable<LootSpawnDefinition> definitions,
        IEnumerable<LootSpawnState> states, int playerCount)
    {
        if (playerCount is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(playerCount));
        var definitionList = definitions.ToArray();
        var stateById = states.ToDictionary(state => state.SpawnId, StringComparer.Ordinal);
        var candidates = definitionList.Where(definition => stateById.TryGetValue(definition.Id, out var state) && state.IsPresent)
            .Select(definition => CreateCandidate(definition, stateById[definition.Id], playerCount)).ToArray();
        var accessible = candidates.Where(item => item.IsAccessible).ToArray();
        var capacity = playerCount * 100;
        var haul = Optimize(accessible.Where(item => item.Type != LootType.SafetyDepositBoxes), capacity);
        var boxes = accessible.Where(item => item.Type == LootType.SafetyDepositBoxes).ToArray();
        var drillsHaul = Optimize(accessible, capacity);
        var glass = PrepSummary("Glass Cutter", accessible.Where(item => item.RequiredPrep == OptionalPrep.GlassCutter).ToArray(),
            items => items.Length == 0 ? PrepRecommendationLevel.NotNeeded : PrepRecommendationLevel.Required,
            items => items.Length == 0 ? "No accessible scoped glass-case loot requires it." : $"Required to access {items.Length} scoped glass-case target(s).");
        var drills = PrepSummary("Power Drills", boxes,
            items => items.Length == 0 ? PrepRecommendationLevel.NotNeeded
                : haul.RemainingCapacityPercent >= 60 ? PrepRecommendationLevel.Recommended
                : haul.RemainingCapacityPercent >= 30 ? PrepRecommendationLevel.Optional
                : PrepRecommendationLevel.NotRecommended,
            items => items.Length == 0 ? "No accessible scoped safety-deposit boxes are available."
                : haul.RemainingCapacityPercent >= 60 ? $"{haul.RemainingCapacityPercent}% spare capacity can hold at least two boxes."
                : haul.RemainingCapacityPercent >= 30 ? $"{haul.RemainingCapacityPercent}% spare capacity can hold one box."
                : "Projected bags are already full or lack the 30% required for a box.");
        var cargoPresent = candidates.Any(item => item.Type == LootType.LoadingBayCargo);
        var cargoSelected = haul.SelectedLoot.Any(item => item.Type == LootType.LoadingBayCargo);
        var entry = cargoSelected
            ? new EntryRecommendation(InfiltrationEntry.AlphaMail, ["Loading Bay Cargo is confirmed and included in the recommended haul.", "The cargo's 30% bag usage fits after higher-priority loot."])
            : new EntryRecommendation(InfiltrationEntry.Skylight, cargoPresent
                ? ["Loading Bay Cargo is present but excluded because higher-priority loot uses the available capacity.", "Skylight remains the preferred default entry."]
                : ["Skylight is the preferred direct default entry.", "No Loading Bay Cargo is included in the recommended haul."]);
        var categories = candidates.GroupBy(item => LootEconomicsCatalog.Get(item.Type,
                definitionList.First(definition => definition.Id == item.LootId).ZoneId).DisplayName)
            .Select(group => new PlanningLootCategorySummary(group.Key, group.Count(), group.Count(x => x.IsAccessible),
                group.Count(x => !x.IsAccessible), group.Sum(x => x.BagPercent), group.Sum(x => x.KnownExactValue ?? 0),
                group.Where(x => x.IsEstimated).Sum(x => x.EstimatedMinValue), group.Where(x => x.IsEstimated).Sum(x => x.EstimatedMaxValue)))
            .OrderBy(item => item.Name).ToArray();
        var exact = accessible.Sum(item => item.KnownExactValue ?? 0);
        var estimatedMin = accessible.Where(item => item.IsEstimated).Sum(item => item.EstimatedMinValue);
        var estimatedMax = accessible.Where(item => item.IsEstimated).Sum(item => item.EstimatedMaxValue);
        return new(playerCount, capacity, candidates.Length, accessible.Length, candidates.Length - accessible.Length,
            accessible.Sum(item => item.BagPercent), candidates.Where(item => !item.IsAccessible).Sum(item => item.BagPercent),
            Math.Max(0, accessible.Sum(item => item.BagPercent) - capacity), exact, estimatedMin, estimatedMax,
            exact + estimatedMin, exact + estimatedMax, candidates.Count(item => item.IsBuyersRequest),
            candidates.Where(item => item.IsBuyersRequest).Sum(item => item.BagPercent),
            candidates.Where(item => item.IsBuyersRequest && !item.IsAccessible).Select(item => item.LootId).ToArray(),
            categories, haul, drillsHaul, glass, drills, entry);
    }

    public static HaulRecommendation Optimize(IEnumerable<PlanningLootCandidate> source, int capacity)
    {
        var candidates = source.Where(item => item.IsAccessible).ToArray();
        var buyers = candidates.Where(item => item.IsBuyersRequest).OrderByDescending(Efficiency).ThenBy(item => item.LootId).ToArray();
        var selectedBuyers = new List<PlanningLootCandidate>();
        var buyerBag = buyers.Sum(item => item.BagPercent);
        var conflict = buyerBag > capacity;
        var used = 0;
        foreach (var buyer in buyers)
            if (used + buyer.BagPercent <= capacity) { selectedBuyers.Add(buyer); used += buyer.BagPercent; }
        var normals = candidates.Where(item => !item.IsBuyersRequest).ToArray();
        var remaining = Math.Max(0, capacity - used);
        var dp = new List<PlanningLootCandidate>?[remaining + 1];
        dp[0] = [];
        foreach (var item in normals)
            for (var bag = remaining; bag >= item.BagPercent; bag--)
            {
                if (dp[bag - item.BagPercent] is null) continue;
                var proposal = dp[bag - item.BagPercent]!.Append(item).ToList();
                if (dp[bag] is null || Expected(proposal) > Expected(dp[bag]!)) dp[bag] = proposal;
            }
        var best = dp.Where(items => items is not null).Select(items => items!).OrderByDescending(Expected)
            .ThenByDescending(items => items.Sum(item => item.PotentialMinValue)).FirstOrDefault() ?? [];
        var selected = selectedBuyers.Concat(best.OrderByDescending(Efficiency)).ToArray();
        var totalBag = selected.Sum(item => item.BagPercent);
        var known = selected.Sum(item => item.KnownExactValue ?? 0);
        var min = selected.Where(item => item.IsEstimated).Sum(item => item.EstimatedMinValue);
        var max = selected.Where(item => item.IsEstimated).Sum(item => item.EstimatedMaxValue);
        return new(selected, totalBag, capacity - totalBag, known, min, max, known + min, known + max,
            selected.Where(item => item.IsBuyersRequest).Sum(item => item.BagPercent),
            selected.Where(item => !item.IsBuyersRequest).Sum(item => item.BagPercent), conflict,
            conflict ? $"Accessible Buyer's Request loot requires {buyerBag}% but crew capacity is {capacity}%; the recommendation is partial." : null);
    }

    private static PlanningLootCandidate CreateCandidate(LootSpawnDefinition definition, LootSpawnState state, int players)
    {
        var economics = LootEconomicsCatalog.Get(definition.Type, definition.ZoneId);
        var minimumPlayers = LootZoneCatalog.Get(definition.ZoneId)?.MinimumPlayers ?? 1;
        var exactAllowed = definition.Type is not (LootType.LoadingBayCargo or LootType.SafetyDepositBoxes);
        return new(definition.Id, definition.Name, definition.MapId, definition.Type, economics.BagPercent,
            players >= minimumPlayers, minimumPlayers, state.IsBuyersRequest,
            exactAllowed ? state.ScopedValue : null, economics.MinValue, economics.MaxValue, economics.RequiredPrep);
    }

    private static PrepRequirementSummary PrepSummary(string name, PlanningLootCandidate[] items,
        Func<PlanningLootCandidate[], PrepRecommendationLevel> level, Func<PlanningLootCandidate[], string> reason) =>
        new(name, level(items), items.Length, items.Sum(item => item.BagPercent), items.Sum(item => item.KnownExactValue ?? 0),
            items.Where(item => item.IsEstimated).Sum(item => item.EstimatedMinValue),
            items.Where(item => item.IsEstimated).Sum(item => item.EstimatedMaxValue), reason(items));
    private static double Efficiency(PlanningLootCandidate item) => item.ExpectedValue / (double)item.BagPercent;
    private static int Expected(IEnumerable<PlanningLootCandidate> items) => items.Sum(item => item.ExpectedValue);
}
