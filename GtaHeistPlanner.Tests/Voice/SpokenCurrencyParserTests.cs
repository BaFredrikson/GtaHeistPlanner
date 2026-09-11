using GtaHeistPlanner.Voice;

namespace GtaHeistPlanner.Tests.Voice;

public sealed class SpokenCurrencyParserTests
{
    [Theory]
    [InlineData("118 thousand 500", 118500)]
    [InlineData("118 thousand and five hundred", 118500)]
    [InlineData("one hundred eighteen thousand five hundred", 118500)]
    [InlineData("118500", 118500)]
    public void ParsesCompleteValuesInOneUtterance(string text, int expected)
    {
        Assert.True(SpokenCurrencyParser.TryParseDetailed(text, out var result));
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void MetadataDistinguishesProvisionalThousandsFromCompleteAndExactValues()
    {
        Assert.True(SpokenCurrencyParser.TryParseDetailed("118 thousand", out var provisional));
        Assert.True(provisional.UsedThousandsUnit);
        Assert.False(provisional.HasExplicitSubThousandComponent);

        Assert.True(SpokenCurrencyParser.TryParseDetailed("118 thousand 500", out var complete));
        Assert.True(complete.UsedThousandsUnit);
        Assert.True(complete.HasExplicitSubThousandComponent);

        Assert.True(SpokenCurrencyParser.TryParseDetailed("118000", out var exact));
        Assert.False(exact.UsedThousandsUnit);
    }
}
