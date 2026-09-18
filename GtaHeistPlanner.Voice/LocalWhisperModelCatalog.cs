namespace GtaHeistPlanner.Voice;

public sealed record LocalWhisperModelInfo(string Id, string FileName, Uri DownloadUri, long ExpectedBytes, bool Recommended,
    string? ExpectedSha256 = null);

public static class LocalWhisperModelCatalog
{
    public static IReadOnlyList<LocalWhisperModelInfo> Models { get; } =
    [
        new("base.en", "ggml-base.en.bin", new("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin"),
            147_964_211, false, "a03779c86df3323075f5e796cb2ce5029f00ec8869eee3fdfb897afe36c6d002"),
        new("small.en", "ggml-small.en.bin", new("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en.bin"),
            487_614_201, true, "c6138d6d58ecc8322097e0f987c32f1be8bb0a18532a3f88f734d1bbf9c41e5d"),
    ];

    public static LocalWhisperModelInfo Get(string id) => Models.FirstOrDefault(model => model.Id == id)
        ?? throw new ArgumentOutOfRangeException(nameof(id), $"Unknown local Whisper model '{id}'.");
}
