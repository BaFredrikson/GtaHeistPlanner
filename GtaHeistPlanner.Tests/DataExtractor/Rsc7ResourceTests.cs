using System.Buffers.Binary;
using System.IO.Compression;
using GtaHeistPlanner.DataExtractor;

namespace GtaHeistPlanner.Tests.DataExtractor;

public sealed class Rsc7ResourceTests
{
    [Fact]
    public void Detect_RecognizesRsc7AndXmlInputs()
    {
        Assert.Equal(ExtractorInputFormat.Rsc7Resource, InputFormatDetector.Detect("RSC7payload"u8));
        Assert.Equal(ExtractorInputFormat.ScenarioRegionXml,
            InputFormatDetector.Detect("\uFEFF  \r\n<CScenarioPointRegion />"u8));
    }

    [Fact]
    public void UnsupportedDecoder_ExplainsThatResourceIsStillPacked()
    {
        var resource = CreateResource();

        var exception = Assert.Throws<Rsc7DecodingUnavailableException>(() =>
            Rsc7ResourceProcessor.Decode("test.ysc", resource, new UnavailableRsc7ResourceDecoder()));

        Assert.Contains("still packed/encrypted", exception.Message);
        Assert.Contains("CodeWalker", exception.Message);
        Assert.Contains("Do not merely remove", exception.Message);
    }

    [Fact]
    public void DecoderAbstraction_ReturnsDecodedPayload()
    {
        var expected = "decoded-script-payload"u8.ToArray();
        var decoder = new StubDecoder((_, header, _) =>
        {
            Assert.Equal(12u, header.Version);
            return expected;
        });

        var actual = Rsc7ResourceProcessor.Decode("test.ysc", CreateResource(), decoder);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DecoderFailure_PreservesOriginalFailure()
    {
        var decoder = new StubDecoder((_, _, _) => throw new InvalidDataException("key unavailable"));

        var exception = Assert.Throws<InvalidDataException>(() =>
            Rsc7ResourceProcessor.Decode("test.ysc", CreateResource(), decoder));

        Assert.Equal("key unavailable", exception.Message);
    }

    [Fact]
    public void MalformedRsc7Header_FailsSafely()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            Rsc7ResourceHeader.Parse("RSC7short"u8));

        Assert.Contains("header is truncated", exception.Message);
    }

    [Fact]
    public void ParsedHeader_PreservesResourceMetadata()
    {
        var header = Rsc7ResourceHeader.Parse(CreateResource());

        Assert.Equal(12u, header.Version);
        Assert.Equal(0x00026080u, header.SystemFlags);
        Assert.Equal(0xC0000000u, header.GraphicsFlags);
    }

    [Fact]
    public void DeflateDecoder_DecodesStandaloneResource()
    {
        var decoded = new byte[80];
        BinaryPrimitives.WriteUInt64LittleEndian(decoded.AsSpan(8), 0x50000020);
        var resource = CreateCompressedResource(decoded);

        var actual = Rsc7ResourceProcessor.Decode("test.ysc", resource, new DeflateRsc7ResourceDecoder());
        var inspection = DecodedYscPayloadInspector.Inspect(actual);

        Assert.Equal(decoded, actual);
        Assert.Equal(1, inspection.ResourcePointerCount);
    }

    [Fact]
    public void DeflateDecoder_RejectsInvalidCompressedPayload()
    {
        var resource = CreateResource()[..17];
        resource[16] = 0x07; // BTYPE=3 is reserved/invalid in DEFLATE.
        var exception = Assert.Throws<InvalidDataException>(() =>
            Rsc7ResourceProcessor.Decode("test.ysc", resource, new DeflateRsc7ResourceDecoder()));

        Assert.Contains("not a valid standalone raw-DEFLATE resource", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    private static byte[] CreateResource()
    {
        var bytes = new byte[32];
        "RSC7"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 12);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0x00026080);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0xC0000000);
        bytes[16] = 0xA5;
        return bytes;
    }

    private static byte[] CreateCompressedResource(byte[] decoded)
    {
        using var output = new MemoryStream();
        output.Write(CreateResource(), 0, Rsc7ResourceHeader.Size);
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(decoded);
        return output.ToArray();
    }

    private sealed class StubDecoder(
        Func<string, Rsc7ResourceHeader, ReadOnlyMemory<byte>, byte[]> decode) : IRsc7ResourceDecoder
    {
        public byte[] Decode(string sourcePath, Rsc7ResourceHeader header, ReadOnlyMemory<byte> packedResource) =>
            decode(sourcePath, header, packedResource);
    }
}
