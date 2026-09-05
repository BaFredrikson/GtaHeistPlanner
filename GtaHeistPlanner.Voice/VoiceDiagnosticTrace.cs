using System.Collections.Concurrent;

namespace GtaHeistPlanner.Voice;

public sealed class VoiceDiagnosticTrace
{
    private readonly ConcurrentQueue<string> _entries = new();
    private readonly ConcurrentDictionary<string, byte> _once = new(StringComparer.Ordinal);

    public IReadOnlyList<string> Entries => _entries.ToArray();

    public void Record(string message) =>
        _entries.Enqueue($"{DateTimeOffset.Now:O} {message}");

    public void RecordOnce(string key, string message)
    {
        if (_once.TryAdd(key, 0))
            Record(message);
    }

    public void RecordException(string step, Exception exception)
    {
        Record($"{step} failed. Exception follows without replacement:\n{exception}");
        if (exception.InnerException is not null)
            Record($"Inner exception:\n{exception.InnerException}");
    }
}
