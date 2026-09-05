namespace GtaHeistPlanner.App.Services;

public static class DevelopmentEnvironmentLoader
{
    public static void LoadFromWorkingTree()
    {
        var environmentFile = FindEnvironmentFile(Directory.GetCurrentDirectory())
            ?? FindEnvironmentFile(AppContext.BaseDirectory);
        if (environmentFile is not null)
            LoadFile(environmentFile);
    }

    public static void LoadFile(string path)
    {
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line[7..].TrimStart();

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;
            var name = line[..separator].Trim();
            if (name.Length == 0 || Environment.GetEnvironmentVariable(name) is not null)
                continue;

            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                value = value[1..^1];
            Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
        }
    }

    private static string? FindEnvironmentFile(string startDirectory)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(startDirectory)); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, ".env");
            if (File.Exists(candidate))
                return candidate;
            if (File.Exists(Path.Combine(directory.FullName, "GtaHeistPlanner.slnx")))
                return null;
        }
        return null;
    }
}
