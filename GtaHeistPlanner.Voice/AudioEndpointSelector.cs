namespace GtaHeistPlanner.Voice;

public sealed record AudioEndpointDescriptor(string Id, string Name);

public static class AudioEndpointSelector
{
    public static AudioEndpointDescriptor ResolveExact(
        string endpointId,
        IEnumerable<AudioEndpointDescriptor> activeCaptureEndpoints)
    {
        if (string.IsNullOrWhiteSpace(endpointId))
            throw new InvalidOperationException("No recording device is configured. Select one in Settings.");

        return activeCaptureEndpoints.SingleOrDefault(endpoint =>
                   string.Equals(endpoint.Id, endpointId, StringComparison.Ordinal))
               ?? throw new InvalidOperationException(
                   $"The configured recording device is unavailable: '{endpointId}'. Select an active input device in Settings.");
    }
}
