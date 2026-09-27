using System.Buffers.Binary;
using System.Text;

namespace GtaHeistPlanner.DataExtractor;

/// <summary>
/// Parses the PC GTA V YSC resource layout documented by the open-source ysc-utils reader.
/// Resource pointers are virtual (0x50/0x60 area) and their low 24 bits address the decoded
/// resource image; they must not be used as raw 64-bit file offsets.
/// </summary>
public static class YscParser
{
    public const int HeaderSize = 144;
    public const int PageSize = 0x4000;

    public static YscScript Parse(ReadOnlyMemory<byte> payload)
    {
        var data = payload.Span;
        if (data.Length < HeaderSize)
            throw new InvalidDataException($"YSC payload is truncated: expected at least {HeaderSize} header bytes, found {data.Length}.");

        var header = ParseHeader(data);
        ValidateCount("code size", header.CodeSize);
        ValidateCount("string size", header.StringSize);
        ValidateCount("native count", header.NativeCount);
        ValidateCount("static count", header.StaticCount);
        ValidateCount("global count", header.GlobalCount);

        var code = ReadPagedSection(data, header.CodeBlocksPointer, header.CodeSize, "code");
        var strings = ReadPagedSection(data, header.StringBlocksPointer, header.StringSize, "string");
        var scriptName = ReadNullTerminated(data, TranslatePointer(header.ScriptNamePointer, data.Length, "script name"), "script name");
        var natives = ReadNatives(data, header);
        var statics = ReadValues(data, header.StaticsPointer, header.StaticCount, "statics");
        var globals = ReadValues(data, header.GlobalsPointer, header.GlobalCount, "globals");
        var stringEntries = DecodeStrings(strings.Bytes);
        var instructions = YscInstructionDecoder.Decode(code.Bytes);

        return new YscScript(scriptName, header,
            new YscCodeSection(header.CodeSize, code.Pages),
            new YscStringTable(header.StringSize, strings.Pages, stringEntries),
            natives, statics, globals, instructions, []);
    }

    public static YscHeader ParseHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
            throw new InvalidDataException("YSC root header is truncated.");

        return new YscHeader(
            U64(data, 0), U64(data, 8), U64(data, 16), U32(data, 24), I32(data, 28),
            I32(data, 32), I32(data, 36), I32(data, 40), I32(data, 44),
            U64(data, 48), U64(data, 56), U64(data, 64), U32(data, 88),
            U64(data, 96), U64(data, 104), I32(data, 112),
            U64(data, 72), U64(data, 80), U32(data, 92), U64(data, 120), U64(data, 128), U64(data, 136));
    }

    public static int TranslatePointer(ulong pointer, int payloadLength, string fieldName)
    {
        if (pointer == 0)
            return 0;
        var area = pointer & 0xFF000000;
        if (area is not (0x50000000 or 0x60000000))
            throw new InvalidDataException($"YSC {fieldName} pointer 0x{pointer:X16} is not in a known resource virtual area.");
        var offset = checked((int)(pointer & 0x00FFFFFF));
        if (offset < 0 || offset >= payloadLength)
            throw new InvalidDataException($"YSC {fieldName} pointer 0x{pointer:X16} resolves outside the {payloadLength:N0}-byte payload.");
        return offset;
    }

    private static (byte[] Bytes, IReadOnlyList<YscPage> Pages) ReadPagedSection(
        ReadOnlySpan<byte> data, ulong tablePointer, int length, string name)
    {
        if (length == 0)
            return ([], []);
        var pageCount = checked((length + PageSize - 1) / PageSize);
        var tableOffset = TranslatePointer(tablePointer, data.Length, $"{name} page table");
        EnsureRange(data, tableOffset, checked(pageCount * 8), $"{name} page table");
        var bytes = new byte[length];
        var pages = new List<YscPage>(pageCount);

        for (var i = 0; i < pageCount; i++)
        {
            var pointer = U64(data, tableOffset + i * 8);
            var offset = TranslatePointer(pointer, data.Length, $"{name} page {i}");
            var pageLength = Math.Min(PageSize, length - i * PageSize);
            EnsureRange(data, offset, pageLength, $"{name} page {i}");
            data.Slice(offset, pageLength).CopyTo(bytes.AsSpan(i * PageSize, pageLength));
            pages.Add(new YscPage(i, i * PageSize, pageLength, pointer, offset));
        }
        return (bytes, pages);
    }

    private static IReadOnlyList<YscStringEntry> DecodeStrings(ReadOnlySpan<byte> table)
    {
        var result = new List<YscStringEntry>();
        var start = 0;
        while (start < table.Length)
        {
            var terminator = table[start..].IndexOf((byte)0);
            if (terminator < 0)
                throw new InvalidDataException($"YSC string at logical offset {start} is not null terminated before the table ends.");
            if (terminator > 0)
            {
                var text = Encoding.UTF8.GetString(table.Slice(start, terminator));
                result.Add(new YscStringEntry(start, text, terminator, start / PageSize, start % PageSize));
            }
            start += terminator + 1;
        }
        return result;
    }

    private static IReadOnlyList<YscNativeEntry> ReadNatives(ReadOnlySpan<byte> data, YscHeader header)
    {
        if (header.NativeCount == 0)
            return [];
        var offset = TranslatePointer(header.NativesPointer, data.Length, "native table");
        EnsureRange(data, offset, checked(header.NativeCount * 8), "native table");
        var result = new List<YscNativeEntry>(header.NativeCount);
        for (var i = 0; i < header.NativeCount; i++)
        {
            var raw = U64(data, offset + i * 8);
            var decoded = RotateLeft(raw, (header.CodeSize + i) & 63);
            result.Add(new YscNativeEntry(i, $"0x{raw:X16}", $"0x{decoded:X16}"));
        }
        return result;
    }

    private static IReadOnlyList<YscValueEntry> ReadValues(ReadOnlySpan<byte> data, ulong pointer, int count, string name)
    {
        if (count == 0)
            return [];
        var offset = TranslatePointer(pointer, data.Length, name);
        EnsureRange(data, offset, checked(count * 8), name);
        var result = new List<YscValueEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var value = U64(data, offset + i * 8);
            result.Add(new YscValueEntry(i, $"0x{value:X16}", unchecked((long)value), value,
                BitConverter.Int64BitsToDouble(unchecked((long)value)).ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                BitConverter.Int32BitsToSingle(unchecked((int)value)).ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        }
        return result;
    }

    private static string ReadNullTerminated(ReadOnlySpan<byte> data, int offset, string name)
    {
        var terminator = data[offset..].IndexOf((byte)0);
        if (terminator < 0)
            throw new InvalidDataException($"YSC {name} is not null terminated.");
        return Encoding.UTF8.GetString(data.Slice(offset, terminator));
    }

    private static void EnsureRange(ReadOnlySpan<byte> data, int offset, int length, string name)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
            throw new InvalidDataException($"YSC {name} range [{offset}, {offset + (long)length}) exceeds payload size {data.Length}.");
    }

    private static void ValidateCount(string name, int value)
    {
        if (value < 0)
            throw new InvalidDataException($"YSC {name} cannot be negative ({value}).");
    }

    private static uint U32(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
    private static int I32(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    private static ulong U64(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(data[offset..]);
    private static ulong RotateLeft(ulong value, int count) => count == 0 ? value : (value << count) | (value >> (64 - count));
}
