namespace GtaHeistPlanner.Voice;

public enum PendingVoiceMode
{
    None,
    LootValue,
    VaultCode,
    SewerRoute,
}

public sealed class VoiceCommandContext
{
    public string? LastMentionedLootId { get; set; }
    public string? PendingLootValueTargetId { get; set; }
    public bool ScopeOutActive { get; set; }
    public bool AwaitingSewerRoute { get; set; }
    public bool AwaitingVaultCode { get; set; }
    public string? LastReversibleAction { get; set; }

    public PendingVoiceMode PendingMode => AwaitingSewerRoute ? PendingVoiceMode.SewerRoute
        : AwaitingVaultCode ? PendingVoiceMode.VaultCode
        : PendingLootValueTargetId is not null ? PendingVoiceMode.LootValue
        : PendingVoiceMode.None;

    public void Reset()
    {
        LastMentionedLootId = null;
        PendingLootValueTargetId = null;
        ScopeOutActive = false;
        AwaitingSewerRoute = false;
        AwaitingVaultCode = false;
        LastReversibleAction = null;
    }
}
