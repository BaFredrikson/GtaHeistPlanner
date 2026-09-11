namespace GtaHeistPlanner.Core.Planning;

public sealed class HeistSaveFile
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required Guid HeistId { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset LastSavedAtUtc { get; init; }
    public int PlayerCount { get; init; } = 1;
    public PlannerStage Stage { get; init; } = PlannerStage.Preparation;
    public string? VaultCode { get; init; }
    public int GuardsDown { get; init; }
    public int CamerasDown { get; init; }
    public string? FocusedMapId { get; init; }
    public Dictionary<string, HeistLootState> LootStates { get; init; } = new(StringComparer.Ordinal);
    public SewerRuntimeSaveState SewerRuntimeState { get; init; } = new();
    public SecurityRuntimeSaveState SecurityRuntimeState { get; init; } = new();
}

public sealed record HeistLootState(bool IsPresent, int? ScopedValue, bool IsBuyersRequest, bool IsLooted);

public sealed class SewerRuntimeSaveState
{
    public string RouteInput { get; init; } = string.Empty;
    public bool IsRouteComplete { get; init; }
    public IReadOnlyList<string> HighlightedPathIds { get; init; } = [];
}

public sealed class SecurityRuntimeSaveState
{
    public IReadOnlyList<string> ActiveCameraIds { get; init; } = [];
    public IReadOnlyList<string> ActiveGuardIds { get; init; } = [];
    public IReadOnlyList<string> ActivePatrolIds { get; init; } = [];
    public Dictionary<string, GtaHeistPlanner.Core.Security.CameraDisableMethod> DisabledCameras { get; init; } = new(StringComparer.Ordinal);
    public int? CountedCameraTakedowns { get; init; }
}
