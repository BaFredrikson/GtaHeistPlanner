using GtaHeistPlanner.Core.Loot;
using GtaHeistPlanner.Voice;

namespace GtaHeistPlanner.Tests.Voice;

public sealed class ScopeOutVoiceSessionTests
{
    [Fact]
    public void ActiveSessionUpdatesOnlyRequestedRunStateAndNotDefinition()
    {
        var target = Definition("target", "west painting");
        var untouched = Definition("untouched", "east painting");
        var definitions = new[] { target, untouched };
        var run = new LootRunState(definitions);
        var session = new ScopeOutVoiceSession(definitions, run);
        session.SetListening(true);
        session.Process("scope out");

        var result = session.Process("west painting 118 thousand");

        Assert.True(result.Applied);
        Assert.True(run.GetState("target").IsPresent);
        Assert.Equal(118000, run.GetState("target").ScopedValue);
        Assert.False(run.GetState("untouched").IsPresent);
        Assert.Equal(target, definitions[0]);
        Assert.Equal("West Painting", target.Name);
        Assert.Equal(.2, target.X);
    }

    [Fact]
    public void CommandsAreIgnoredUntilActivatedAndAfterStopping()
    {
        var definition = Definition("target", "west painting");
        var run = new LootRunState([definition]);
        var session = new ScopeOutVoiceSession([definition], run);
        session.SetListening(true);

        Assert.False(session.Process("west painting 34 thousand").Applied);
        Assert.False(run.GetState("target").IsPresent);
        Assert.True(session.Process("scope out").Applied);
        Assert.Equal(ScopeOutSessionState.Active, session.State);
        Assert.True(session.Process("stop scope out").Applied);
        Assert.Equal(ScopeOutSessionState.WaitingForActivation, session.State);
        Assert.False(session.Process("west painting 34 thousand").Applied);
    }

    private static LootSpawnDefinition Definition(string id, string alias) =>
        new(id, "main-floor", alias == "west painting" ? "West Painting" : "East Painting", LootType.Painting, .2, .3)
        {
            VoiceAliases = [alias],
        };
}
