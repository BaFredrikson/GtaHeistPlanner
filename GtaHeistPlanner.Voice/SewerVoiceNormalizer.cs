using System.Text.RegularExpressions;

namespace GtaHeistPlanner.Voice;

public static partial class SewerVoiceNormalizer
{
    private static readonly IReadOnlyDictionary<string, string> Numbers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["one"] = "1", ["two"] = "2", ["three"] = "3", ["four"] = "4",
        ["five"] = "5", ["six"] = "6", ["seven"] = "7", ["eight"] = "8", ["nine"] = "9",
    };

    private static readonly IReadOnlyDictionary<string, char> Tunnels = new Dictionary<string, char>(StringComparer.Ordinal)
    {
        ["a"] = 'A', ["alpha"] = 'A',
        ["b"] = 'B', ["bee"] = 'B', ["bravo"] = 'B',
        ["c"] = 'C', ["see"] = 'C', ["sea"] = 'C', ["charlie"] = 'C',
        ["d"] = 'D', ["dee"] = 'D', ["delta"] = 'D',
        ["e"] = 'E', ["echo"] = 'E',
    };

    public static bool TryNormalize(string text, out string canonicalRoute, out string? error)
    {
        ArgumentNullException.ThrowIfNull(text);
        var tokens = TokenPattern().Matches(text.ToLowerInvariant()).Select(match => match.Value).ToList();
        tokens.RemoveAll(token => token == "chamber");
        var instructions = new List<string>();

        for (var index = 0; index < tokens.Count;)
        {
            var chamber = Numbers.TryGetValue(tokens[index], out var spokenNumber) ? spokenNumber : tokens[index];
            if (!int.TryParse(chamber, out _) || index + 1 >= tokens.Count || !Tunnels.TryGetValue(tokens[index + 1], out var tunnel))
            {
                canonicalRoute = string.Join(' ', instructions);
                error = $"Could not interpret sewer route near '{tokens[index]}'. Say a chamber number followed by tunnel A through E.";
                return false;
            }
            instructions.Add($"{chamber}{tunnel}");
            index += 2;
        }

        canonicalRoute = string.Join(' ', instructions);
        error = instructions.Count == 0 ? "No chamber-and-tunnel instructions were heard." : null;
        return instructions.Count > 0;
    }

    [GeneratedRegex(@"[a-z]+|\d+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
