namespace GtaHeistPlanner.Core.Security;

public static class MapInteractionMarkerIcons
{
    public static string FileName(MapInteractionMarkerType type) => type switch
    {
        MapInteractionMarkerType.SewerEntrance => "sewer_entrance.png",
        MapInteractionMarkerType.Rappel => "rappel.png",
        MapInteractionMarkerType.Elevator => "elevator.png",
        MapInteractionMarkerType.Keycard => "keycard.png",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static string DefaultDisplayName(MapInteractionMarkerType type) => type switch
    {
        MapInteractionMarkerType.SewerEntrance => "Sewer Entrance",
        MapInteractionMarkerType.Rappel => "Rappel",
        MapInteractionMarkerType.Elevator => "Elevator",
        MapInteractionMarkerType.Keycard => "Keycard",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}

