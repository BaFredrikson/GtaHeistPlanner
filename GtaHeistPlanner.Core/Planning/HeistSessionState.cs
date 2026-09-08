namespace GtaHeistPlanner.Core.Planning;

public sealed class HeistSessionState
{
    public const int CameraDisableLimit = 2;
    private int _playerCount = 1;

    public PlannerStage CurrentStage { get; set; } = PlannerStage.Preparation;
    public string? VaultCode { get; set; }
    public int GuardsDown { get; set; }
    public int CamerasDown { get; set; }

    public bool TryIncrementCamerasDown(out string? error)
    {
        if (CamerasDown >= CameraDisableLimit)
        {
            error = $"Camera limit reached ({CameraDisableLimit}/{CameraDisableLimit}); disabling another camera would blow stealth.";
            return false;
        }
        CamerasDown++;
        error = null;
        return true;
    }

    public void Reset()
    {
        PlayerCount = 1;
        CurrentStage = PlannerStage.Preparation;
        VaultCode = null;
        GuardsDown = 0;
        CamerasDown = 0;
    }

    public int PlayerCount
    {
        get => _playerCount;
        set => _playerCount = value is >= 1 and <= 4
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Player count must be between 1 and 4.");
    }
}
