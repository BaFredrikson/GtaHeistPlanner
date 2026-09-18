using System.Security.Cryptography;
using System.Text;

namespace GtaHeistPlanner.App.Services;

public sealed class ProtectedVoiceSecretStore
{
    private readonly string _directory;

    public ProtectedVoiceSecretStore(string? directory = null) =>
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GtaHeistPlanner", "secrets");

    public void Set(string name, string? secret)
    {
        var path = PathFor(name);
        if (string.IsNullOrWhiteSpace(secret))
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Protected voice credentials currently require Windows DPAPI.");
        Directory.CreateDirectory(_directory);
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, protectedBytes);
    }

    public string? Get(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path)) return null;
        if (!OperatingSystem.IsWindows()) return null;
        var bytes = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }

    public bool Exists(string name) => File.Exists(PathFor(name));
    private string PathFor(string name) => Path.Combine(_directory, $"{name}.bin");
}
