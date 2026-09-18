using GtaHeistPlanner.Voice;

namespace GtaHeistPlanner.Tests.Voice;

public sealed class WhisperComputeCapabilityServiceTests
{
    [Fact]
    public void NvidiaDriverAndSuccessfulNativeLoadMakeCudaAvailable()
    {
        var capability = Service(driver: true, package: true, native: true).Detect();

        Assert.True(capability.NvidiaDriverDetected);
        Assert.True(capability.Cuda12RuntimeInstalled);
        Assert.True(capability.Cuda12NativeLoadSucceeded);
        Assert.True(capability.CudaAvailable);
    }

    [Fact]
    public void DriverAloneDoesNotMakeCudaAvailableWhenNativeLoadFails()
    {
        var capability = Service(driver: true, package: true, native: false,
            "Missing CUDA native dependency: cublas64_12.dll.").Detect();

        Assert.True(capability.NvidiaDriverDetected);
        Assert.False(capability.Cuda12NativeLoadSucceeded);
        Assert.False(capability.CudaAvailable);
        Assert.Contains("cublas64_12.dll", capability.FailureReason);
    }

    [Fact]
    public void MissingRuntimePackageIsDistinguishedFromMissingDriver()
    {
        var missingPackage = Service(driver: true, package: false, native: false).Detect();
        var missingDriver = Service(driver: false, package: true, native: true).Detect();

        Assert.True(missingPackage.NvidiaDriverDetected);
        Assert.False(missingPackage.Cuda12RuntimeInstalled);
        Assert.Contains("package", missingPackage.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.False(missingDriver.NvidiaDriverDetected);
        Assert.Contains("driver", missingDriver.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResultIsCachedForApplicationSession()
    {
        var calls = 0;
        var service = new WhisperComputeCapabilityService(() => { calls++; return true; },
            () => "cuda.dll", _ => (true, null));

        service.Detect();
        service.Detect();

        Assert.Equal(1, calls);
    }

    private static WhisperComputeCapabilityService Service(bool driver, bool package, bool native,
        string? failure = "CUDA native runtime load failed.") => new(
        () => driver,
        () => package ? "cuda-runtime-test.dll" : null,
        _ => (native, native ? null : failure));
}
