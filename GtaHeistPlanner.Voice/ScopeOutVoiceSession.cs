using GtaHeistPlanner.Core.Loot;

namespace GtaHeistPlanner.Voice;

public sealed class ScopeOutVoiceSession(IEnumerable<LootSpawnDefinition> definitions, LootRunState lootRun)
{
    private readonly VoiceCommandParser _parser = new(definitions);
    private readonly LootRunState _lootRun = lootRun;

    public ScopeOutSessionState State { get; private set; }

    public void SetListening(bool listening) => State = listening
        ? ScopeOutSessionState.WaitingForActivation
        : ScopeOutSessionState.Inactive;

    public VoiceDispatchResult Process(string recognizedText)
    {
        var parse = _parser.Parse(recognizedText, State);
        if (parse.Disposition != VoiceParseDisposition.Parsed || parse.Command is null)
            return new(parse, false, Error: parse.Error);

        switch (parse.Command)
        {
            case ActivateScopeOutCommand:
                State = ScopeOutSessionState.Active;
                return new(parse, true);
            case DeactivateScopeOutCommand:
                State = ScopeOutSessionState.WaitingForActivation;
                return new(parse, true);
            case RecordScopedLootCommand command:
                _lootRun.RecordScopedLoot(command.LootLocationId, command.ScopedValue);
                return new(parse, true, command.LootLocationId, command.ScopedValue);
            default:
                return new(parse, false, Error: "Unsupported voice command.");
        }
    }
}
