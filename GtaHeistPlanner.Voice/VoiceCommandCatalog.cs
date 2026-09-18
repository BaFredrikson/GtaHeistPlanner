using GtaHeistPlanner.Core.Planning;

namespace GtaHeistPlanner.Voice;

public sealed record VoiceCommandDefinition(
    string Name,
    string Category,
    IReadOnlyList<PlannerStage> Stages,
    IReadOnlyList<string> Aliases,
    string Example);

public static class VoiceCommandCatalog
{
    public static readonly VoiceCommandDefinition GuardDown = new("Guard down", "Guards", [PlannerStage.HeistInfiltration],
        ["tango down", "guard down", "guard dropped", "dropped guard", "dropped a guard", "took out guard", "took out a guard", "guard neutralized"], "Tango down");
    public static readonly VoiceCommandDefinition CameraDown = new("Camera down", "Cameras",
        [PlannerStage.HeistInfiltration, PlannerStage.HeistActivity],
        ["camera down", "camera disabled", "disabled camera", "charlie down", "<camera name>"], "Camera down");
    public static readonly VoiceCommandDefinition ShowroomButton = new("Showroom disable button", "Cameras", [PlannerStage.HeistInfiltration],
        ["shot the button", "shoot the button", "camera button shot"], "Shot the button");

    public static IReadOnlyList<VoiceCommandDefinition> Definitions { get; } =
    [
        new("Scope out", "Scope", [PlannerStage.Preparation], ["scope out"], "Scope out"),
        new("Loot", "Loot", [PlannerStage.Preparation, PlannerStage.HeistActivity], ["<loot name>"], "<loot name>"),
        new("Loot value", "Loot", [PlannerStage.Preparation], ["<value>"], "118 thousand"),
        new("Special loot", "Loot", [PlannerStage.Preparation], ["special loot", "buyer's request"], "Buyer's request"),
        new("Vault code", "Vault", [PlannerStage.Preparation], ["vault code <numbers>"], "Vault code 46 18 73"),
        new("Planning", "Navigation", [PlannerStage.Planning], ["plan out", "plan it", "pan out", "pan it", "overview"], "Plan out"),
        new("Infiltration", "Stage", [PlannerStage.HeistInfiltration], ["heist start", "start heist", "start infiltration", "infiltration start"], "Heist start"),
        GuardDown,
        CameraDown,
        ShowroomButton,
        new("Enter museum", "Enter museum", [PlannerStage.HeistInfiltration], ["going down skylight", "using access codes", "alpha mail arriving"], "Going down skylight"),
        new("Sewer", "Sewer", [PlannerStage.HeistInfiltration],
            ["sewer grate reached", "sewer route", "sower route", "so we're route", "<chamber> Alpha/Bravo/Charlie/Delta/Echo", "undo route", "clear route"],
            "Sewer grate reached"),
        new("Leave sewer", "Stage", [PlannerStage.HeistInfiltration], ["outta the sewers"], "Outta the sewers"),
        new("Maps", "Maps", Enum.GetValues<PlannerStage>(), ["pull up <map>", "pull back", "back up"], "Pull up rooftop"),
        new("Undo", "General", Enum.GetValues<PlannerStage>(), ["undo"], "Undo"),
    ];
}
