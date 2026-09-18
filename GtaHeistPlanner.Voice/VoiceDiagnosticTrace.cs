using System.Collections.Concurrent;

namespace GtaHeistPlanner.Voice;

public sealed class VoiceDiagnosticTrace
{
    private const int MaximumEntries = 500;
    private readonly ConcurrentQueue<string> _entries = new();
    private readonly ConcurrentDictionary<string, byte> _once = new(StringComparer.Ordinal);
    private readonly Func<bool>? _uiThreadAccess;

    public VoiceDiagnosticTrace(Func<bool>? uiThreadAccess = null) => _uiThreadAccess = uiThreadAccess;

    public IReadOnlyList<string> Entries => _entries.ToArray();

    public void Record(string message)
    {
        _entries.Enqueue($"{DateTimeOffset.Now:O} {message}");
        while (_entries.Count > MaximumEntries)
            _entries.TryDequeue(out _);
    }

    public void RecordMilestone(string operation) => Record(
        $"[thread {Environment.CurrentManagedThreadId}; UI access: {(_uiThreadAccess is null ? "unknown" : _uiThreadAccess().ToString())}] {operation}");

    public void RecordOnce(string key, string message)
    {
        if (_once.TryAdd(key, 0))
            Record(message);
    }

    public void RecordException(string step, Exception exception)
    {
        var nativeError = exception is System.ComponentModel.Win32Exception win32
            ? $"; Win32 NativeErrorCode={win32.NativeErrorCode}"
            : string.Empty;
        Record($"{step} failed. Exception type={exception.GetType().FullName}; HResult=0x{exception.HResult:X8}; Win32 low word={exception.HResult & 0xFFFF}{nativeError}. Exception follows without replacement:\n{exception}");
        if (exception.InnerException is not null)
            Record($"Inner exception:\n{exception.InnerException}");
    }
}
