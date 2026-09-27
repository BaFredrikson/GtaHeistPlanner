using System.Text;

namespace GtaHeistPlanner.DataExtractor;

public static class YscReportWriter
{
    public static void WriteConsole(YscAnalysisResult result, string jsonPath, string textPath)
    {
        var script = result.Script;
        Console.WriteLine($"Script: {script.Name} (hash 0x{script.Header.ScriptNameHash:X8})");
        Console.WriteLine($"Code: {script.Code.Length:N0} bytes in {script.Code.Pages.Count} page(s); {script.Instructions.Count:N0} instructions.");
        Console.WriteLine($"Strings: {script.StringTable.Entries.Count:N0} non-empty entries, {script.StringTable.Length:N0} bytes in {script.StringTable.Pages.Count} page(s).");
        Console.WriteLine($"Natives: {script.Natives.Count:N0}; statics: {script.Statics.Count:N0}; globals: {script.Globals.Count:N0}; parameters: {script.Header.ParameterCount:N0}.");
        Console.WriteLine($"Painting plaintext matches: {result.PaintingMatches.Sum(x => x.StringOffsets.Count)}; hash correlations: {result.PaintingMatches.Sum(x => x.StaticIndices.Count + x.GlobalIndices.Count + x.InstructionOffsets.Count)}.");
        Console.WriteLine($"JSON written to: {jsonPath}");
        Console.WriteLine($"Text report written to: {textPath}");
    }

    public static string CreateText(YscAnalysisResult result)
    {
        var s = result.Script;
        var text = new StringBuilder();
        text.AppendLine("SCRIPT HEADER");
        text.AppendLine($"Name: {s.Name}");
        text.AppendLine($"Name hash: 0x{s.Header.ScriptNameHash:X8}");
        text.AppendLine($"Code: {s.Code.Length} bytes / {s.Code.Pages.Count} pages");
        text.AppendLine($"Strings: {s.StringTable.Length} bytes / {s.StringTable.Pages.Count} pages / {s.StringTable.Entries.Count} non-empty entries");
        text.AppendLine($"Parameters: {s.Header.ParameterCount}; statics: {s.Statics.Count}; globals: {s.Globals.Count}; natives: {s.Natives.Count}");
        text.AppendLine().AppendLine("PAINTING MATCHES");
        foreach (var match in result.PaintingMatches.Where(x => x.StringOffsets.Count + x.StaticIndices.Count + x.GlobalIndices.Count + x.InstructionOffsets.Count > 0))
            text.AppendLine($"{match.Name} {match.JoaatHash}: strings=[{string.Join(',', match.StringOffsets)}] statics=[{string.Join(',', match.StaticIndices)}] globals=[{string.Join(',', match.GlobalIndices)}] code=[{string.Join(',', match.InstructionOffsets)}]");
        text.AppendLine().AppendLine("STRINGS");
        foreach (var entry in s.StringTable.Entries)
            text.AppendLine($"0x{entry.Index:X6} p{entry.PageIndex}+0x{entry.PageOffset:X4} {entry.Text.Replace("\r", "\\r").Replace("\n", "\\n")}");
        text.AppendLine().AppendLine("NATIVES");
        foreach (var entry in s.Natives) text.AppendLine($"{entry.Index:D4} raw={entry.RawValue} hash={entry.DecodedHash}");
        text.AppendLine().AppendLine("STATICS");
        foreach (var entry in s.Statics) text.AppendLine($"{entry.Index:D5} {entry.Hex} i64={entry.SignedInteger} f32={entry.LowFloatValue}");
        text.AppendLine().AppendLine("GLOBALS");
        foreach (var entry in s.Globals) text.AppendLine($"{entry.Index:D5} {entry.Hex} i64={entry.SignedInteger} f32={entry.LowFloatValue}");
        text.AppendLine().AppendLine("CODE / INSTRUCTIONS");
        foreach (var instruction in s.Instructions)
            text.AppendLine($"{instruction.Offset:X6}: {instruction.Opcode,-28} {instruction.OperandHex}");
        return text.ToString();
    }
}
