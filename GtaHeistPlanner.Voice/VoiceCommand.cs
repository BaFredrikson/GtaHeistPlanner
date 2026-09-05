namespace GtaHeistPlanner.Voice;

public abstract record VoiceCommand;

public sealed record ActivateScopeOutCommand : VoiceCommand;

public sealed record DeactivateScopeOutCommand : VoiceCommand;

public sealed record RecordScopedLootCommand(string LootLocationId, int? ScopedValue) : VoiceCommand;

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
