namespace GtaHeistPlanner.DataExtractor;

public enum ExtractorInputFormat
{
    ScenarioRegionXml,
    Rsc7Resource,
    Unknown,
}

public static class InputFormatDetector
{
    private static ReadOnlySpan<byte> Rsc7Magic => "RSC7"u8;

    public static ExtractorInputFormat Detect(ReadOnlySpan<byte> data)
    {
        if (data.Length >= Rsc7Magic.Length && data[..Rsc7Magic.Length].SequenceEqual(Rsc7Magic))
            return ExtractorInputFormat.Rsc7Resource;

        var index = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
        while (index < data.Length && data[index] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            index++;

        return index < data.Length && data[index] == (byte)'<'
            ? ExtractorInputFormat.ScenarioRegionXml
            : ExtractorInputFormat.Unknown;
    }

    public static ExtractorInputFormat DetectFile(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> prefix = stackalloc byte[256];
        var count = stream.Read(prefix);
        return Detect(prefix[..count]);
    }
}
