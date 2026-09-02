using System.Text.Json;
using GtaHeistPlanner.App.Models;

namespace GtaHeistPlanner.App.Services;

public static class SecurityAnalysisLoader
{
    public static SecurityAnalysis LoadKortz()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "kortz", "kortz_museum.analysis.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("Kortz analysis data was not copied to the application output.", path);

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<SecurityAnalysis>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidDataException($"Analysis file '{path}' contains no data.");
    }
}
