using System.Text;

namespace GtaHeistPlanner.Voice;

public static class SpokenTextNormalizer
{
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var character in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSpace && builder.Length > 0)
                    builder.Append(' ');
                builder.Append(character);
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }
        return builder.ToString();
    }
}
