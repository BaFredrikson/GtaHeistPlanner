using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GtaHeistPlanner.Launcher;

internal static partial class Program
{
    private const uint ErrorIcon = 0x00000010;

    [STAThread]
    private static int Main(string[] args)
    {
        var launcherDirectory = AppContext.BaseDirectory;
        var applicationPath = Path.Combine(launcherDirectory, "app", "GtaHeistPlanner.App.exe");

        if (!File.Exists(applicationPath))
        {
            ShowError(
                "GTA Heist Planner could not be started because its application files are missing.\n\n" +
                $"Expected file:\n{applicationPath}\n\nPlease extract the complete release ZIP and try again.");
            return 2;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = applicationPath,
                WorkingDirectory = launcherDirectory,
                UseShellExecute = false,
            };

            foreach (var argument in args)
                startInfo.ArgumentList.Add(argument);

            Process.Start(startInfo);
            return 0;
        }
        catch (Exception exception)
        {
            ShowError($"GTA Heist Planner could not be started.\n\n{exception.Message}");
            return 1;
        }
    }

    private static void ShowError(string message) =>
        MessageBox(IntPtr.Zero, message, "GTA Heist Planner", ErrorIcon);

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(IntPtr window, string text, string caption, uint type);
}
