namespace GtaHeistPlanner.Core.Security;

public sealed record SecurityDataset(
    IReadOnlyList<SecurityCameraDefinition> Cameras,
    IReadOnlyList<SecurityGuardDefinition> Guards,
    IReadOnlyList<SecurityPatrolDefinition> Patrols);
