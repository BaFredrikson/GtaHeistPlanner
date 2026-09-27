using System.Buffers.Binary;
using System.IO.Compression;

namespace GtaHeistPlanner.DataExtractor;

public sealed record Rsc7ResourceHeader(
    uint Version,
    uint SystemFlags,
    uint GraphicsFlags)
{
    public const int Size = 16;

    public static Rsc7ResourceHeader Parse(ReadOnlySpan<byte> resource)
    {
        if (resource.Length < 4 || !resource[..4].SequenceEqual("RSC7"u8))
            throw new InvalidDataException("The input does not have an RSC7 resource header.");
        if (resource.Length < Size)
            throw new InvalidDataException($"Malformed RSC7 resource: the {Size}-byte header is truncated (found {resource.Length} bytes).");

        return new Rsc7ResourceHeader(
            BinaryPrimitives.ReadUInt32LittleEndian(resource[4..8]),
            BinaryPrimitives.ReadUInt32LittleEndian(resource[8..12]),
            BinaryPrimitives.ReadUInt32LittleEndian(resource[12..16]));
    }
}

public interface IRsc7ResourceDecoder
{
    byte[] Decode(string sourcePath, Rsc7ResourceHeader header, ReadOnlyMemory<byte> packedResource);
}

public sealed class Rsc7DecodingUnavailableException : Exception
{
    public Rsc7DecodingUnavailableException(string message) : base(message) { }
}

public sealed class UnavailableRsc7ResourceDecoder : IRsc7ResourceDecoder
{
    public byte[] Decode(string sourcePath, Rsc7ResourceHeader header, ReadOnlyMemory<byte> packedResource) =>
        throw new Rsc7DecodingUnavailableException(
            "RSC7 YSC resource detected. This file is still packed/encrypted; decoding is unavailable because " +
            "the repository has no CodeWalker/Rage resource decoder or GTA V key material. Export/decode it " +
            "with a supported CodeWalker resource pipeline before analysis. Do not merely remove the 16-byte header. " +
            "The existing extractor accepts CScenarioPointRegion XML; a future YSC analysis path requires the genuine " +
            "decrypted and decompressed script payload with a structurally valid script header/sections.");
}

/// <summary>
/// Decodes the OpenIV/CodeWalker-compatible standalone resource form: a 16-byte
/// RSC7 header followed by a raw DEFLATE stream. Archive encryption, when present,
/// must already have been removed by the exporting resource pipeline.
/// </summary>
public sealed class DeflateRsc7ResourceDecoder : IRsc7ResourceDecoder
{
    private const int MaximumDecodedSize = 512 * 1024 * 1024;

    public byte[] Decode(string sourcePath, Rsc7ResourceHeader header, ReadOnlyMemory<byte> packedResource)
    {
        if (packedResource.Length <= Rsc7ResourceHeader.Size)
            throw new InvalidDataException("Malformed RSC7 resource: no compressed payload follows the header.");

        try
        {
            using var input = new MemoryStream(packedResource[Rsc7ResourceHeader.Size..].ToArray(), writable: false);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int count;
            while ((count = deflate.Read(buffer, 0, buffer.Length)) != 0)
            {
                if (output.Length + count > MaximumDecodedSize)
                    throw new InvalidDataException($"Decoded RSC7 payload exceeds the {MaximumDecodedSize:N0}-byte safety limit.");
                output.Write(buffer, 0, count);
            }
            return output.ToArray();
        }
        catch (InvalidDataException exception) when (!exception.Message.Contains("safety limit", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "RSC7 container detected, but its payload is not a valid standalone raw-DEFLATE resource. " +
                "It may still be archive-encrypted and require CodeWalker's GTA-derived AES/NG key state.", exception);
        }
    }
}

public sealed record DecodedYscPayloadInfo(int Size, int ResourcePointerCount);

public static class DecodedYscPayloadInspector
{
    public static DecodedYscPayloadInfo Inspect(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 64)
            throw new InvalidDataException("Decoded payload is too short to contain a GTA V script resource header.");

        var pointerCount = 0;
        var headerLength = Math.Min(payload.Length, 512);
        for (var offset = 0; offset + sizeof(ulong) <= headerLength; offset += sizeof(ulong))
        {
            var value = BinaryPrimitives.ReadUInt64LittleEndian(payload[offset..]);
            var resourceArea = value & 0xFF000000;
            if ((value >> 32) == 0 && resourceArea is 0x50000000 or 0x60000000)
                pointerCount++;
        }

        if (pointerCount == 0)
            throw new InvalidDataException(
                "The decompressed bytes do not contain any system/graphics resource pointers in the root header; " +
                "the payload cannot yet be accepted as a structurally valid GTA V script resource.");

        return new DecodedYscPayloadInfo(payload.Length, pointerCount);
    }
}

public static class Rsc7ResourceProcessor
{
    public static byte[] Decode(string sourcePath, IRsc7ResourceDecoder decoder)
    {
        var resource = File.ReadAllBytes(sourcePath);
        return Decode(sourcePath, resource, decoder);
    }

    public static byte[] Decode(string sourcePath, ReadOnlyMemory<byte> resource, IRsc7ResourceDecoder decoder)
    {
        var header = Rsc7ResourceHeader.Parse(resource.Span);
        var decoded = decoder.Decode(sourcePath, header, resource);
        if (decoded.Length == 0)
            throw new InvalidDataException("The RSC7 decoder returned an empty script payload.");
        if (InputFormatDetector.Detect(decoded) == ExtractorInputFormat.Rsc7Resource)
            throw new InvalidDataException("The RSC7 decoder returned another packed RSC7 container instead of a decoded script payload.");
        return decoded;
    }
}
