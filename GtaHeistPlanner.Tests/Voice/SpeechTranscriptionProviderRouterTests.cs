using GtaHeistPlanner.Core.Settings;
using GtaHeistPlanner.Voice;

namespace GtaHeistPlanner.Tests.Voice;

public sealed class SpeechTranscriptionProviderRouterTests
{
    [Fact]
    public async Task FactoryUsesSavedSelectionAndSwitchStopsDisposesPreviousProvider()
    {
        var created = new List<FakeProvider>();
        using var router = new SpeechTranscriptionProviderRouter(settings =>
        {
            var provider = new FakeProvider(settings.VoiceProvider); created.Add(provider); return provider;
        });
        await router.ConfigureAsync(new() { VoiceProvider = TranscriptionProviderKind.OpenAi, RecordingDeviceId = "same-device" });
        await router.StartAsync(new(48_000, 16, 1), []);
        await router.ConfigureAsync(new() { VoiceProvider = TranscriptionProviderKind.LocalWhisper, RecordingDeviceId = "same-device" });
        await router.StartAsync(new(48_000, 16, 1), []);

        Assert.Equal([TranscriptionProviderKind.OpenAi, TranscriptionProviderKind.LocalWhisper], created.Select(item => item.Kind));
        Assert.True(created[0].Stopped);
        Assert.True(created[0].Disposed);
    }

    [Theory]
    [InlineData(TranscriptionProviderKind.LocalWhisper)]
    [InlineData(TranscriptionProviderKind.OpenAi)]
    public async Task FinalTranscriptUsesOneSharedDownstreamEvent(TranscriptionProviderKind kind)
    {
        FakeProvider? provider = null;
        using var router = new SpeechTranscriptionProviderRouter(settings => provider = new FakeProvider(settings.VoiceProvider));
        var transcripts = new List<string>(); router.SpeechRecognized += (_, e) => transcripts.Add(e.Text);
        await router.ConfigureAsync(new() { VoiceProvider = kind });
        await router.StartAsync(new(48_000, 16, 1), []);
        provider!.EmitFinal("three Charlie");
        Assert.Equal(["three Charlie"], transcripts);
    }

    private sealed class FakeProvider(TranscriptionProviderKind kind) : ISpeechTranscriptionProvider
    {
        public event EventHandler<SpeechRecognizedEventArgs>? SpeechRecognized;
        public event EventHandler<PartialTranscriptEventArgs>? PartialTranscriptChanged { add { } remove { } }
        public event EventHandler<SpeechRecognitionFailedEventArgs>? RecognitionFailed { add { } remove { } }
        public TranscriptionProviderKind Kind { get; } = kind;
        public TranscriptionProviderCapabilities Capabilities { get; } = new(false, false, false);
        public string StateDescription => "fake";
        public VoiceDiagnosticTrace Diagnostics { get; } = new();
        public bool Stopped { get; private set; }
        public bool Disposed { get; private set; }
        public Task StartAsync(PcmAudioFormat format, IReadOnlyCollection<string> keywords, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void PushAudio(ReadOnlyMemory<byte> pcmAudio, int activityLevel) { }
        public Task StopAsync(CancellationToken cancellationToken = default) { Stopped = true; return Task.CompletedTask; }
        public Task<string> TestAsync(CancellationToken cancellationToken = default) => Task.FromResult("ready");
        public void EmitFinal(string text) => SpeechRecognized?.Invoke(this, new(text, 1));
        public void Dispose() => Disposed = true;
    }
}
