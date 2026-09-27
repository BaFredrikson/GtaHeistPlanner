namespace GtaHeistPlanner.DataExtractor;

public sealed record YscHeader(
    ulong PageBase,
    ulong PageMapPointer,
    ulong CodeBlocksPointer,
    uint GlobalsSignature,
    int CodeSize,
    int ParameterCount,
    int StaticCount,
    int GlobalCount,
    int NativeCount,
    ulong StaticsPointer,
    ulong GlobalsPointer,
    ulong NativesPointer,
    uint ScriptNameHash,
    ulong ScriptNamePointer,
    ulong StringBlocksPointer,
    int StringSize,
    ulong Unknown1,
    ulong Unknown2,
    uint Unknown3,
    ulong Unknown4,
    ulong Unknown5,
    ulong Unknown6);

public sealed record YscPage(int Index, int LogicalOffset, int Length, ulong VirtualPointer, int FileOffset);

public sealed record YscStringEntry(int Index, string Text, int Length, int PageIndex, int PageOffset);

public sealed record YscNativeEntry(int Index, string RawValue, string DecodedHash);

public sealed record YscValueEntry(
    int Index, string Hex, long SignedInteger, ulong UnsignedInteger, string DoubleValue, string LowFloatValue);

public sealed record YscInstruction(
    int Offset, byte RawOpcode, string Opcode, string OperandHex, IReadOnlyList<ulong> ImmediateValues);

public sealed record YscCodeSection(int Length, IReadOnlyList<YscPage> Pages);

public sealed record YscStringTable(int Length, IReadOnlyList<YscPage> Pages, IReadOnlyList<YscStringEntry> Entries);

public sealed record YscScript(
    string Name,
    YscHeader Header,
    YscCodeSection Code,
    YscStringTable StringTable,
    IReadOnlyList<YscNativeEntry> Natives,
    IReadOnlyList<YscValueEntry> Statics,
    IReadOnlyList<YscValueEntry> Globals,
    IReadOnlyList<YscInstruction> Instructions,
    IReadOnlyList<string> Diagnostics);

public sealed record PaintingMatch(
    string Name,
    string JoaatHash,
    IReadOnlyList<int> StringOffsets,
    IReadOnlyList<int> StaticIndices,
    IReadOnlyList<int> GlobalIndices,
    IReadOnlyList<int> InstructionOffsets);

public sealed record YscInputInfo(string Path, string Container, int PackedSize, int DecodedSize, uint ResourceVersion);

public sealed record YscAnalysisResult(
    YscInputInfo Input,
    YscScript Script,
    IReadOnlyList<PaintingMatch> PaintingMatches);
