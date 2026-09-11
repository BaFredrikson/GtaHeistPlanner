using GtaHeistPlanner.Core.Loot;
using GtaHeistPlanner.Core.Maps;
using GtaHeistPlanner.Core.Planning;
using GtaHeistPlanner.Core.Security;

namespace GtaHeistPlanner.Voice;

public sealed class VoiceCommandParser
{
    private readonly IReadOnlyList<AliasEntry> _lootAliases;
    private readonly IReadOnlyList<AliasEntry> _mapAliases;
    private readonly IReadOnlyList<AliasEntry> _cameraAliases;

    public VoiceCommandParser(IEnumerable<LootSpawnDefinition> lootDefinitions, IEnumerable<MapDefinition>? maps = null,
        IEnumerable<SecurityCameraDefinition>? cameras = null)
    {
        _lootAliases = BuildAliases(lootDefinitions.SelectMany(definition =>
            definition.VoiceAliases.Append(definition.Name).Select(alias => (alias, definition.Id))));
        _mapAliases = BuildAliases((maps ?? []).SelectMany(map => MapAliases(map).Select(alias => (alias, map.Id))));
        _cameraAliases = BuildAliases((cameras ?? []).SelectMany(camera =>
            new[] { $"{camera.Name} down", $"{camera.Name} disabled" }.Select(alias => (alias, camera.Id))));
    }

    public VoiceCommandParseResult Parse(string recognizedText, ScopeOutSessionState state)
    {
        var context = new VoiceCommandContext { ScopeOutActive = state == ScopeOutSessionState.Active };
        return Parse(recognizedText, context, PlannerStage.Preparation);
    }

    public VoiceCommandParseResult Parse(string recognizedText, VoiceCommandContext context, PlannerStage stage)
    {
        var text = SpokenTextNormalizer.Normalize(recognizedText);
        if (text.Length == 0) return Ignored();

        if (text == "undo") return Parsed(new UndoVoiceCommand());
        if (text is "back up" or "pull back") return Parsed(new ExitMapFocusVoiceCommand());
        if (text == "scope out") return Parsed(new ActivateScopeOutCommand());
        if (text == "stop scope out") return Parsed(new DeactivateScopeOutCommand());
        if (text is "plan out" or "plan it" or "pan out" or "pan it" or "overview")
            return Parsed(new ChangeStageVoiceCommand(PlannerStage.Planning));
        if (text is "heist start" or "start heist" or "start infiltration" or "infiltration start")
            return Parsed(new ChangeStageVoiceCommand(PlannerStage.HeistInfiltration));
        if (text == "going down skylight") return Parsed(new ChangeStageVoiceCommand(PlannerStage.HeistActivity, true));
        if (text is "using access codes" or "alpha mail arriving") return Parsed(new ChangeStageVoiceCommand(PlannerStage.HeistActivity));
        if (text == "outta the sewers") return Parsed(new ExitSewerRouteVoiceCommand());
        if (text == "sewer grate reached") return Parsed(new EnterSewerRouteVoiceCommand());
        if (VoiceCommandCatalog.GuardDown.Aliases.Contains(text, StringComparer.Ordinal)) return Parsed(new IncrementGuardsDownVoiceCommand());
        if (VoiceCommandCatalog.CameraDown.Aliases.Contains(text, StringComparer.Ordinal)) return Parsed(new IncrementCamerasDownVoiceCommand());
        if (VoiceCommandCatalog.ShowroomButton.Aliases.Contains(text, StringComparer.Ordinal)) return Parsed(new DisableShowroomByButtonVoiceCommand());
        var namedCamera = ResolveExactAlias(text, _cameraAliases, id => new DisableNamedCameraVoiceCommand(id), "camera");
        if (namedCamera.Disposition != VoiceParseDisposition.Rejected || _cameraAliases.Any(alias => alias.Alias == text)) return namedCamera;
        if (text is "special loot" or "buyers request" or "buyer s request") return Parsed(new ToggleSpecialLootCommand());

        if (text.StartsWith("pull up ", StringComparison.Ordinal))
            return ResolveExactAlias(text[8..], _mapAliases, id => new FocusMapVoiceCommand(id), "map");
        if (text == "vault code") return Parsed(new SetVaultCodeVoiceCommand(null));
        if (text.StartsWith("vault code ", StringComparison.Ordinal))
            return TryParseVaultCode(text[11..], out var inlineCode) ? Parsed(new SetVaultCodeVoiceCommand(inlineCode)) : Rejected("Vault code must contain three number groups.");

        if (text.StartsWith("sewer route ", StringComparison.Ordinal))
            return NormalizeSewerRoute(recognizedText[(recognizedText.IndexOf("route", StringComparison.OrdinalIgnoreCase) + 5)..]);

        if (context.AwaitingVaultCode)
            return TryParseVaultCode(text, out var pendingCode) ? Parsed(new SetVaultCodeVoiceCommand(pendingCode)) : Rejected("The pending vault code was not understood.");
        if (context.AwaitingSewerRoute) return NormalizeSewerRoute(recognizedText);
        if (context.ScopeOutActive && context.PendingLootValueTargetId is not null && SpokenCurrencyParser.TryParseDetailed(text, out var pendingValue))
            return Parsed(new SetPendingLootValueCommand(pendingValue.Value, pendingValue));
        if (context.ScopeOutActive && context.NumericContinuationTargetId is { } continuationTarget &&
            SpokenCurrencyParser.TryParseDetailed(text, out var remainder) && !remainder.UsedThousandsUnit && remainder.Value is >= 1 and <= 999)
            return Parsed(new ContinueLootValueCommand(continuationTarget, remainder.Value));

        if (stage is not (PlannerStage.Preparation or PlannerStage.HeistActivity) ||
            (stage == PlannerStage.Preparation && !context.ScopeOutActive)) return Ignored();
        return ResolveLoot(text);
    }

