namespace GtaHeistPlanner.Core.Sewer;

public static class SewerRouteParser
{
    public static SewerRoute Parse(string text)
    {
        var tokens = text.Split([' ', ',', ';', '-', '>'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var turns = new List<SewerTurn>();
        foreach (var token in tokens)
        {
            if (token.Equals("escape", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("route", StringComparison.OrdinalIgnoreCase))
                continue;
            if (token.Equals("l", StringComparison.OrdinalIgnoreCase) || token.Equals("left", StringComparison.OrdinalIgnoreCase))
                turns.Add(SewerTurn.Left);
            else if (token.Equals("r", StringComparison.OrdinalIgnoreCase) || token.Equals("right", StringComparison.OrdinalIgnoreCase))
                turns.Add(SewerTurn.Right);
            else
                throw new FormatException($"Unknown sewer turn '{token}'. Use L/R or left/right.");
        }
        if (turns.Count == 0)
            throw new FormatException("Enter at least one sewer turn.");
        return new SewerRoute(turns);
    }
}
