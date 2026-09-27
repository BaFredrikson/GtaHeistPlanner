namespace GtaHeistPlanner.App.ViewModels;

public sealed record HeistRecommendedLootItemViewModel(
    string Id,
    string Name,
    string Location,
    int BagPercent,
    bool IsBuyersRequest,
    bool IsLooted,
    int? ExactValue,
    int EstimatedMinValue,
    int EstimatedMaxValue)
{
    public string StatusGlyph => IsLooted ? "✓" : "○";
    public string ValueDisplay => ExactValue is { } exact
        ? $"${exact:N0}"
        : $"${EstimatedMinValue:N0}–${EstimatedMaxValue:N0} est.";
}
