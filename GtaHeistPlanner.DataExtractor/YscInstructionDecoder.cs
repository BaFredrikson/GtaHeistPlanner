using System.Buffers.Binary;

namespace GtaHeistPlanner.DataExtractor;

/// <summary>Flat PC ScriptVM decoder based on the opcode ordering and operand widths in ysc-utils.</summary>
public static class YscInstructionDecoder
{
    private static readonly string[] Names =
    [
        "NOP", "IADD", "ISUB", "IMUL", "IDIV", "IMOD", "INOT", "INEG", "IEQ", "INE", "IGT", "IGE", "ILT", "ILE",
        "FADD", "FSUB", "FMUL", "FDIV", "FMOD", "FNEG", "FEQ", "FNE", "FGT", "FGE", "FLT", "FLE",
        "VADD", "VSUB", "VMUL", "VDIV", "VNEG", "IAND", "IOR", "IXOR", "I2F", "F2I", "F2V",
        "PUSH_CONST_U8", "PUSH_CONST_U8_U8", "PUSH_CONST_U8_U8_U8", "PUSH_CONST_U32", "PUSH_CONST_F", "DUP", "DROP",
        "NATIVE", "ENTER", "LEAVE", "LOAD", "STORE", "STORE_REV", "LOAD_N", "STORE_N",
        "ARRAY_U8", "ARRAY_U8_LOAD", "ARRAY_U8_STORE", "LOCAL_U8", "LOCAL_U8_LOAD", "LOCAL_U8_STORE",
        "STATIC_U8", "STATIC_U8_LOAD", "STATIC_U8_STORE", "IADD_U8", "IMUL_U8", "IOFFSET",
        "IOFFSET_U8", "IOFFSET_U8_LOAD", "IOFFSET_U8_STORE", "PUSH_CONST_S16", "IADD_S16", "IMUL_S16",
        "IOFFSET_S16", "IOFFSET_S16_LOAD", "IOFFSET_S16_STORE", "ARRAY_U16", "ARRAY_U16_LOAD", "ARRAY_U16_STORE",
        "LOCAL_U16", "LOCAL_U16_LOAD", "LOCAL_U16_STORE", "STATIC_U16", "STATIC_U16_LOAD", "STATIC_U16_STORE",
        "GLOBAL_U16", "GLOBAL_U16_LOAD", "GLOBAL_U16_STORE", "J", "JZ", "IEQ_JZ", "INE_JZ", "IGT_JZ", "IGE_JZ", "ILT_JZ", "ILE_JZ",
        "CALL", "LOCAL_U24", "LOCAL_U24_LOAD", "LOCAL_U24_STORE", "GLOBAL_U24", "GLOBAL_U24_LOAD", "GLOBAL_U24_STORE", "PUSH_CONST_U24",
        "SWITCH", "STRING", "STRINGHASH", "TEXT_LABEL_ASSIGN_STRING", "TEXT_LABEL_ASSIGN_INT", "TEXT_LABEL_APPEND_STRING",
        "TEXT_LABEL_APPEND_INT", "TEXT_LABEL_COPY", "CATCH", "THROW", "CALL_INDIRECT", "PUSH_CONST_M1",
        "PUSH_CONST_0", "PUSH_CONST_1", "PUSH_CONST_2", "PUSH_CONST_3", "PUSH_CONST_4", "PUSH_CONST_5", "PUSH_CONST_6", "PUSH_CONST_7",
        "PUSH_CONST_FM1", "PUSH_CONST_F0", "PUSH_CONST_F1", "PUSH_CONST_F2", "PUSH_CONST_F3", "PUSH_CONST_F4", "PUSH_CONST_F5", "PUSH_CONST_F6", "PUSH_CONST_F7",
        "IS_BIT_SET"
    ];

    public static IReadOnlyList<YscInstruction> Decode(ReadOnlySpan<byte> code)
    {
        var result = new List<YscInstruction>();
        var offset = 0;
        while (offset < code.Length)
        {
            var start = offset;
            var raw = code[offset++];
            if (raw >= Names.Length)
                throw new InvalidDataException($"Unknown YSC opcode 0x{raw:X2} at code offset {start}.");
            var name = Names[raw];
            var operandLength = GetOperandLength(raw, code, offset, start);
            if (offset > code.Length - operandLength)
                throw new InvalidDataException($"YSC instruction {name} at code offset {start} is truncated.");
            var operands = code.Slice(offset, operandLength);
            result.Add(new YscInstruction(start, raw, name, Convert.ToHexString(operands), ReadImmediates(name, operands)));
            offset += operandLength;
        }
        return result;
    }

    private static int GetOperandLength(byte opcode, ReadOnlySpan<byte> code, int operandOffset, int instructionOffset)
    {
        if (opcode == 45) // ENTER: args:u8, locals:u16, function-name-byte-count:u8, name bytes
        {
            if (operandOffset > code.Length - 4)
                throw new InvalidDataException($"YSC ENTER at code offset {instructionOffset} is truncated.");
            return checked(4 + code[operandOffset + 3]);
        }
        if (opcode == 101) // SWITCH: count:u8 followed by count * (value:u32, jump:u16)
        {
            if (operandOffset >= code.Length)
                throw new InvalidDataException($"YSC SWITCH at code offset {instructionOffset} is truncated.");
            return checked(1 + code[operandOffset] * 6);
        }

        return opcode switch
        {
            37 => 1, 38 => 2, 39 => 3, 40 or 41 => 4, 44 => 3, 46 => 2,
            >= 52 and <= 62 => 1,
            >= 64 and <= 66 => 1,
            >= 67 and <= 92 => 2,
            >= 93 and <= 100 => 3,
            >= 104 and <= 107 => 1,
            _ => 0,
        };
    }

    private static IReadOnlyList<ulong> ReadImmediates(string name, ReadOnlySpan<byte> operands)
    {
        if (name == "PUSH_CONST_U8") return [operands[0]];
        if (name == "PUSH_CONST_U32") return [BinaryPrimitives.ReadUInt32LittleEndian(operands)];
        if (name == "PUSH_CONST_S16") return [unchecked((ulong)BinaryPrimitives.ReadInt16LittleEndian(operands))];
        if (name == "PUSH_CONST_U24") return [(ulong)(operands[0] | operands[1] << 8 | operands[2] << 16)];
        if (name.StartsWith("PUSH_CONST_", StringComparison.Ordinal) && name.Length == "PUSH_CONST_0".Length && char.IsDigit(name[^1]))
            return [(ulong)(name[^1] - '0')];
        return [];
    }
}
