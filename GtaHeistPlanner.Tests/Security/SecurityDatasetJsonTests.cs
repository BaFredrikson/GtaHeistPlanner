using GtaHeistPlanner.Core.Security;

namespace GtaHeistPlanner.Tests.Security;

public sealed class SecurityDatasetJsonTests
{
    [Fact]
    public void RoundTripPreservesMapScopedSecurityData()
    {
        var dataset = new SecurityDataset(
            [new("camera-1", "upperfloor", "North camera", .25, .4, 90, 60, .2)],
            [new("guard-1", "upperfloor", "Lobby guard", .5, .6, ["patrol-1"])],
            [new("patrol-1", "upperfloor", "Lobby loop", [new(.4, .5), new(.6, .5)])]);
        using var stream = new MemoryStream();

        SecurityDatasetJson.Save(stream, dataset);
        stream.Position = 0;
        var loaded = SecurityDatasetJson.Load(stream);

        Assert.Equal(dataset.Cameras[0], loaded.Cameras[0]);
        Assert.Equal(dataset.Guards[0].Id, loaded.Guards[0].Id);
        Assert.Equal(dataset.Guards[0].PatrolIds, loaded.Guards[0].PatrolIds);
        Assert.Equal(dataset.Patrols[0].Id, loaded.Patrols[0].Id);
        Assert.Equal(dataset.Patrols[0].Waypoints, loaded.Patrols[0].Waypoints);
    }

    [Fact]
    public void RejectsCoordinatesOutsideNormalizedMapSpace()
    {
        var dataset = new SecurityDataset(
            [new("camera-1", "upperfloor", "Camera", 1.1, .4, 0, 60, .2)], [], []);

        Assert.Throws<InvalidDataException>(() => SecurityDatasetJson.Validate(dataset));
    }

    [Fact]
    public void RejectsGuardAssignmentToPatrolOnAnotherMap()
    {
        var dataset = new SecurityDataset(
            [],
            [new("guard-1", "mainfloor", "Guard", .5, .5, ["patrol-1"])],
            [new("patrol-1", "upperfloor", "Patrol", [new(.5, .5)])]);

        Assert.Throws<InvalidDataException>(() => SecurityDatasetJson.Validate(dataset));
    }
}