    private VoiceCommandParseResult ResolveLoot(string text)
    {
        var matching = _lootAliases.Where(entry => text == entry.Alias || text.StartsWith(entry.Alias + " ", StringComparison.Ordinal))
            .Select(entry => (entry, remainder: text.Length == entry.Alias.Length ? "" : text[(entry.Alias.Length + 1)..])).ToArray();
        var candidates = matching.Where(item => item.remainder.Length == 0 || SpokenCurrencyParser.TryParseDetailed(item.remainder, out _)).ToArray();
        if (candidates.Length == 0) return Rejected(matching.Length > 0 ? "The loot location was recognized, but its spoken value was not understood." : "No loot alias could be resolved.");
        var longest = candidates.Max(item => item.entry.Alias.Length);
        var best = candidates.Where(item => item.entry.Alias.Length == longest).ToArray();
        var ids = best.Select(item => item.entry.TargetId).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length != 1) return Rejected($"Loot alias is ambiguous: {string.Join(", ", ids)}.");
        SpokenNumberResult? value = null;
        if (best[0].remainder.Length > 0 && SpokenCurrencyParser.TryParseDetailed(best[0].remainder, out var parsed)) value = parsed;
        return Parsed(new RecordScopedLootCommand(ids[0], value?.Value, value));
    }

    private static VoiceCommandParseResult ResolveExactAlias(string alias, IReadOnlyList<AliasEntry> aliases, Func<string, VoiceCommand> factory, string kind)
    {
        var ids = aliases.Where(entry => entry.Alias == alias).Select(entry => entry.TargetId).Distinct(StringComparer.Ordinal).ToArray();
        return ids.Length switch { 1 => Parsed(factory(ids[0])), > 1 => Rejected($"The {kind} alias '{alias}' is ambiguous."), _ => Rejected($"No {kind} alias could be resolved for '{alias}'.") };
    }

    private static IReadOnlyList<AliasEntry> BuildAliases(IEnumerable<(string alias, string id)> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value.alias)).Select(value => new AliasEntry(SpokenTextNormalizer.Normalize(value.alias), value.id))
        .Distinct().OrderByDescending(entry => entry.Alias.Length).ToArray();

    private static IEnumerable<string> MapAliases(MapDefinition map)
    {
        yield return map.DisplayName;
        yield return map.Id.Replace('-', ' ');
        var normalized = SpokenTextNormalizer.Normalize(map.DisplayName);
        if (normalized.StartsWith("exterior ", StringComparison.Ordinal)) yield return normalized[9..];
    }

    private static bool TryParseVaultCode(string text, out string code)
    {
        var tokens = SpokenTextNormalizer.Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var groups = new List<int>();
        for (var index = 0; index < tokens.Length;)
        {
            if (int.TryParse(tokens[index], out var digits) && digits is >= 0 and <= 99) { groups.Add(digits); index++; continue; }
            var take = index + 1 < tokens.Length && IsTens(tokens[index]) ? 2 : 1;
            if (!SpokenCurrencyParser.TryParse(string.Join(' ', tokens.Skip(index).Take(take)), out var spoken) || spoken > 99) { code = ""; return false; }
            groups.Add(spoken); index += take;
        }
        if (groups.Count != 3) { code = ""; return false; }
        code = string.Join('-', groups.Select(value => value.ToString("00")));
        return true;
    }

    private static bool IsTens(string token) => token is "twenty" or "thirty" or "forty" or "fifty" or "sixty" or "seventy" or "eighty" or "ninety";
    private static VoiceCommandParseResult NormalizeSewerRoute(string text) =>
        SewerVoiceNormalizer.TryNormalize(text, out var route, out var error)
            ? Parsed(new ApplySewerRouteVoiceCommand(route))
            : Rejected(error!);
    private static VoiceCommandParseResult Parsed(VoiceCommand command) => new(VoiceParseDisposition.Parsed, command);
    private static VoiceCommandParseResult Rejected(string error) => new(VoiceParseDisposition.Rejected, Error: error);
    private static VoiceCommandParseResult Ignored() => new(VoiceParseDisposition.Ignored);
    private sealed record AliasEntry(string Alias, string TargetId);
}
