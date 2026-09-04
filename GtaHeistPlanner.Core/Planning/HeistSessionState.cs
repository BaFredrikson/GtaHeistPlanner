namespace GtaHeistPlanner.Core.Planning;

public sealed class HeistSessionState
{
    private int _playerCount = 1;

    public PlannerStage CurrentStage { get; set; } = PlannerStage.Preparation;

    public int PlayerCount
    {
        get => _playerCount;
        set => _playerCount = value is >= 1 and <= 4
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Player count must be between 1 and 4.");
    }
}
