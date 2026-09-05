namespace GtaHeistPlanner.App.Services;

/// <summary>
/// Locates persistent, authored project data. During development this resolves to
/// GtaHeistPlanner.App/Data/kortz; a published build uses its adjacent Data folder.
/// </summary>
public static class ProjectDataPath
{
    private const string AppProjectFile = "GtaHeistPlanner.App.csproj";

    public static string KortzFile(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        foreach (var startPath in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, AppProjectFile)))
                {
                    return Path.Combine(directory.FullName, "Data", "kortz", fileName);
                }

                var appDirectory = Path.Combine(directory.FullName, "GtaHeistPlanner.App");
                if (File.Exists(Path.Combine(appDirectory, AppProjectFile)))
                {
                    return Path.Combine(appDirectory, "Data", "kortz", fileName);
                }
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "Data", "kortz", fileName);
    }
}
