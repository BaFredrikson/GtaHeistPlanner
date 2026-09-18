namespace GtaHeistPlanner.Voice;

public static class TranscriptSanitizer
{
    private static readonly HashSet<string> NonSpeechMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "BLANK AUDIO",
    };

    public static bool TrySanitize(string? transcript, out string sanitized)
    {
        sanitized = transcript?.Trim() ?? string.Empty;
        if (sanitized.Length == 0) return false;

        var start = 0;
        var end = sanitized.Length - 1;
        while (start <= end && (char.IsWhiteSpace(sanitized[start]) || char.IsPunctuation(sanitized[start]))) start++;
        while (end >= start && (char.IsWhiteSpace(sanitized[end]) || char.IsPunctuation(sanitized[end]))) end--;
        var marker = start <= end ? sanitized[start..(end + 1)] : string.Empty;
        marker = string.Join(' ', marker.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (NonSpeechMarkers.Contains(marker))
        {
            sanitized = string.Empty;
            return false;
        }
        return true;
    }
}
