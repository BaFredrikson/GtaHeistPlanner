using GtaHeistPlanner.App.Models;
using NAudio.CoreAudioApi;
using System.Runtime.Versioning;

namespace GtaHeistPlanner.App.Services;

public sealed class RecordingDeviceService
{
    public IReadOnlyList<RecordingDeviceInfo> GetAvailableDevices()
    {
        if (!OperatingSystem.IsWindows())
            return [];

        return GetWindowsDevices();
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<RecordingDeviceInfo> GetWindowsDevices()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator
                .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .Select(device => new RecordingDeviceInfo(device.ID, device.FriendlyName))
                .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch
        {
            // Settings remains usable even when Windows Core Audio is unavailable.
            return [];
        }
    }
}
