namespace GtaHeistPlanner.App.Models;

public sealed record RecordingDeviceInfo(string Id, string Name)
{
    public override string ToString() => Name;
}
