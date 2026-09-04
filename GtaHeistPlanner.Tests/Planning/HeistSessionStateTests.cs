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
}
