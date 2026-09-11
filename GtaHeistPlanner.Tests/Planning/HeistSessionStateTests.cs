using GtaHeistPlanner.Core.Planning;

namespace GtaHeistPlanner.Tests.Planning;

public sealed class HeistSessionStateTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void AcceptsValidPlayerCounts(int count)
    {
        var session = new HeistSessionState { PlayerCount = count };
        Assert.Equal(count, session.PlayerCount);
    }

    [Theory]
    [InlineData(0)] [InlineData(5)]
    public void RejectsInvalidPlayerCounts(int count) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new HeistSessionState { PlayerCount = count });

    [Fact]
    public void ChangingStagePreservesPlayerAndLootSessionState()
    {
        var session = new HeistSessionState { PlayerCount = 3 };
        var scopedLootIds = new HashSet<string> { "main-floor-loot-01" };

        session.CurrentStage = PlannerStage.HeistActivity;

        Assert.Equal(3, session.PlayerCount);
        Assert.Contains("main-floor-loot-01", scopedLootIds);
    }

    [Fact]
    public void VaultCodeSurvivesStageChangesAndClearsOnReset()
    {
        var session = new HeistSessionState { VaultCode = "12-34-56" };

        session.CurrentStage = PlannerStage.HeistActivity;
        Assert.Equal("12-34-56", session.VaultCode);

        session.Reset();
        Assert.Null(session.VaultCode);
    }

    [Fact]
    public void CameraCounterRecordsBeyondLimitAndResetClearsRunState()
    {
        var session = new HeistSessionState { PlayerCount = 4, GuardsDown = 3, VaultCode = "46-18-73" };
        Assert.True(session.TryIncrementCamerasDown(out _));
        Assert.True(session.TryIncrementCamerasDown(out _));
        Assert.True(session.TryIncrementCamerasDown(out var error));
        Assert.True(session.TryIncrementCamerasDown(out _));
        Assert.Equal(4, session.CamerasDown);
        Assert.True(session.IsCameraStealthCompromised);
        Assert.Contains("stealth", error, StringComparison.OrdinalIgnoreCase);

        session.Reset();
        Assert.Equal(1, session.PlayerCount);
        Assert.Equal(PlannerStage.Preparation, session.CurrentStage);
        Assert.Equal(0, session.GuardsDown);
        Assert.Equal(0, session.CamerasDown);
    }
}
