using System.Text.Json.Serialization;

namespace GtaHeistPlanner.Core.Security;

public sealed record SecurityDataset(
    IReadOnlyList<SecurityCameraDefinition> Cameras,
    IReadOnlyList<SecurityGuardDefinition> Guards,
    IReadOnlyList<SecurityPatrolDefinition> Patrols,
    IReadOnlyList<MapInteractionMarker>? InteractionMarkers = null)
{
    [JsonIgnore]
    public IReadOnlyList<MapInteractionMarker> Markers => InteractionMarkers ?? [];
}
