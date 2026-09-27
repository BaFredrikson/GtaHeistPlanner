using System.Buffers.Binary;
using System.Text;
using GtaHeistPlanner.DataExtractor;

namespace GtaHeistPlanner.Tests.DataExtractor;

public sealed class YscParserTests
{
    [Fact]
    public void TranslatePointer_UsesResourceAreaAndLow24BitOffset()
    {
        Assert.Equal(0x2345, YscParser.TranslatePointer(0x50002345, 0x3000, "test"));
        Assert.Throws<InvalidDataException>(() => YscParser.TranslatePointer(0x1234, 0x3000, "test"));
        Assert.Throws<InvalidDataException>(() => YscParser.TranslatePointer(0x50004000, 0x3000, "test"));
    }

    [Fact]
    public void Parse_ReadsHeaderAndAllTables()
    {
        var script = YscParser.Parse(CreatePayload());

        Assert.Equal("kortz_test", script.Name);
        Assert.Equal(7, script.Header.CodeSize);
        Assert.Equal(2, script.Header.ParameterCount);
        Assert.Single(script.Code.Pages);
        Assert.Equal(7, script.Code.Pages[0].Length);
        Assert.Equal(["Hello", "World"], script.StringTable.Entries.Select(x => x.Text));
        Assert.Equal("0x0000000000000080", script.Natives[0].DecodedHash);
        Assert.Equal(42UL, script.Statics[0].UnsignedInteger);
        Assert.Equal(0x3F800000UL, script.Globals[0].UnsignedInteger);
        Assert.Equal(["PUSH_CONST_U8", "PUSH_CONST_U32"], script.Instructions.Select(x => x.Opcode));
    }

    [Fact]
    public void Parse_RejectsTruncatedHeader()
    {
        var exception = Assert.Throws<InvalidDataException>(() => YscParser.Parse(new byte[32]));
        Assert.Contains("truncated", exception.Message);
    }

    [Fact]
    public void Parse_RejectsSectionOutsidePayload()
    {
        var payload = CreatePayload();
        WritePointer(payload, 16, 0x500007FF);

        var exception = Assert.Throws<InvalidDataException>(() => YscParser.Parse(payload));
        Assert.Contains("code page table", exception.Message);
    }

    [Fact]
    public void Parse_RejectsUnterminatedStringTableEntry()
    {
        var payload = CreatePayload();
        payload[0x300 + 11] = (byte)'!';

        var exception = Assert.Throws<InvalidDataException>(() => YscParser.Parse(payload));
        Assert.Contains("not null terminated", exception.Message);
    }

    [Fact]
    public void InstructionDecoder_HandlesVariableWidthInstructions()
    {
        byte[] code = [45, 1, 2, 0, 3, (byte)'f', (byte)'o', (byte)'o', 101, 1, 5, 0, 0, 0, 2, 0, 46, 1, 0];

        var instructions = YscInstructionDecoder.Decode(code);

        Assert.Equal(["ENTER", "SWITCH", "LEAVE"], instructions.Select(x => x.Opcode));
        Assert.Equal([0, 8, 16], instructions.Select(x => x.Offset));
    }

    [Fact]
    public void InstructionDecoder_RejectsUnknownAndTruncatedOpcodes()
    {
        Assert.Throws<InvalidDataException>(() => YscInstructionDecoder.Decode([0xFF]));
        Assert.Throws<InvalidDataException>(() => YscInstructionDecoder.Decode([40, 1, 2]));
    }

    [Fact]
    public void Joaat_MatchesKnownGtaVector()
    {
        Assert.Equal(0xB779A091u, YscPaintingAnalysis.Joaat("adder"));
        Assert.Equal(YscPaintingAnalysis.Joaat("adder"), YscPaintingAnalysis.Joaat("ADDER"));
    }

    private static byte[] CreatePayload()
    {
        var data = new byte[0x400];
        WritePointer(data, 8, 0x500000F0); // page map (retained metadata)
        WritePointer(data, 16, 0x50000100); // code page pointer table
        WriteU32(data, 24, 0x11223344);
        WriteI32(data, 28, 7);
        WriteI32(data, 32, 2);
        WriteI32(data, 36, 1);
        WriteI32(data, 40, 1);
        WriteI32(data, 44, 1);
        WritePointer(data, 48, 0x50000198);
        WritePointer(data, 56, 0x500001A0);
        WritePointer(data, 64, 0x50000190);
        WriteU32(data, 88, 0x55667788);
        WritePointer(data, 96, 0x50000180);
        WritePointer(data, 104, 0x50000108);
        WriteI32(data, 112, 12);
        WritePointer(data, 0x100, 0x50000200);
        WritePointer(data, 0x108, 0x50000300);
        Encoding.UTF8.GetBytes("kortz_test\0").CopyTo(data, 0x180);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(0x190), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(0x198), 42);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(0x1A0), 0x3F800000);
        new byte[] { 37, 42, 40, 0x91, 0xA0, 0x79, 0xB7 }.CopyTo(data, 0x200);
        Encoding.UTF8.GetBytes("Hello\0World\0").CopyTo(data, 0x300);
        return data;
    }

    private static void WritePointer(byte[] data, int offset, ulong pointer) => BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset), pointer);
    private static void WriteU32(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
    private static void WriteI32(byte[] data, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), value);
}
