using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using GtaHeistPlanner.Core.Settings;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace GtaHeistPlanner.Voice;

public interface ILocalWhisperRuntimeLoader
{
    ILocalWhisperRuntime Load(string modelPath, LocalWhisperCompute compute);
}

public interface ILocalWhisperRuntime : IDisposable
{
    WhisperInferenceBackend Backend { get; }
    IAsyncEnumerable<string> ProcessAsync(float[] samples, CancellationToken cancellationToken);
}

public sealed class WhisperNetRuntimeLoader : ILocalWhisperRuntimeLoader
{
    public static int CpuThreadLimit => Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
    public static IReadOnlyList<RuntimeLibrary> RuntimeOrderFor(LocalWhisperCompute compute) => compute switch
    {
        LocalWhisperCompute.Gpu => [RuntimeLibrary.Cuda12],
        LocalWhisperCompute.Cpu => [RuntimeLibrary.Cpu],
        _ => [RuntimeLibrary.Cuda12, RuntimeLibrary.Cpu],
    };

    public ILocalWhisperRuntime Load(string modelPath, LocalWhisperCompute compute)
    {
        RuntimeOptions.RuntimeLibraryOrder = RuntimeOrderFor(compute).ToList();
        var useGpu = compute == LocalWhisperCompute.Gpu;
        var factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions { UseGpu = useGpu });
        try
        {
            var cudaLoaded = RuntimeOptions.LoadedLibrary is RuntimeLibrary.Cuda or RuntimeLibrary.Cuda12;
            if (useGpu && !cudaLoaded)
                throw new InvalidOperationException($"Whisper.net initialized an unexpected runtime '{RuntimeOptions.LoadedLibrary}' while CUDA12-only mode was requested.");
            var builder = factory.CreateBuilder().WithLanguage("en");
            if (!useGpu)
                builder.WithThreads(CpuThreadLimit);
            var processor = builder.Build();
            return new Runtime(factory, processor, useGpu ? WhisperInferenceBackend.Cuda : WhisperInferenceBackend.Cpu);
        }
        catch { factory.Dispose(); throw; }
    }

    private sealed class Runtime(WhisperFactory factory, WhisperProcessor processor, WhisperInferenceBackend backend) : ILocalWhisperRuntime
    {
        public WhisperInferenceBackend Backend => backend;
        public async IAsyncEnumerable<string> ProcessAsync(float[] samples,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var segment in processor.ProcessAsync(samples, cancellationToken).ConfigureAwait(false))
                yield return segment.Text;
        }

        public void Dispose() { processor.Dispose(); factory.Dispose(); }
    }
}

public sealed class LocalWhisperTranscriptionProvider : ISpeechTranscriptionProvider
{
    private readonly string _modelPath;
    private readonly LocalWhisperCompute _compute;
    private readonly ILocalWhisperRuntimeLoader _loader;
    private readonly IWhisperComputeCapabilityService _capabilities;
    private readonly VoiceDiagnosticTrace _diagnostics;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Task<ILocalWhisperRuntime>? _initializationTask;
    private ILocalWhisperRuntime? _runtime;
    private Channel<AudioFrame>? _frames;
    private CancellationTokenSource? _workerCancellation;
    private Task? _worker;
    private bool _disposed;

    public LocalWhisperTranscriptionProvider(string modelPath, LocalWhisperCompute compute,
        VoiceDiagnosticTrace? diagnostics = null)
        : this(modelPath, compute, new WhisperNetRuntimeLoader(), new WhisperComputeCapabilityService(), diagnostics) { }

    public LocalWhisperTranscriptionProvider(string modelPath, LocalWhisperCompute compute,
        ILocalWhisperRuntimeLoader loader, VoiceDiagnosticTrace? diagnostics = null)
        : this(modelPath, compute, loader, new WhisperComputeCapabilityService(), diagnostics) { }

