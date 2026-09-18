using GtaHeistPlanner.Core.Settings;
using GtaHeistPlanner.Voice;
using Whisper.net.LibraryLoader;

namespace GtaHeistPlanner.Tests.Voice;

public sealed class LocalWhisperTranscriptionProviderTests
{
    [Fact]
    public async Task SlowInitializationIsAsynchronousAndSharedByConcurrentStarts()
    {
        using var model = new TemporaryModel();
        var loader = new ControlledLoader();
        using var provider = new LocalWhisperTranscriptionProvider(model.Path, LocalWhisperCompute.Cpu, loader);

        var first = provider.StartAsync(Format, []);
        var second = provider.StartAsync(Format, []);

        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        await WaitUntilAsync(() => loader.LoadCount == 1);
        Assert.Equal(1, loader.LoadCount);
        Assert.NotEqual(Environment.CurrentManagedThreadId, loader.LoadThreadId);

        loader.Release();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, loader.LoadCount);
    }

    [Fact]
    public async Task StopAndRestartRetainInitializedRuntime()
    {
        using var model = new TemporaryModel();
        var loader = new ControlledLoader(released: true);
        using var provider = new LocalWhisperTranscriptionProvider(model.Path, LocalWhisperCompute.Cpu, loader);

        await provider.StartAsync(Format, []);
        await provider.StopAsync();
        await provider.StartAsync(Format, []);
        await provider.StopAsync();

        Assert.Equal(1, loader.LoadCount);
        Assert.False(loader.Runtime.Disposed);
    }

    [Fact]
    public async Task StopDuringInitializationDoesNotWaitForNativeLoad()
    {
        using var model = new TemporaryModel();
        var loader = new ControlledLoader();
        using var provider = new LocalWhisperTranscriptionProvider(model.Path, LocalWhisperCompute.Cpu, loader);
        using var cancellation = new CancellationTokenSource();

        var start = provider.StartAsync(Format, [], cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);
        await provider.StopAsync().WaitAsync(TimeSpan.FromSeconds(1));

        loader.Release();
    }

    [Fact]
    public async Task ProviderSwitchDuringInitializationCompletesWithoutWaitingOrDeadlocking()
    {
        using var model = new TemporaryModel();
        var loader = new ControlledLoader();
        using var local = new LocalWhisperTranscriptionProvider(model.Path, LocalWhisperCompute.Cpu, loader);
        using var router = new SpeechTranscriptionProviderRouter(settings =>
            settings.VoiceProvider == TranscriptionProviderKind.LocalWhisper ? local : new ImmediateProvider());
        await router.ConfigureAsync(new() { VoiceProvider = TranscriptionProviderKind.LocalWhisper });
        var start = router.StartAsync(Format, []);
        await WaitUntilAsync(() => loader.LoadCount == 1);

        await router.ConfigureAsync(new() { VoiceProvider = TranscriptionProviderKind.OpenAi })
            .WaitAsync(TimeSpan.FromSeconds(1));

        loader.Release();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => start);
    }

    [Fact]
    public async Task InitializationFailureProducesErrorStateAndDiagnostic()
    {
        using var model = new TemporaryModel();
        var loader = new ControlledLoader(released: true) { Error = new InvalidOperationException("native load failed") };
        using var provider = new LocalWhisperTranscriptionProvider(model.Path, LocalWhisperCompute.Cpu, loader);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.StartAsync(Format, []));

        Assert.Equal("Local Whisper error", provider.StateDescription);
        Assert.Contains(provider.Diagnostics.Entries, entry => entry.Contains("native load failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SegmentedAudioRunsOnWorkerAndRaisesOneFinalTranscript()
    {
        using var model = new TemporaryModel();
        var loader = new ControlledLoader(released: true);
        using var provider = new LocalWhisperTranscriptionProvider(model.Path, LocalWhisperCompute.Cpu, loader);
        var transcript = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.SpeechRecognized += (_, value) => transcript.TrySetResult(value.Text);
        var callerThread = Environment.CurrentManagedThreadId;

        await provider.StartAsync(Format, []);
        provider.PushAudio(new byte[3200], 10);
        for (var index = 0; index < 7; index++) provider.PushAudio(new byte[3200], 0);

        Assert.Equal("scope out", await transcript.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.NotEqual(callerThread, loader.Runtime.ProcessThreadId);
        Assert.Equal(1, loader.Runtime.ProcessCount);
    }

    [Fact]
    public async Task BlankAudioMarkerIsDiagnosedButNotRaisedAsFinalTranscript()
    {
        using var model = new TemporaryModel();
        var loader = new ControlledLoader(released: true);
        loader.Runtime.Text = " [BLANK AUDIO] ";
        using var provider = new LocalWhisperTranscriptionProvider(model.Path, LocalWhisperCompute.Cpu, loader);
        var finals = new List<string>();
        provider.SpeechRecognized += (_, value) => finals.Add(value.Text);

        await provider.StartAsync(Format, []);
        provider.PushAudio(new byte[3200], 10);
        for (var index = 0; index < 7; index++) provider.PushAudio(new byte[3200], 0);
        await WaitUntilAsync(() => loader.Runtime.ProcessCount == 1);
        await Task.Delay(25);

        Assert.Empty(finals);
        Assert.Contains(provider.Diagnostics.Entries, entry => entry.Contains("ignored", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(LocalWhisperCompute.Cpu, true, LocalWhisperCompute.Cpu, WhisperInferenceBackend.Cpu)]
    [InlineData(LocalWhisperCompute.Auto, true, LocalWhisperCompute.Gpu, WhisperInferenceBackend.Cuda)]
    [InlineData(LocalWhisperCompute.Auto, false, LocalWhisperCompute.Gpu, WhisperInferenceBackend.Cuda)]
    [InlineData(LocalWhisperCompute.Gpu, true, LocalWhisperCompute.Gpu, WhisperInferenceBackend.Cuda)]
    public async Task ComputeModeSelectsExpectedBackend(LocalWhisperCompute requested, bool cudaAvailable,
        LocalWhisperCompute loadedMode, WhisperInferenceBackend backend)
    {
        using var model = new TemporaryModel();
        var loader = new ControlledLoader(released: true);
        using var provider = new LocalWhisperTranscriptionProvider(model.Path, requested, loader,
            new FakeCapabilities(cudaAvailable));

        await provider.StartAsync(Format, []);

        Assert.Equal([loadedMode], loader.LoadedModes);
        Assert.Equal(backend, loader.Runtime.Backend);
        Assert.All(loader.ModelPaths, path => Assert.Equal(model.Path, path));
    }

    [Fact]
    public async Task AutoFallsBackToCpuWhenGpuInitializationFails()
    {
        using var model = new TemporaryModel();
        var loader = new ControlledLoader(released: true) { FailGpu = true };
        using var provider = new LocalWhisperTranscriptionProvider(model.Path, LocalWhisperCompute.Auto, loader,
            new FakeCapabilities(true));

        await provider.StartAsync(Format, []);

        Assert.Equal([LocalWhisperCompute.Gpu, LocalWhisperCompute.Cpu], loader.LoadedModes);
        Assert.Contains(provider.Diagnostics.Entries, entry => entry.Contains("Falling back to CPU", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CapabilityProbeCannotOverrideSuccessfulExplicitGpuInitialization()
    {
        using var model = new TemporaryModel();
        var loader = new ControlledLoader(released: true);
        using var provider = new LocalWhisperTranscriptionProvider(model.Path, LocalWhisperCompute.Gpu, loader,
            new FakeCapabilities(false));

        await provider.StartAsync(Format, []);

        Assert.Equal([LocalWhisperCompute.Gpu], loader.LoadedModes);
        Assert.Equal(WhisperInferenceBackend.Cuda, loader.Runtime.Backend);
    }

    [Fact]
    public async Task ExplicitGpuSurfacesOriginalNativeLoadFailure()
    {
        using var model = new TemporaryModel();
        var expected = new System.ComponentModel.Win32Exception(126, "cublas64_12.dll was not found");
        var loader = new ControlledLoader(released: true) { Error = expected };
        using var provider = new LocalWhisperTranscriptionProvider(model.Path, LocalWhisperCompute.Gpu, loader,
            new FakeCapabilities(true));

        var actual = await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(() => provider.StartAsync(Format, []));

        Assert.Same(expected, actual);
        Assert.Contains(provider.Diagnostics.Entries, entry => entry.Contains("Win32 NativeErrorCode=126", StringComparison.Ordinal));
    }

    [Fact]
    public void RuntimeOrdersAreModeSpecific()
    {
        Assert.Equal([RuntimeLibrary.Cuda12, RuntimeLibrary.Cpu], WhisperNetRuntimeLoader.RuntimeOrderFor(LocalWhisperCompute.Auto));
        Assert.Equal([RuntimeLibrary.Cuda12], WhisperNetRuntimeLoader.RuntimeOrderFor(LocalWhisperCompute.Gpu));
        Assert.Equal([RuntimeLibrary.Cpu], WhisperNetRuntimeLoader.RuntimeOrderFor(LocalWhisperCompute.Cpu));
    }

    [Fact]
    public void CpuThreadLimitLeavesHeadroomAndIsCapped()
    {
        Assert.InRange(WhisperNetRuntimeLoader.CpuThreadLimit, 1, 4);
        if (Environment.ProcessorCount > 1)
            Assert.True(WhisperNetRuntimeLoader.CpuThreadLimit < Environment.ProcessorCount);
    }

    private static readonly PcmAudioFormat Format = new(16_000, 16, 1);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }

    private sealed class ControlledLoader(bool released = false) : ILocalWhisperRuntimeLoader
    {
        private readonly ManualResetEventSlim _release = new(released);
        private int _loadCount;
        public FakeRuntime Runtime { get; } = new();
        public Exception? Error { get; init; }
        public bool FailGpu { get; init; }
        public List<LocalWhisperCompute> LoadedModes { get; } = [];
        public List<string> ModelPaths { get; } = [];
        public int LoadCount => Volatile.Read(ref _loadCount);
        public int LoadThreadId { get; private set; }
        public ILocalWhisperRuntime Load(string modelPath, LocalWhisperCompute compute)
        {
            Interlocked.Increment(ref _loadCount);
            LoadedModes.Add(compute);
            ModelPaths.Add(modelPath);
            LoadThreadId = Environment.CurrentManagedThreadId;
            _release.Wait();
            if (compute == LocalWhisperCompute.Gpu && FailGpu) throw new InvalidOperationException("CUDA init failed");
            if (Error is not null) throw Error;
            Runtime.Backend = compute == LocalWhisperCompute.Gpu ? WhisperInferenceBackend.Cuda : WhisperInferenceBackend.Cpu;
            return Runtime;
        }
        public void Release() => _release.Set();
    }

    private sealed class FakeRuntime : ILocalWhisperRuntime
    {
        public WhisperInferenceBackend Backend { get; set; }
        public string Text { get; set; } = "scope out";
        public int ProcessCount { get; private set; }
        public int ProcessThreadId { get; private set; }
        public bool Disposed { get; private set; }
        public async IAsyncEnumerable<string> ProcessAsync(float[] samples,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ProcessCount++;
            ProcessThreadId = Environment.CurrentManagedThreadId;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return Text;
        }
        public void Dispose() => Disposed = true;
    }

    private sealed class FakeCapabilities(bool cudaAvailable) : IWhisperComputeCapabilityService
    {
        public WhisperComputeCapabilities Detect() => new(true, cudaAvailable,
            cudaAvailable ? null : "CUDA test capability unavailable.");
    }

    private sealed class ImmediateProvider : ISpeechTranscriptionProvider
    {
        public event EventHandler<SpeechRecognizedEventArgs>? SpeechRecognized { add { } remove { } }
        public event EventHandler<PartialTranscriptEventArgs>? PartialTranscriptChanged { add { } remove { } }
        public event EventHandler<SpeechRecognitionFailedEventArgs>? RecognitionFailed { add { } remove { } }
        public TranscriptionProviderKind Kind => TranscriptionProviderKind.OpenAi;
        public TranscriptionProviderCapabilities Capabilities { get; } = new(false, false, false);
        public string StateDescription => "ready";
        public VoiceDiagnosticTrace Diagnostics { get; } = new();
        public Task StartAsync(PcmAudioFormat format, IReadOnlyCollection<string> keywords, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void PushAudio(ReadOnlyMemory<byte> pcmAudio, int activityLevel) { }
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> TestAsync(CancellationToken cancellationToken = default) => Task.FromResult("ready");
        public void Dispose() { }
    }

    private sealed class TemporaryModel : IDisposable
    {
        public TemporaryModel()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"whisper-test-{Guid.NewGuid():N}.bin");
            File.WriteAllBytes(Path, [1]);
        }
        public string Path { get; }
        public void Dispose() => File.Delete(Path);
    }
}
