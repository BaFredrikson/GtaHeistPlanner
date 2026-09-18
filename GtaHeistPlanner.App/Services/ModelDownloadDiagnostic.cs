using System.Net;
using System.Text;

namespace GtaHeistPlanner.App.Services;

public sealed record ModelDownloadDiagnostic
{
    public required string ModelId { get; init; }
    public required Uri SourceUrl { get; init; }
    public HttpStatusCode? HttpStatusCode { get; init; }
    public string? FinalHost { get; init; }
    public Uri? FinalResponseUrl { get; init; }
    public long? ContentLength { get; init; }
    public long BytesDownloaded { get; init; }
    public required string TemporaryPath { get; init; }
    public required string DestinationPath { get; init; }
    public bool DestinationDirectoryCreated { get; init; }
    public string ValidationResult { get; init; } = "Not run";
    public long? ExpectedSize { get; init; }
    public long? ActualSize { get; init; }
    public string? ExpectedSha256 { get; init; }
    public string? ActualSha256 { get; init; }
    public bool Cancelled { get; init; }
    public Exception? Exception { get; init; }

    public override string ToString()
    {
        var text = new StringBuilder()
            .AppendLine($"Model: {ModelId}")
            .AppendLine($"Source: {Sanitize(SourceUrl)}")
            .AppendLine($"HTTP: {(HttpStatusCode is null ? "not received" : $"{(int)HttpStatusCode} {HttpStatusCode}")}")
            .AppendLine($"Final host: {FinalHost ?? "unknown"}")
            .AppendLine($"Final response URL: {(FinalResponseUrl is null ? "unknown" : Sanitize(FinalResponseUrl))}")
            .AppendLine($"Content-Length: {ContentLength?.ToString() ?? "not supplied"}")
            .AppendLine($"Bytes downloaded: {BytesDownloaded}")
            .AppendLine($"Temporary file: {TemporaryPath}")
            .AppendLine($"Destination: {DestinationPath}")
            .AppendLine($"Directory creation: {(DestinationDirectoryCreated ? "succeeded" : "not completed")}")
            .AppendLine($"Validation: {ValidationResult}")
            .AppendLine($"Expected size/hash: {ExpectedSize?.ToString() ?? "none"} / {ExpectedSha256 ?? "none"}")
            .AppendLine($"Actual size/hash: {ActualSize?.ToString() ?? "unknown"} / {ActualSha256 ?? "unknown"}")
            .AppendLine($"Cancelled: {Cancelled}");
        for (var error = Exception; error is not null; error = error.InnerException)
            text.AppendLine($"{error.GetType().FullName}: {error.Message}");
        return text.ToString().TrimEnd();
    }

    public static string Sanitize(Uri uri) => uri.GetLeftPart(UriPartial.Path);
}

public sealed class ModelValidationException(string message) : IOException(message);

public sealed record ModelDownloadProgress(long BytesReceived, long? TotalBytes)
{
    public double? Fraction => TotalBytes is > 0 ? Math.Min(1, BytesReceived / (double)TotalBytes.Value) : null;
}
