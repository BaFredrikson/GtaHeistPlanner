namespace GtaHeistPlanner.Core.Loot;

public sealed class LootSpawnState
{
    public required string SpawnId { get; init; }
    public bool IsPresent { get; set; }
    public bool IsBuyersRequest { get; set; }
    public bool IsLooted { get; set; }
    public int? ScopedValue { get; set; }
}