    public LocalWhisperTranscriptionProvider(string modelPath, LocalWhisperCompute compute,
        ILocalWhisperRuntimeLoader loader, IWhisperComputeCapabilityService capabilities,
        VoiceDiagnosticTrace? diagnostics = null)
    {
        _modelPath = modelPath;
        _compute = compute;
        _loader = loader;
        _capabilities = capabilities;
        _diagnostics = diagnostics ?? new();
    }

    public event EventHandler<SpeechRecognizedEventArgs>? SpeechRecognized;
    public event EventHandler<PartialTranscriptEventArgs>? PartialTranscriptChanged { add { } remove { } }
    public event EventHandler<SpeechRecognitionFailedEventArgs>? RecognitionFailed;
    public TranscriptionProviderKind Kind => TranscriptionProviderKind.LocalWhisper;
    public TranscriptionProviderCapabilities Capabilities { get; } = new(false, false, false);
    public string StateDescription { get; private set; } = "Stopped";
    public VoiceDiagnosticTrace Diagnostics => _diagnostics;

    public async Task StartAsync(PcmAudioFormat format, IReadOnlyCollection<string> keywords,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Validate(format);
        _diagnostics.RecordMilestone("Local Whisper Start requested");
        _diagnostics.Record($"Requested compute mode: {_compute}");
        _diagnostics.Record($"Local Whisper model file verified: '{Path.GetFileName(_modelPath)}' ({new FileInfo(_modelPath).Length:N0} bytes).");

        Task<ILocalWhisperRuntime> initialization;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_worker is not null) return;
            StateDescription = $"Initializing Local Whisper - {Path.GetFileNameWithoutExtension(_modelPath)}...";
            initialization = _initializationTask ??= InitializeRuntimeAsync();
        }
        finally { _gate.Release(); }

        ILocalWhisperRuntime runtime;
        try { runtime = await initialization.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StateDescription = "Stopped";
            _diagnostics.RecordMilestone("Local Whisper start cancelled; non-cancellable native load continues in background");
            throw;
        }
        catch (Exception exception)
        {
            StateDescription = "Local Whisper error";
            _diagnostics.RecordException("Local Whisper initialization", exception);
            RecognitionFailed?.Invoke(this, new(exception));
            throw;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_worker is not null) return;
            _runtime = runtime;
            var resampler = new Pcm16MonoResampler(format.SampleRate, 16_000);
            _frames = Channel.CreateBounded<AudioFrame>(new BoundedChannelOptions(16)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });
            _workerCancellation = new();
            var frames = _frames;
            var token = _workerCancellation.Token;
            _worker = Task.Run(() => PumpAsync(frames, resampler, runtime, token), CancellationToken.None);
            StateDescription = $"Listening (Local Whisper {Path.GetFileNameWithoutExtension(_modelPath)} - {runtime.Backend})";
            _diagnostics.RecordMilestone("Local Whisper recognition worker started");
            _diagnostics.RecordMilestone("Local Whisper Listening");
        }
        finally { _gate.Release(); }
    }

    public void PushAudio(ReadOnlyMemory<byte> pcmAudio, int activityLevel) =>
        _frames?.Writer.TryWrite(new(pcmAudio.ToArray(), activityLevel));

    private async Task<ILocalWhisperRuntime> InitializeRuntimeAsync()
    {
        var timer = Stopwatch.StartNew();
        _diagnostics.RecordMilestone("Local Whisper model load started (background)");
        _diagnostics.RecordMilestone("Local Whisper runtime/context creation started (background)");
        try
        {
            var runtime = await Task.Run(ResolveAndLoadRuntime, CancellationToken.None).ConfigureAwait(false);
            timer.Stop();
            _runtime = runtime;
            if (_disposed && ReferenceEquals(Interlocked.CompareExchange(ref _runtime, null, runtime), runtime))
            {
                runtime.Dispose();
                throw new ObjectDisposedException(nameof(LocalWhisperTranscriptionProvider));
            }
            _diagnostics.RecordMilestone($"Local Whisper model load completed ({timer.ElapsedMilliseconds} ms)");
            _diagnostics.RecordMilestone($"Local Whisper runtime/context creation completed ({timer.ElapsedMilliseconds} ms total)");
            _diagnostics.Record($"Selected backend: {runtime.Backend}");
            _diagnostics.Record($"Inference backend: {runtime.Backend}");
            return runtime;
        }
        catch (Exception exception)
        {
            timer.Stop();
            _diagnostics.RecordException($"Local Whisper native initialization after {timer.ElapsedMilliseconds} ms", exception);
            throw;
        }
    }

    private ILocalWhisperRuntime ResolveAndLoadRuntime()
    {
        if (_compute == LocalWhisperCompute.Cpu)
        {
            _diagnostics.Record("Runtime order: Cpu (explicit CPU)");
            return _loader.Load(_modelPath, LocalWhisperCompute.Cpu);
        }

        var capabilities = _capabilities.Detect();
        _diagnostics.Record($"NVIDIA driver detected: {capabilities.NvidiaDriverDetected}");
        _diagnostics.Record($"Whisper CUDA12 runtime packaged: {capabilities.Cuda12RuntimeInstalled}");
        _diagnostics.Record($"Whisper CUDA12 native load succeeded: {capabilities.Cuda12NativeLoadSucceeded}");
        _diagnostics.Record(capabilities.CudaAvailable
            ? "GPU backend probe: Whisper CUDA12 runtime available"
            : $"GPU backend probe: CUDA unavailable - {capabilities.FailureReason}");
        LogNativeEnvironment();
        if (_compute == LocalWhisperCompute.Gpu)
        {
            _diagnostics.Record("Runtime order: Cuda12 (explicit GPU; CPU fallback disabled)");
            return _loader.Load(_modelPath, LocalWhisperCompute.Gpu);
        }
        _diagnostics.Record("Runtime order: Cuda12 -> Cpu (Auto uses separate authoritative attempts)");
        try
        {
            return _loader.Load(_modelPath, LocalWhisperCompute.Gpu);
        }
        catch (Exception exception)
        {
            _diagnostics.RecordException("Authoritative Whisper.net CUDA12 initialization", exception);
            _diagnostics.Record("Falling back to CPU after real CUDA12 initialization failure");
        }
        return _loader.Load(_modelPath, LocalWhisperCompute.Cpu);
    }

    private void LogNativeEnvironment()
    {
        _diagnostics.Record($"Process architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}; OS architecture: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
        _diagnostics.Record($"CUDA_PATH: {Environment.GetEnvironmentVariable("CUDA_PATH") ?? "<unset>"}");
        var cudaPaths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries).Where(path => path.Contains("CUDA", StringComparison.OrdinalIgnoreCase));
        _diagnostics.Record($"CUDA PATH entries: {string.Join("; ", cudaPaths)}");
        foreach (var name in new[] { "cudart64_12.dll", "cublas64_12.dll", "cublasLt64_12.dll" })
            _diagnostics.Record($"{name}: {ResolveFromPath(name) ?? "<not resolved>"}");
        _diagnostics.Record($"Packaged CUDA12 runtime: {Path.Combine(AppContext.BaseDirectory, "runtimes", "cuda12", "win-x64", "whisper.dll")}");
    }

    private static string? ResolveFromPath(string fileName) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries).Select(path => Path.Combine(path.Trim(), fileName)).FirstOrDefault(File.Exists);

    private async Task PumpAsync(Channel<AudioFrame> frames, Pcm16MonoResampler resampler,
        ILocalWhisperRuntime runtime, CancellationToken cancellationToken)
    {
        var utterance = new List<byte>();
        var active = false;
        var silence = 0d;
        try
        {
            await foreach (var frame in frames.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var pcm = resampler.Convert(frame.Data);
                var duration = pcm.Length / 32d;
                if (frame.Activity >= 3) { active = true; silence = 0; }
                else if (active) silence += duration;
                if (!active) continue;
                utterance.AddRange(pcm);
                if (silence < 700) continue;
                await TranscribeAsync(runtime, utterance.ToArray(), cancellationToken).ConfigureAwait(false);
                utterance.Clear(); active = false; silence = 0;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            StateDescription = "Local Whisper error";
            _diagnostics.RecordException("Local Whisper recognition worker", exception);
            RecognitionFailed?.Invoke(this, new(exception));
        }
    }

    private async Task TranscribeAsync(ILocalWhisperRuntime runtime, byte[] pcm16, CancellationToken cancellationToken)
    {
        StateDescription = "Transcribing locally";
        var timer = Stopwatch.StartNew();
        _diagnostics.RecordMilestone($"Local Whisper inference started ({pcm16.Length / 32d:N0} ms audio)");
        var samples = new float[pcm16.Length / 2];
        for (var i = 0; i < samples.Length; i++) samples[i] = BitConverter.ToInt16(pcm16, i * 2) / 32768f;
        var text = new StringBuilder();
        await foreach (var segment in runtime.ProcessAsync(samples, cancellationToken).ConfigureAwait(false)) text.Append(segment);
        timer.Stop();
        StateDescription = $"Listening (Local Whisper {Path.GetFileNameWithoutExtension(_modelPath)} - {runtime.Backend})";
        _diagnostics.RecordMilestone($"Local Whisper inference completed ({timer.ElapsedMilliseconds} ms)");
        var rawFinal = text.ToString();
        if (TranscriptSanitizer.TrySanitize(rawFinal, out var final))
            SpeechRecognized?.Invoke(this, new(final, float.NaN));
        else
            _diagnostics.Record($"Local Whisper final: {rawFinal.Trim()} -> ignored as empty/non-speech marker");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Channel<AudioFrame>? frames;
        CancellationTokenSource? workerCancellation;
        Task? worker;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            frames = _frames; workerCancellation = _workerCancellation; worker = _worker;
            _frames = null; _workerCancellation = null; _worker = null;
            StateDescription = "Stopped";
        }
        finally { _gate.Release(); }

        frames?.Writer.TryComplete();
        workerCancellation?.Cancel();
        if (worker is not null)
        {
            try { await worker.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (workerCancellation?.IsCancellationRequested == true || cancellationToken.IsCancellationRequested) { }
        }
        workerCancellation?.Dispose();
        _diagnostics.RecordMilestone("Local Whisper recognition stopped; initialized model retained");
    }

    public async Task<string> TestAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateModelAndCompute();
        Task<ILocalWhisperRuntime> initialization;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { initialization = _initializationTask ??= InitializeRuntimeAsync(); }
        finally { _gate.Release(); }
        var runtime = await initialization.WaitAsync(cancellationToken).ConfigureAwait(false);
        return $"Local Whisper ready - {Path.GetFileNameWithoutExtension(_modelPath)} - {runtime.Backend}";
    }

    private void Validate(PcmAudioFormat format)
    {
        ValidateModelAndCompute();
        if (format.BitsPerSample != 16 || format.Channels != 1)
            throw new NotSupportedException("Local Whisper requires mono PCM16 capture input.");
    }

    private void ValidateModelAndCompute()
    {
        if (!File.Exists(_modelPath)) throw new FileNotFoundException("Local Whisper model is not installed. Open Settings to download it.", _modelPath);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _frames?.Writer.TryComplete();
        _workerCancellation?.Cancel();
        _workerCancellation?.Dispose();
        var runtime = Interlocked.Exchange(ref _runtime, null);
        runtime?.Dispose();
        _gate.Dispose();
    }

    private sealed record AudioFrame(byte[] Data, int Activity);
}
