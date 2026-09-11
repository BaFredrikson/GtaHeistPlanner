namespace GtaHeistPlanner.Voice;

public abstract record VoiceCommand;

public sealed record ActivateScopeOutCommand : VoiceCommand;

public sealed record DeactivateScopeOutCommand : VoiceCommand;

public sealed record RecordScopedLootCommand(string LootLocationId, int? ScopedValue, SpokenNumberResult? ValueParse = null) : VoiceCommand;
public sealed record SetPendingLootValueCommand(int ScopedValue, SpokenNumberResult? ValueParse = null) : VoiceCommand;
public sealed record ContinueLootValueCommand(string LootLocationId, int Remainder) : VoiceCommand;
public sealed record ToggleSpecialLootCommand : VoiceCommand;
public sealed record UndoVoiceCommand : VoiceCommand;
public sealed record FocusMapVoiceCommand(string MapId) : VoiceCommand;
public sealed record ExitMapFocusVoiceCommand : VoiceCommand;
public sealed record SetVaultCodeVoiceCommand(string? VaultCode) : VoiceCommand;
public sealed record ChangeStageVoiceCommand(GtaHeistPlanner.Core.Planning.PlannerStage Stage, bool IsSkylightEntry = false) : VoiceCommand;
public sealed record IncrementGuardsDownVoiceCommand : VoiceCommand;
public sealed record IncrementCamerasDownVoiceCommand : VoiceCommand;
public sealed record DisableNamedCameraVoiceCommand(string CameraId) : VoiceCommand;
public sealed record DisableShowroomByButtonVoiceCommand : VoiceCommand;
public sealed record EnterSewerRouteVoiceCommand : VoiceCommand;
public sealed record ApplySewerRouteVoiceCommand(string RouteText) : VoiceCommand;
public sealed record ExitSewerRouteVoiceCommand : VoiceCommand;

public enum VoiceParseDisposition
{
    Parsed,
    Ignored,
    Rejected,
}

public sealed record VoiceCommandParseResult(
    VoiceParseDisposition Disposition,
    VoiceCommand? Command = null,
    string? Error = null);

public sealed record VoiceDispatchResult(
    VoiceCommandParseResult ParseResult,
    bool Applied,
    string? UpdatedLootLocationId = null,
    int? ScopedValue = null,
    string? Error = null);
