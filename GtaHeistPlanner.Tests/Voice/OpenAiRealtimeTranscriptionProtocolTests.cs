using System.Text.Json;
using GtaHeistPlanner.Voice;

namespace GtaHeistPlanner.Tests.Voice;

public sealed class OpenAiRealtimeTranscriptionProtocolTests
{
    [Fact]
    public void Endpoints_UseDedicatedTranscriptionSessionFlow()
    {
        Assert.Equal("wss://api.openai.com/v1/realtime?intent=transcription", OpenAiRealtimeTranscriptionProtocol.WebSocketEndpoint.AbsoluteUri);
        Assert.Equal("WebSocket HTTP GET upgrade", OpenAiRealtimeTranscriptionProtocol.BootstrapMethod);
        Assert.DoesNotContain("model=", OpenAiRealtimeTranscriptionProtocol.WebSocketEndpoint.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SessionUpdate_ConfiguresTranscriptionModelWithoutGeneralRealtimeSession()
    {
        using var document = JsonDocument.Parse(OpenAiRealtimeTranscriptionProtocol.CreateSessionUpdate(["scope out"]));
        var root = document.RootElement;

        Assert.Equal("session.update", root.GetProperty("type").GetString());
        Assert.DoesNotContain("transcription_session.update", root.GetRawText(), StringComparison.Ordinal);
        Assert.False(root.TryGetProperty("model", out _));
        var session = root.GetProperty("session");
        Assert.Equal("transcription", session.GetProperty("type").GetString());
        Assert.False(session.TryGetProperty("model", out _));
        var input = session.GetProperty("audio").GetProperty("input");
        Assert.Equal("audio/pcm", input.GetProperty("format").GetProperty("type").GetString());
        Assert.Equal(24000, input.GetProperty("format").GetProperty("rate").GetInt32());
        Assert.Equal("gpt-live-transcribe", input.GetProperty("transcription").GetProperty("model").GetString());
        Assert.False(input.TryGetProperty("turn_detection", out _));
    }

    [Fact]
    public void AudioMessages_UseAppendAndExplicitCommitEvents()
    {
        using var append = JsonDocument.Parse(OpenAiRealtimeTranscriptionProtocol.CreateAudioAppend([1, 2, 3, 4]));
        using var commit = JsonDocument.Parse(OpenAiRealtimeTranscriptionProtocol.CreateAudioCommit());

        Assert.Equal("input_audio_buffer.append", append.RootElement.GetProperty("type").GetString());
        Assert.Equal([1, 2, 3, 4], Convert.FromBase64String(append.RootElement.GetProperty("audio").GetString()!));
        Assert.Equal("input_audio_buffer.commit", commit.RootElement.GetProperty("type").GetString());
        Assert.Single(commit.RootElement.EnumerateObject());
    }
}
