using GtaHeistPlanner.App.Services;
using GtaHeistPlanner.Core.Settings;

namespace GtaHeistPlanner.Tests.Settings;

public sealed class ApplicationSettingsStoreTests
{
    [Fact]
    public void SavesAndLoadsMachinePreferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"GtaHeistPlanner-tests-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new ApplicationSettingsStore(path);
            var expected = new ApplicationSettings
            {
                RecordingDeviceId = "device-id",
                RecordingDeviceName = "Test microphone",
                MicrophoneEnabled = true,
                DeveloperMode = true,
            };

            store.Save(expected);
            var actual = store.Load();

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
