using GtaHeistPlanner.Core.Planning;
using GtaHeistPlanner.Core.Security;
using System.Text;

namespace GtaHeistPlanner.Tests.Security;

public sealed class MapInteractionMarkerTests
{
    [Theory]
    [InlineData(MapInteractionMarkerType.SewerEntrance, "sewer_entrance.png")]
    [InlineData(MapInteractionMarkerType.Rappel, "rappel.png")]
    [InlineData(MapInteractionMarkerType.Elevator, "elevator.png")]
    [InlineData(MapInteractionMarkerType.Keycard, "keycard.png")]
    public void TypeMapsToExpectedIcon(MapInteractionMarkerType type, string fileName) =>
        Assert.Equal(fileName, MapInteractionMarkerIcons.FileName(type));

    [Fact]
    public void PermanentDatasetRoundTripPreservesMarkerAndNormalizedCoordinates()
    {
        var expected = new MapInteractionMarker(
            "basement-elevator-01", "basement", MapInteractionMarkerType.Elevator, .421, .688, "Service Elevator");
        using var stream = new MemoryStream();

        SecurityDatasetJson.Save(stream, new([], [], [], [expected]));
        var json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("\"interactionMarkers\"", json);
        Assert.DoesNotContain("\"markers\"", json);
        stream.Position = 0;
        var actual = Assert.Single(SecurityDatasetJson.Load(stream).Markers);

        Assert.Equal(expected, actual);
        Assert.InRange(actual.X, 0, 1);
        Assert.InRange(actual.Y, 0, 1);
    }

    [Theory]
    [InlineData(MapInteractionMarkerType.SewerEntrance)]
    [InlineData(MapInteractionMarkerType.Rappel)]
    [InlineData(MapInteractionMarkerType.Elevator)]
    [InlineData(MapInteractionMarkerType.Keycard)]
    public void DeveloperPlacementCreatesRequestedType(MapInteractionMarkerType type)
    {
        var marker = MapInteractionMarkerFactory.Create(type, "main-floor", .25, .75, []);

        Assert.Equal(type, marker.Type);
        Assert.Equal("main-floor", marker.MapId);
        Assert.Equal(.25, marker.X);
        Assert.Equal(.75, marker.Y);
        Assert.False(string.IsNullOrWhiteSpace(marker.Id));
    }

    [Fact]
    public void StagePolicyControlsMarkerVisibilityAndDeveloperAuthoring()
    {
        var preparation = StageViewPolicies.Get(PlannerStage.Preparation);
        var planning = StageViewPolicies.Get(PlannerStage.Planning);
        var infiltration = StageViewPolicies.Get(PlannerStage.HeistInfiltration);
        var activity = StageViewPolicies.Get(PlannerStage.HeistActivity);

        Assert.False(preparation.AllowsOverlay(OverlayType.InteractionMarkers, "main-floor"));
        Assert.True(planning.AllowsOverlay(OverlayType.InteractionMarkers, "main-floor"));
        Assert.True(infiltration.AllowsOverlay(OverlayType.InteractionMarkers, "sewer"));
        Assert.True(activity.AllowsOverlay(OverlayType.InteractionMarkers, "basement"));
        Assert.False(DeveloperViewPolicy.CanAuthorInteractionMarkers(false, activity, "basement"));
        Assert.True(DeveloperViewPolicy.CanAuthorInteractionMarkers(true, activity, "basement"));
    }

    [Fact]
    public void RuntimeHeistSaveContractDoesNotContainStaticMarkers()
    {
        var properties = typeof(HeistSaveFile).GetProperties().Select(property => property.Name);

        Assert.DoesNotContain(properties, name => name.Contains("Interaction", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("Marker", StringComparison.OrdinalIgnoreCase));
    }
}
