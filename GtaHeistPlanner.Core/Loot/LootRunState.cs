namespace GtaHeistPlanner.Core.Loot;

public sealed class LootRunState
{
    public const int BuyersRequestLimit = 3;

    private readonly Dictionary<string, LootSpawnDefinition> _definitions;
    private readonly Dictionary<string, LootSpawnState> _states;

    public LootRunState(IEnumerable<LootSpawnDefinition> definitions)
    {
        var definitionList = definitions.ToList();
        if (definitionList.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != definitionList.Count)
            throw new ArgumentException("Loot spawn IDs must be unique.", nameof(definitions));

        _definitions = definitionList.ToDictionary(item => item.Id, StringComparer.Ordinal);
        _states = definitionList.ToDictionary(
            item => item.Id,
            item => NewState(item.Id),
            StringComparer.Ordinal);
    }

    public IReadOnlyCollection<LootSpawnDefinition> Definitions => _definitions.Values;
    public IReadOnlyCollection<LootSpawnState> States => _states.Values;
    public int BuyersRequestCount => _states.Values.Count(state => state.IsBuyersRequest);

    public LootSpawnState GetState(string spawnId) =>
        _states.TryGetValue(spawnId, out var state)
            ? state
            : throw new KeyNotFoundException($"Unknown loot spawn '{spawnId}'.");

    public void SetLootPresent(string spawnId, bool value) => GetState(spawnId).IsPresent = value;

    public void SetLooted(string spawnId, bool value) => GetState(spawnId).IsLooted = value;

    public bool TrySetBuyersRequest(string spawnId, bool value, out string? error)
    {
        var state = GetState(spawnId);
        if (value && !state.IsBuyersRequest && BuyersRequestCount >= BuyersRequestLimit)
        {
            error = $"Buyer's Request is limited to {BuyersRequestLimit} containers. Clear one before selecting another.";
            return false;
        }

        state.IsBuyersRequest = value;
        error = null;
        return true;
    }

    public void ResetLootState()
    {
        foreach (var state in _states.Values)
        {
            state.IsPresent = false;
            state.IsBuyersRequest = false;
            state.IsLooted = false;
            state.ScopedValue = null;
        }
    }

    private static LootSpawnState NewState(string id) => new() { SpawnId = id };
}
