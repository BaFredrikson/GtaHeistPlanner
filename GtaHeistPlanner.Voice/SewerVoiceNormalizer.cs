using System.Text.RegularExpressions;

namespace GtaHeistPlanner.Voice;

public static partial class SewerVoiceNormalizer
{
    private static readonly IReadOnlyDictionary<string, string> Numbers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["one"] = "1", ["won"] = "1", ["two"] = "2", ["too"] = "2", ["to"] = "2",
        ["three"] = "3", ["free"] = "3", ["tree"] = "3", ["four"] = "4", ["for"] = "4",
        ["five"] = "5", ["six"] = "6", ["sicks"] = "6",
    };

    private static readonly IReadOnlyDictionary<string, char> Tunnels = new Dictionary<string, char>(StringComparer.Ordinal)
    {
        ["a"] = 'A', ["alpha"] = 'A',
        ["b"] = 'B', ["bee"] = 'B', ["be"] = 'B', ["bravo"] = 'B',
        ["c"] = 'C', ["see"] = 'C', ["sea"] = 'C', ["charlie"] = 'C',
        ["d"] = 'D', ["dee"] = 'D', ["the"] = 'D', ["delta"] = 'D',
        ["e"] = 'E', ["echo"] = 'E',
    };

    public static bool TryNormalize(string text, out string canonicalRoute, out string? error)
    {
        ArgumentNullException.ThrowIfNull(text);
        var tokens = TokenPattern().Matches(text.ToLowerInvariant()).Select(match => match.Value).ToList();
        tokens.RemoveAll(token => token is "chamber" or "number" or "tunnel" or "please");
        var instructions = new List<string>();

        for (var index = 0; index < tokens.Count;)
        {
            var chamber = Numbers.TryGetValue(tokens[index], out var spokenNumber) ? spokenNumber : tokens[index];
            if (!int.TryParse(chamber, out var chamberNumber) || index + 1 >= tokens.Count || !Tunnels.TryGetValue(tokens[index + 1], out var tunnel))
            {
                canonicalRoute = string.Join(' ', instructions);
                error = $"Could not interpret sewer route near '{tokens[index]}'. Say a chamber number followed by tunnel A through E.";
                return false;
            }
            if (chamberNumber is < 1 or > 6)
            {
                var finalDigit = chamberNumber % 10;
                if (finalDigit is < 1 or > 6)
                {
                    canonicalRoute = string.Join(' ', instructions);
                    error = $"Chamber '{chamberNumber}' cannot be reduced to a valid sewer chamber.";
                    return false;
                }
                chamberNumber = finalDigit;
            }
            instructions.Add($"{chamberNumber}{tunnel}");
            index += 2;
        }

        canonicalRoute = string.Join(' ', instructions);
        error = instructions.Count == 0 ? "No chamber-and-tunnel instructions were heard." : null;
        return instructions.Count > 0;
    }

    public static bool IsActivationPhrase(string text, bool allowAmbiguous)
    {
        var normalized = SpokenTextNormalizer.Normalize(text);
        if (normalized is "sewer route" or "sewer" or "sower route" or "sower" or "so we re route") return true;
        return allowAmbiguous && normalized is "route" or "so we re" or "so we re uh" or "so we re up";
    }

    [GeneratedRegex(@"[a-z]+|\d+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
