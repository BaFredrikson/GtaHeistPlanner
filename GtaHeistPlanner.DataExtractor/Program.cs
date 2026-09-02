using System.Text.Json;
using GtaHeistPlanner.DataExtractor;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: GtaHeistPlanner.DataExtractor <scenario.ymt.xml> [output.json]");
    return 2;
}

try
{
    var sourcePath = Path.GetFullPath(args[0]);
    var result = ScenarioRegionExtractor.Extract(sourcePath);
    ConsoleReportWriter.Write(result);

    var outputPath = args.Length == 2
        ? Path.GetFullPath(args[1])
        : Path.Combine(Directory.GetCurrentDirectory(), "analysis", "kortz",
            $"{ScenarioRegionExtractor.GetSourceBaseName(sourcePath)}.analysis.json");

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllText(outputPath, JsonSerializer.Serialize(result, JsonOptions.Default));
    Console.WriteLine($"JSON written to: {outputPath}");
    return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException or InvalidDataException)
{
    Console.Error.WriteLine($"Extraction failed: {exception.Message}");
    return 1;
}
