using GtaHeistPlanner.Core.Loot;

namespace GtaHeistPlanner.Voice;

public sealed class VoiceCommandParser(IEnumerable<LootSpawnDefinition> lootDefinitions)
{
    private readonly IReadOnlyList<AliasEntry> _aliases = lootDefinitions
        .SelectMany(definition => definition.VoiceAliases.Append(definition.Name)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => new AliasEntry(SpokenTextNormalizer.Normalize(alias), definition.Id)))
        .Distinct()
        .OrderByDescending(entry => entry.Alias.Length)
        .ToArray();

    public VoiceCommandParseResult Parse(string recognizedText, ScopeOutSessionState state)
    {
        var text = SpokenTextNormalizer.Normalize(recognizedText);
        if (text == "scope out")
            return new(VoiceParseDisposition.Parsed, new ActivateScopeOutCommand());
        if (text == "stop scope out")
            return state == ScopeOutSessionState.Active
                ? new(VoiceParseDisposition.Parsed, new DeactivateScopeOutCommand())
                : new(VoiceParseDisposition.Ignored);
        if (state != ScopeOutSessionState.Active)
            return new(VoiceParseDisposition.Ignored);

        var matchingAliases = _aliases
            .Where(entry => text == entry.Alias || text.StartsWith(entry.Alias + " ", StringComparison.Ordinal))
            .Select(entry => new
            {
                Entry = entry,
                Remainder = text.Length == entry.Alias.Length ? string.Empty : text[(entry.Alias.Length + 1)..],
            })
            .ToArray();
        var candidates = matchingAliases
            .Where(candidate => candidate.Remainder.Length == 0 || SpokenCurrencyParser.TryParse(candidate.Remainder, out _))
            .ToArray();

        if (candidates.Length == 0)
            return new(VoiceParseDisposition.Rejected, Error: matchingAliases.Length > 0
                ? "The loot location was recognized, but its spoken value was not understood."
                : "No loot alias could be resolved.");

        var longestAliasLength = candidates.Max(candidate => candidate.Entry.Alias.Length);
        var best = candidates.Where(candidate => candidate.Entry.Alias.Length == longestAliasLength).ToArray();
        var locationIds = best.Select(candidate => candidate.Entry.LootLocationId).Distinct(StringComparer.Ordinal).ToArray();
        if (locationIds.Length != 1)
            return new(VoiceParseDisposition.Rejected, Error: $"Loot alias is ambiguous: {string.Join(", ", locationIds)}.");

        var remainder = best[0].Remainder;
        int? value = null;
        if (remainder.Length > 0 && SpokenCurrencyParser.TryParse(remainder, out var parsedValue))
            value = parsedValue;
        return new(VoiceParseDisposition.Parsed, new RecordScopedLootCommand(locationIds[0], value));
    }

    private sealed record AliasEntry(string Alias, string LootLocationId);
}
