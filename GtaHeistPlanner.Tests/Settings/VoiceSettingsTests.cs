using GtaHeistPlanner.App.Services;
using GtaHeistPlanner.Core.Settings;

namespace GtaHeistPlanner.Tests.Settings;

public sealed class VoiceSettingsTests
{
    [Theory]
    [InlineData(TranscriptionProviderKind.LocalWhisper)]
    [InlineData(TranscriptionProviderKind.OpenAi)]
    [InlineData(TranscriptionProviderKind.Custom)]
    public void IniRoundTripPreservesProviderConfigurationWithoutSecrets(TranscriptionProviderKind provider)
    {
        var settings = new ApplicationSettings
        {
            VoiceProvider = provider, RecordingDeviceId = "endpoint-1", RecordingDeviceName = "Mic",
            LocalWhisperModel = "base.en", LocalWhisperCompute = LocalWhisperCompute.Cpu,
            OpenAiTranscriptionModel = "gpt-live-transcribe", CustomTranscriptionEndpoint = "http://localhost:8000/v1/",
            CustomTranscriptionModel = "whisper", HasOpenAiApiKey = true, HasCustomApiKey = true,
        };
        using var writer = new StringWriter(); ApplicationSettingsIni.Save(writer, settings);
        var ini = writer.ToString();
        var restored = ApplicationSettingsIni.Load(new StringReader(ini));

        Assert.Equal(provider, restored.VoiceProvider);
        Assert.Equal("endpoint-1", restored.RecordingDeviceId);
        Assert.Equal("base.en", restored.LocalWhisperModel);
        Assert.Equal("http://localhost:8000/v1/", restored.CustomTranscriptionEndpoint);
        Assert.DoesNotContain("api-key", ini, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", ini, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacyJsonMigratesWithoutLosingRecordingDeviceOrDeveloperMode()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"gta-settings-{Guid.NewGuid():N}");
        var iniPath = Path.Combine(directory, "settings.ini"); Directory.CreateDirectory(directory);
        try
        {
            using (var stream = File.Create(Path.Combine(directory, "settings.json")))
                ApplicationSettingsJson.Save(stream, new() { RecordingDeviceId = "legacy-device", DeveloperMode = true });
            var loaded = new ApplicationSettingsStore(iniPath).Load();
            Assert.Equal("legacy-device", loaded.RecordingDeviceId);
            Assert.True(loaded.DeveloperMode);
            Assert.True(File.Exists(iniPath));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void LocalModelStoreReportsInstalledAndMissingModels()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"gta-models-{Guid.NewGuid():N}");
        try
        {
            var store = new LocalWhisperModelStore(directory: directory);
            Assert.False(store.IsInstalled("base.en"));
            Directory.CreateDirectory(directory); File.WriteAllBytes(store.PathFor("base.en"), [1]);
            Assert.True(store.IsInstalled("base.en"));
            store.Remove("base.en");
            Assert.False(store.IsInstalled("base.en"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void SavingSettingsPreservesUnknownIniSections()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"gta-ini-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.ini"); Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(path, "[Future]\nPreference=keep-me\n");
            new ApplicationSettingsStore(path).Save(new() { VoiceProvider = TranscriptionProviderKind.LocalWhisper });
            Assert.Contains("Preference=keep-me", File.ReadAllText(path));
        }
        finally { Directory.Delete(directory, true); }
    }
}
