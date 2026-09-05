namespace GtaHeistPlanner.Core.Sewer;

public static class SewerRouteParser
{
    private static readonly System.Text.RegularExpressions.Regex InstructionPattern = new(
        @"(?:\bchamber\s+)?(?<chamber>\d+)\s*(?<tunnel>[a-z])\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static IReadOnlyList<SewerInstruction> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("Enter at least one sewer instruction, such as 2C.");

        var instructions = new List<SewerInstruction>();
        foreach (System.Text.RegularExpressions.Match match in InstructionPattern.Matches(text))
        {
            var chamber = int.Parse(match.Groups["chamber"].Value, System.Globalization.CultureInfo.InvariantCulture);
            instructions.Add(new SewerInstruction(chamber, match.Groups["tunnel"].Value[0]));
        }

        var remainder = InstructionPattern.Replace(text, string.Empty);
        if (instructions.Count == 0 || remainder.Any(character => !char.IsWhiteSpace(character) && character is not ',' and not ';' and not '-' and not '>'))
            throw new FormatException($"Invalid sewer route '{text}'. Use chamber-letter instructions such as 2C 3C 4D.");
        return instructions;
    }
}
