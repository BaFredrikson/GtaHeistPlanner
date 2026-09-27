using GtaHeistPlanner.App.Models;
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
    [InlineData(MapInteractionMarkerType.Painting, "painting.png")]
    [InlineData(MapInteractionMarkerType.Keypad, "keypad.png")]
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
    [InlineData(MapInteractionMarkerType.Painting)]
    [InlineData(MapInteractionMarkerType.Keypad)]
    public void DeveloperPlacementCreatesRequestedType(MapInteractionMarkerType type)
    {
        var marker = MapInteractionMarkerFactory.Create(type, "main-floor", .25, .75, []);

        Assert.Equal(type, marker.Type);
        Assert.Equal("main-floor", marker.MapId);
        Assert.Equal(.25, marker.X);
        Assert.Equal(.75, marker.Y);
        Assert.False(string.IsNullOrWhiteSpace(marker.Id));
    }

    [Theory]
    [InlineData(MapInteractionMarkerType.Painting, 4)]
    [InlineData(MapInteractionMarkerType.Keypad, 5)]
    public void NewMarkerTypesRoundTripWithStablePersistedValues(MapInteractionMarkerType type, int persistedValue)
    {
        var expected = new MapInteractionMarker("new-marker", "main-floor", type, .2, .3,
            MapInteractionMarkerIcons.DefaultDisplayName(type));
        using var stream = new MemoryStream();

        SecurityDatasetJson.Save(stream, new([], [], [], [expected]));
        var json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains($"\"type\": {persistedValue}", json);
        stream.Position = 0;

        Assert.Equal(type, Assert.Single(SecurityDatasetJson.Load(stream).Markers).Type);
    }

    [Theory]
    [InlineData(SecurityEditorTool.Painting, MapInteractionMarkerType.Painting)]
    [InlineData(SecurityEditorTool.Keypad, MapInteractionMarkerType.Keypad)]
    public void DeveloperEditorOffersAndMapsNewMarkerTools(
        SecurityEditorTool tool, MapInteractionMarkerType expectedType)
    {
        Assert.Contains(tool, InteractionMarkerToolCatalog.Tools);
        Assert.True(InteractionMarkerToolCatalog.TryGetMarkerType(tool, out var actualType));
        Assert.Equal(expectedType, actualType);
    }

    [Fact]
    public void LegacyNumericMarkerTypesStillLoadWithoutMigration()
    {
        const string legacyJson = """
            {
              "cameras": [],
              "guards": [],
              "patrols": [],
              "interactionMarkers": [
                { "id": "old-sewer", "mapId": "sewer", "type": 0, "x": 0.2, "y": 0.3, "displayName": "Sewer Entrance" },
                { "id": "old-keycard", "mapId": "main-floor", "type": 3, "x": 0.4, "y": 0.5, "displayName": "Keycard" }
              ]
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(legacyJson));

        var markers = SecurityDatasetJson.Load(stream).Markers;

        Assert.Equal(MapInteractionMarkerType.SewerEntrance, markers[0].Type);
        Assert.Equal(MapInteractionMarkerType.Keycard, markers[1].Type);
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
