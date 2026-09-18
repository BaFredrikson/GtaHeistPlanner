using System.Runtime.InteropServices;

namespace GtaHeistPlanner.Voice;

public enum WhisperInferenceBackend { Cpu, Cuda }

public sealed record WhisperComputeCapabilities(bool CpuAvailable, bool CudaAvailable, string? FailureReason = null)
{
    public bool NvidiaDriverDetected { get; init; }
    public bool Cuda12RuntimeInstalled { get; init; }
    public bool Cuda12NativeLoadSucceeded { get; init; }
}

public interface IWhisperComputeCapabilityService
{
    WhisperComputeCapabilities Detect();
}

public sealed class WhisperComputeCapabilityService : IWhisperComputeCapabilityService
{
    private readonly Lazy<WhisperComputeCapabilities> _cached;

    public WhisperComputeCapabilityService() : this(ProbeDriver, ResolvePackagedRuntime, ProbeNativeRuntime) { }

    public WhisperComputeCapabilityService(Func<bool> driverProbe, Func<string?> runtimePathResolver,
        Func<string, (bool Success, string? Failure)> nativeRuntimeProbe)
    {
        _cached = new(() => Probe(driverProbe, runtimePathResolver, nativeRuntimeProbe));
    }

    public WhisperComputeCapabilities Detect() => _cached.Value;

    private static WhisperComputeCapabilities Probe(Func<bool> driverProbe, Func<string?> runtimePathResolver,
        Func<string, (bool Success, string? Failure)> nativeRuntimeProbe)
    {
        if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            return new(true, false, "CUDA is supported by this application only on Windows x64.");
        var driver = driverProbe();
        if (!driver)
            return new(true, false, "No supported NVIDIA driver was detected.");
        var runtimePath = runtimePathResolver();
        if (runtimePath is null)
            return new(true, false, "The Whisper CUDA12 runtime package is not present in the application output.")
            {
                NvidiaDriverDetected = true,
            };
        var native = nativeRuntimeProbe(runtimePath);
        return new(true, native.Success, native.Failure)
        {
            NvidiaDriverDetected = true,
            Cuda12RuntimeInstalled = true,
            Cuda12NativeLoadSucceeded = native.Success,
        };
    }

    private static bool ProbeDriver()
    {
        if (!NativeLibrary.TryLoad("nvcuda.dll", out var handle)) return false;
        NativeLibrary.Free(handle);
        return true;
    }

    private static string? ResolvePackagedRuntime()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "runtimes", "cuda12", "win-x64", "ggml-cuda-whisper.dll");
        return File.Exists(path) ? path : null;
    }

    private static (bool Success, string? Failure) ProbeNativeRuntime(string path)
    {
        if (NativeLibrary.TryLoad(path, out var handle))
        {
            NativeLibrary.Free(handle);
            return (true, null);
        }
        var missing = KnownCudaDependencies.Where(name => !CanLoad(name)).ToArray();
        var detail = missing.Length == 0
            ? "The packaged Whisper CUDA12 native library could not be loaded. A CUDA dependency or compatible runtime may be missing."
            : $"Missing CUDA native dependencies: {string.Join(", ", missing)}.";
        return (false, $"NVIDIA GPU detected, but CUDA acceleration is not ready. {detail} Whisper CUDA12 requires CUDA Toolkit 12.4.1 or newer.");
    }

    private static bool CanLoad(string library)
    {
        if (!NativeLibrary.TryLoad(library, out var handle)) return false;
        NativeLibrary.Free(handle);
        return true;
    }

    private static readonly string[] KnownCudaDependencies =
        ["cudart64_12.dll", "cublas64_12.dll", "cublasLt64_12.dll"];
}
