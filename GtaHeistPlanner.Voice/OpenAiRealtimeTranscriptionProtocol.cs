using System.Text.Json;

namespace GtaHeistPlanner.Voice;

public static class OpenAiRealtimeTranscriptionProtocol
{
    public const string Model = "gpt-live-transcribe";
    public const string BootstrapMethod = "WebSocket HTTP GET upgrade";
    public static Uri WebSocketEndpoint { get; } = new("wss://api.openai.com/v1/realtime?intent=transcription");
    public const string SessionUpdateEventType = "session.update";
    public const string AudioAppendEventType = "input_audio_buffer.append";
    public const string AudioCommitEventType = "input_audio_buffer.commit";

    public static byte[] CreateSessionUpdate(IReadOnlyCollection<string> sourceKeywords)
    {
        var keywords = sourceKeywords.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Replace('\r', ' ').Replace('\n', ' ').Replace("<", "").Replace(">", ""))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = SessionUpdateEventType,
            session = new
            {
                type = "transcription",
                audio = new
                {
                    input = new
                    {
                        format = new
                        {
                            type = "audio/pcm",
                            rate = 24000
                        },
                        transcription = new
                        {
                            model = Model,
                            prompt = "GTA heist planning. Transcribe commands and monetary values exactly.",
                            keywords,
                            languages = new[] { "en" },
                            delay = "low"
                        }
                    }
                }
            }
        });
    }

    public static byte[] CreateAudioAppend(ReadOnlySpan<byte> pcm24KhzMono) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = AudioAppendEventType,
            audio = Convert.ToBase64String(pcm24KhzMono)
        });

    public static byte[] CreateAudioCommit() =>
        JsonSerializer.SerializeToUtf8Bytes(new { type = AudioCommitEventType });
}
