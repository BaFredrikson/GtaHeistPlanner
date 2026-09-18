namespace GtaHeistPlanner.Core.Settings;

public sealed class IniDocument
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections =
        new(StringComparer.OrdinalIgnoreCase);

    public string? Get(string section, string key) =>
        _sections.TryGetValue(section, out var values) && values.TryGetValue(key, out var value) ? value : null;

    public void Set(string section, string key, string? value)
    {
        if (!_sections.TryGetValue(section, out var values))
            _sections[section] = values = new(StringComparer.OrdinalIgnoreCase);
        if (value is null) values.Remove(key); else values[key] = value;
    }

    public static IniDocument Load(TextReader reader)
    {
        var document = new IniDocument();
        var section = string.Empty;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']')) { section = line[1..^1].Trim(); continue; }
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            document.Set(section, line[..separator].Trim(), line[(separator + 1)..].Trim());
        }
        return document;
    }

    public void Save(TextWriter writer)
    {
        foreach (var section in _sections.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (section.Key.Length > 0) writer.WriteLine($"[{section.Key}]");
            foreach (var pair in section.Value.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                writer.WriteLine($"{pair.Key}={pair.Value}");
            writer.WriteLine();
        }
    }
}

