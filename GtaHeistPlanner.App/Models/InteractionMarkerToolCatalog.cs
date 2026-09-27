using GtaHeistPlanner.Core.Security;

namespace GtaHeistPlanner.App.Models;

public static class InteractionMarkerToolCatalog
{
    public static IReadOnlyList<SecurityEditorTool> Tools { get; } =
    [
        SecurityEditorTool.Select,
        SecurityEditorTool.SewerEntrance,
        SecurityEditorTool.Rappel,
        SecurityEditorTool.Elevator,
        SecurityEditorTool.Keycard,
        SecurityEditorTool.Painting,
        SecurityEditorTool.Keypad,
    ];

    public static bool TryGetMarkerType(SecurityEditorTool tool, out MapInteractionMarkerType type)
    {
        type = tool switch
        {
            SecurityEditorTool.SewerEntrance => MapInteractionMarkerType.SewerEntrance,
            SecurityEditorTool.Rappel => MapInteractionMarkerType.Rappel,
            SecurityEditorTool.Elevator => MapInteractionMarkerType.Elevator,
            SecurityEditorTool.Keycard => MapInteractionMarkerType.Keycard,
            SecurityEditorTool.Painting => MapInteractionMarkerType.Painting,
            SecurityEditorTool.Keypad => MapInteractionMarkerType.Keypad,
            _ => default,
        };

        return tool is SecurityEditorTool.SewerEntrance or SecurityEditorTool.Rappel
            or SecurityEditorTool.Elevator or SecurityEditorTool.Keycard
            or SecurityEditorTool.Painting or SecurityEditorTool.Keypad;
    }
}
