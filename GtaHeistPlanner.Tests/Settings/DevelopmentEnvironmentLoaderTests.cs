using GtaHeistPlanner.App.Services;

namespace GtaHeistPlanner.Tests.Settings;

public sealed class DevelopmentEnvironmentLoaderTests
{
    [Fact]
    public void LoadFile_LoadsMissingVariablesAndParsesEqualsInValue()
    {
        var name = $"GTA_HEIST_TEST_{Guid.NewGuid():N}";
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, $"# local settings{Environment.NewLine}{name}=value=with=equals");
            DevelopmentEnvironmentLoader.LoadFile(path);
            Assert.Equal("value=with=equals", Environment.GetEnvironmentVariable(name));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFile_DoesNotOverwriteExistingEnvironmentVariable()
    {
        var name = $"GTA_HEIST_TEST_{Guid.NewGuid():N}";
        var path = Path.GetTempFileName();
        try
        {
            Environment.SetEnvironmentVariable(name, "from-operating-system");
            File.WriteAllText(path, $"{name}=from-dotenv");
            DevelopmentEnvironmentLoader.LoadFile(path);
            Assert.Equal("from-operating-system", Environment.GetEnvironmentVariable(name));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
            File.Delete(path);
        }
    }
}
