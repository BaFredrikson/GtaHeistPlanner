using GtaHeistPlanner.Voice;

namespace GtaHeistPlanner.Tests.Voice;

public sealed class AudioEndpointSelectorTests
{
    private static readonly AudioEndpointDescriptor[] Endpoints =
    [
        new("{capture-endpoint-a}", "Desk microphone"),
        new("{capture-endpoint-b}", "Headset microphone"),
    ];

    [Fact]
    public void ResolvesExactPersistedEndpointId()
    {
        var result = AudioEndpointSelector.ResolveExact("{capture-endpoint-b}", Endpoints);

        Assert.Equal("Headset microphone", result.Name);
        Assert.Equal("{capture-endpoint-b}", result.Id);
    }

    [Fact]
    public void DoesNotSubstituteDefaultOrCaseInsensitiveEndpoint()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            AudioEndpointSelector.ResolveExact("{CAPTURE-ENDPOINT-B}", Endpoints));

        Assert.Contains("unavailable", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingConfigurationReportsSettingsAction()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            AudioEndpointSelector.ResolveExact("", Endpoints));

        Assert.Contains("Settings", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DisconnectedSavedEndpointFailsRatherThanFallingBack()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AudioEndpointSelector.ResolveExact("{disconnected}", Endpoints));
    }
}
