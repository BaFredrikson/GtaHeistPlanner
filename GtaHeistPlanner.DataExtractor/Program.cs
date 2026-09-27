using System.Text.Json;
using GtaHeistPlanner.DataExtractor;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: GtaHeistPlanner.DataExtractor <scenario.ymt.xml|resource.ysc> [output.json]");
    return 2;
}

try
{
    var sourcePath = Path.GetFullPath(args[0]);
    var format = InputFormatDetector.DetectFile(sourcePath);
    if (format == ExtractorInputFormat.Rsc7Resource)
    {
        var resource = File.ReadAllBytes(sourcePath);
        var header = Rsc7ResourceHeader.Parse(resource);
        Console.WriteLine($"RSC7 resource detected: version={header.Version}, systemFlags=0x{header.SystemFlags:X8}, graphicsFlags=0x{header.GraphicsFlags:X8}, size={resource.Length:N0} bytes.");
        var decoded = Rsc7ResourceProcessor.Decode(sourcePath, resource, new DeflateRsc7ResourceDecoder());
        var payload = DecodedYscPayloadInspector.Inspect(decoded);
        Console.WriteLine($"Container decoded successfully: raw-DEFLATE script payload, {payload.Size:N0} bytes.");
        Console.WriteLine($"Structural check passed: found {payload.ResourcePointerCount} GTA system/graphics resource pointer(s) in the root header.");
        var script = YscParser.Parse(decoded);
        var analysis = new YscAnalysisResult(
            new YscInputInfo(sourcePath, "RSC7", resource.Length, decoded.Length, header.Version),
            script,
            YscPaintingAnalysis.Find(script));
        var yscOutputPath = args.Length == 2
            ? Path.GetFullPath(args[1])
            : Path.Combine(Directory.GetCurrentDirectory(), "analysis", "kortz",
                $"{Path.GetFileName(sourcePath)}.analysis.json");
        var textPath = Path.ChangeExtension(yscOutputPath, ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(yscOutputPath)!);
        File.WriteAllText(yscOutputPath, JsonSerializer.Serialize(analysis, JsonOptions.Default));
        File.WriteAllText(textPath, YscReportWriter.CreateText(analysis));
        YscReportWriter.WriteConsole(analysis, yscOutputPath, textPath);
        return 0;
    }
    if (format != ExtractorInputFormat.ScenarioRegionXml)
        throw new InvalidDataException("Unsupported input format. Expected CScenarioPointRegion XML or an RSC7 resource.");

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
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException or InvalidDataException or Rsc7DecodingUnavailableException)
{
    Console.Error.WriteLine($"Extraction failed: {exception.Message}");
    return 1;
}
