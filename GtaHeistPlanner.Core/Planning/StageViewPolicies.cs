namespace GtaHeistPlanner.Core.Planning;

public static class StageViewPolicies
{
    private static readonly IReadOnlySet<string> LootMaps = new HashSet<string>(StringComparer.Ordinal)
    {
        "main-floor", "upper-floor", "lower-floor", "basement",
    };

    private static readonly IReadOnlySet<string> ExteriorMaps = new HashSet<string>(StringComparer.Ordinal)
    {
        "exterior-firstfloor", "exterior-second-floor",
        "exterior-rooftop",
    };

    private static readonly IReadOnlySet<string> InfiltrationMaps = new HashSet<string>(ExteriorMaps, StringComparer.Ordinal)
    {
        "sewer",
    };

    private static readonly IReadOnlySet<string> ActivityMaps = new HashSet<string>(LootMaps, StringComparer.Ordinal);

    public static StageViewPolicy Get(PlannerStage stage) => stage switch
    {
        PlannerStage.Preparation => new(stage, LootMaps, LootVisibilityMode.AllClearly,
            false, false, false, false, false),
        PlannerStage.Planning => new(stage, LootMaps, LootVisibilityMode.ScopedOnly,
            false, false, false, false, false),
        PlannerStage.HeistInfiltration => new(stage, InfiltrationMaps, LootVisibilityMode.Hidden,
            true, true, false, true, false),
        PlannerStage.HeistActivity => new(stage, ActivityMaps, LootVisibilityMode.ScopedOnly,
            false, false, true, false, true),
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    };
}
