using GtaHeistPlanner.Voice;

namespace GtaHeistPlanner.Tests.Voice;

public sealed class OpenAiRealtimeSpeechRecognitionServiceTests
{
    [Fact]
    public async Task FinalTranscript_IsDispatchedExactlyOnce()
    {
        var transport = new FakeTransport();
        using var service = Create(transport);
        var recognized = new List<string>();
        service.SpeechRecognized += (_, eventArgs) => recognized.Add(eventArgs.Text);

        await service.StartAsync(new(48_000, 16, 1), []);
        transport.EmitFinal("scope out");

        Assert.Equal(["scope out"], recognized);
    }

    [Fact]
    public async Task PartialTranscript_DoesNotDispatchCommand()
    {
        var transport = new FakeTransport();
        using var service = Create(transport);
        var recognized = 0;
        var partial = "";
        service.SpeechRecognized += (_, _) => recognized++;
        service.PartialTranscriptChanged += (_, eventArgs) => partial = eventArgs.Text;

        await service.StartAsync(new(48_000, 16, 1), []);
        transport.EmitPartial("west painting");

        Assert.Equal(0, recognized);
        Assert.Equal("west painting", partial);
    }

    [Fact]
    public async Task LocalSilenceAfterSpeech_CommitsAudioBuffer()
    {
        var transport = new FakeTransport();
        using var service = Create(transport);
        await service.StartAsync(new(48_000, 16, 1), []);
        var fortyMillisecondsAt48Khz = new byte[3_840];

        service.PushAudio(fortyMillisecondsAt48Khz, 3);
        await transport.FirstAudioSent.Task.WaitAsync(TimeSpan.FromSeconds(1));
        for (var index = 0; index < 18; index++)
        {
            service.PushAudio(fortyMillisecondsAt48Khz, 0);
            await Task.Delay(1);
        }

        await transport.AudioCommitted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(1, transport.CommitCount);
    }

    [Fact]
    public async Task MissingApiKey_FailsClearlyWithoutConnecting()
    {
        var transport = new FakeTransport();
        using var service = new OpenAiRealtimeSpeechRecognitionService(() => transport, () => null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(new(48_000, 16, 1), []));

        Assert.Equal("OpenAI API key is not configured.", exception.Message);
        Assert.False(transport.Connected);
    }

    [Fact]
    public async Task Stop_PreventsStaleFinalTranscript()
    {
        var transport = new FakeTransport();
        using var service = Create(transport);
        var recognized = 0;
        service.SpeechRecognized += (_, _) => recognized++;
        await service.StartAsync(new(48_000, 16, 1), []);

        await service.StopAsync();
        transport.EmitFinal("scope out");

        Assert.Equal(0, recognized);
        Assert.True(transport.Closed);
    }

    [Fact]
    public async Task TransportError_IsPropagated()
    {
        var transport = new FakeTransport();
        using var service = Create(transport);
        Exception? received = null;
        service.RecognitionFailed += (_, eventArgs) => received = eventArgs.Exception;
        await service.StartAsync(new(48_000, 16, 1), []);

        transport.EmitFailure(new IOException("network unavailable"));

        Assert.IsType<IOException>(received);
        Assert.Equal("network unavailable", received!.Message);
    }

    [Fact]
    public async Task StopAsync_DoesNotSynchronouslyBlockWhileTransportIsClosing()
    {
        var transport = new FakeTransport { DelayClose = true };
        using var service = Create(transport);
        await service.StartAsync(new(48_000, 16, 1), []);

        var stop = service.StopAsync();

        Assert.False(stop.IsCompleted);
        transport.CompleteClose();
        await stop.WaitAsync(TimeSpan.FromSeconds(1));
    }

    private static OpenAiRealtimeSpeechRecognitionService Create(FakeTransport transport) =>
        new(() => transport, () => "test-key-not-sent");

    private sealed class FakeTransport : IRealtimeTranscriptionTransport
    {
        public event EventHandler<string>? PartialTranscript;
        public event EventHandler<string>? FinalTranscript;
        public event EventHandler<Exception>? Failed;
        public event EventHandler<string>? StateChanged;
        public bool Connected { get; private set; }
        public bool Closed { get; private set; }
        public bool DelayClose { get; init; }
        public int CommitCount { get; private set; }
        public TaskCompletionSource FirstAudioSent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AudioCommitted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _closeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ConnectAsync(string apiKey, RealtimeTranscriptionOptions options, CancellationToken cancellationToken)
        {
            Connected = true;
            StateChanged?.Invoke(this, "ready");
            return Task.CompletedTask;
        }

        public ValueTask SendAudioAsync(ReadOnlyMemory<byte> pcm24KhzMono, CancellationToken cancellationToken)
        {
            FirstAudioSent.TrySetResult();
            return ValueTask.CompletedTask;
        }
        public ValueTask CommitAudioAsync(CancellationToken cancellationToken)
        {
            CommitCount++;
            AudioCommitted.TrySetResult();
            return ValueTask.CompletedTask;
        }
        public Task CloseAsync(CancellationToken cancellationToken)
        {
            Closed = true;
            return DelayClose ? _closeCompletion.Task : Task.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void EmitPartial(string text) => PartialTranscript?.Invoke(this, text);
        public void EmitFinal(string text) => FinalTranscript?.Invoke(this, text);
        public void EmitFailure(Exception exception) => Failed?.Invoke(this, exception);
        public void CompleteClose() => _closeCompletion.TrySetResult();
    }
}
