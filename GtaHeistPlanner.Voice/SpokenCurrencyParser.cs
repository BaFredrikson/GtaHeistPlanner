namespace GtaHeistPlanner.Voice;

public sealed record SpokenNumberResult(int Value, bool UsedThousandsUnit, bool HasExplicitSubThousandComponent);

public static class SpokenCurrencyParser
{
    private static readonly IReadOnlyDictionary<string, int> SmallNumbers = new Dictionary<string, int>
    {
        ["zero"] = 0, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4,
        ["five"] = 5, ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9,
        ["ten"] = 10, ["eleven"] = 11, ["twelve"] = 12, ["thirteen"] = 13,
        ["fourteen"] = 14, ["fifteen"] = 15, ["sixteen"] = 16, ["seventeen"] = 17,
        ["eighteen"] = 18, ["nineteen"] = 19, ["twenty"] = 20, ["thirty"] = 30,
        ["forty"] = 40, ["fifty"] = 50, ["sixty"] = 60, ["seventy"] = 70,
        ["eighty"] = 80, ["ninety"] = 90,
    };

    public static bool TryParse(string text, out int value)
    {
        if (TryParseDetailed(text, out var result))
        {
            value = result.Value;
            return true;
        }
        value = 0;
        return false;
    }

    public static bool TryParseDetailed(string text, out SpokenNumberResult result)
    {
        var normalized = SpokenTextNormalizer.Normalize(text);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token != "and")
            .ToArray();
        result = new(0, false, false);
        if (tokens.Length == 0)
            return false;

        var thousandsIndex = Array.FindIndex(tokens, token => token is "thousand" or "k");
        if (thousandsIndex >= 0)
        {
            if (thousandsIndex == 0 || Array.FindIndex(tokens, thousandsIndex + 1, token => token is "thousand" or "k") >= 0 ||
                !TryParseUnderThousand(tokens[..thousandsIndex], out var thousands))
                return false;
            var remainderTokens = tokens[(thousandsIndex + 1)..];
            var hasRemainder = remainderTokens.Length > 0;
            var remainder = 0;
            if (hasRemainder && !TryParseUnderThousand(remainderTokens, out remainder)) return false;
            var total = (long)thousands * 1000 + remainder;
            if (total is <= 0 or > int.MaxValue) return false;
            result = new((int)total, true, hasRemainder);
            return true;
        }

        if (!TryParseUnderThousand(tokens, out var value)) return false;
        result = new(value, false, false);
        return true;
    }

    private static bool TryParseUnderThousand(string[] tokens, out int value)
    {
        value = 0;
        if (tokens.Length == 0) return false;
        if (tokens.All(token => token.All(char.IsDigit)) &&
            int.TryParse(string.Concat(tokens), out var groupedDigits))
        {
            value = groupedDigits;
            return value > 0;
        }

        if (tokens.Length == 1 && int.TryParse(tokens[0], out var digits))
        {
            value = digits;
            return value > 0;
        }

        var current = 0;
        foreach (var token in tokens)
        {
            if (SmallNumbers.TryGetValue(token, out var number))
                current += number;
            else if (token == "hundred" && current is > 0 and < 10)
                current *= 100;
            else
                return false;
        }
        value = current;
        return value > 0;
    }
}
