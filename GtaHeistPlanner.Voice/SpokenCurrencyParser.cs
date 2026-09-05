namespace GtaHeistPlanner.Voice;

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
        var normalized = SpokenTextNormalizer.Normalize(text);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token != "and")
            .ToArray();
        value = 0;
        if (tokens.Length == 0)
            return false;

        var multiplier = 1;
        if (tokens[^1] is "thousand" or "k")
        {
            multiplier = 1000;
            tokens = tokens[..^1];
        }
        if (tokens.Length == 0)
            return false;

        if (tokens.All(token => token.All(char.IsDigit)) &&
            int.TryParse(string.Concat(tokens), out var groupedDigits))
            return TryMultiply(groupedDigits, multiplier, out value);

        if (tokens.Length == 1 && int.TryParse(tokens[0], out var digits))
            return TryMultiply(digits, multiplier, out value);

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
        return current > 0 && TryMultiply(current, multiplier, out value);
    }

    private static bool TryMultiply(int number, int multiplier, out int value)
    {
        var result = (long)number * multiplier;
        value = result is > 0 and <= int.MaxValue ? (int)result : 0;
        return value > 0;
    }
}
