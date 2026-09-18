namespace GtaHeistPlanner.Core.Settings;

public static class ApplicationSettingsIni
{
    public static ApplicationSettings Load(TextReader reader)
    {
        var ini = IniDocument.Load(reader);
        return new ApplicationSettings
        {
            RecordingDeviceId = ini.Get("Voice", "RecordingDeviceId"),
            RecordingDeviceName = ini.Get("Voice", "RecordingDeviceName"),
            MicrophoneEnabled = Bool(ini.Get("Voice", "MicrophoneEnabled"), true),
            DeveloperMode = Bool(ini.Get("Application", "DeveloperMode"), false),
            VoiceProvider = EnumValue(ini.Get("Voice", "Provider"), TranscriptionProviderKind.LocalWhisper),
            LocalWhisperModel = ini.Get("LocalWhisper", "Model") ?? "small.en",
            LocalWhisperCompute = EnumValue(ini.Get("LocalWhisper", "Compute"), LocalWhisperCompute.Auto),
            OpenAiTranscriptionModel = ini.Get("OpenAI", "Model") ?? "gpt-live-transcribe",
            CustomTranscriptionEndpoint = ini.Get("CustomTranscription", "Endpoint") ?? string.Empty,
            CustomTranscriptionModel = ini.Get("CustomTranscription", "Model") ?? string.Empty,
            HasOpenAiApiKey = Bool(ini.Get("OpenAI", "HasApiKey"), false),
            HasCustomApiKey = Bool(ini.Get("CustomTranscription", "HasApiKey"), false),
        };
    }

    public static void Save(TextWriter writer, ApplicationSettings settings)
    {
        var ini = new IniDocument();
        Apply(ini, settings);
        ini.Save(writer);
    }

    public static void Apply(IniDocument ini, ApplicationSettings settings)
    {
        ini.Set("Application", "DeveloperMode", settings.DeveloperMode.ToString());
        ini.Set("Voice", "Provider", settings.VoiceProvider.ToString());
        ini.Set("Voice", "RecordingDeviceId", settings.RecordingDeviceId);
        ini.Set("Voice", "RecordingDeviceName", settings.RecordingDeviceName);
        ini.Set("Voice", "MicrophoneEnabled", settings.MicrophoneEnabled.ToString());
        ini.Set("LocalWhisper", "Model", settings.LocalWhisperModel);
        ini.Set("LocalWhisper", "Compute", settings.LocalWhisperCompute.ToString());
        ini.Set("OpenAI", "Model", settings.OpenAiTranscriptionModel);
        ini.Set("OpenAI", "HasApiKey", settings.HasOpenAiApiKey.ToString());
        ini.Set("CustomTranscription", "Endpoint", settings.CustomTranscriptionEndpoint);
        ini.Set("CustomTranscription", "Model", settings.CustomTranscriptionModel);
        ini.Set("CustomTranscription", "HasApiKey", settings.HasCustomApiKey.ToString());
    }

    private static bool Bool(string? value, bool fallback) => bool.TryParse(value, out var parsed) ? parsed : fallback;
    private static T EnumValue<T>(string? value, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var parsed) ? parsed : fallback;
}
