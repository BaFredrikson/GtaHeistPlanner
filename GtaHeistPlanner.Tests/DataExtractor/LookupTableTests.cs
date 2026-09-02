using GtaHeistPlanner.DataExtractor;

namespace GtaHeistPlanner.Tests.DataExtractor;

public sealed class LookupTableTests
{
    [Fact]
    public void Resolve_ReturnsValueAtSourceIndex()
    {
        var lookup = new LookupTable(["none", "security"], "Ped model set");

        Assert.Equal("security", lookup.Resolve(1));
    }

    [Fact]
    public void Resolve_ThrowsForInvalidIndex()
    {
        var lookup = new LookupTable(["only"], "Test");

        var exception = Assert.Throws<InvalidDataException>(() => lookup.Resolve(2));
        Assert.Contains("index 2", exception.Message);
    }
}
