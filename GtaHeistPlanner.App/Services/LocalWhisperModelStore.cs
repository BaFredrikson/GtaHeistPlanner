using System.Net;
using System.Security.Cryptography;
using GtaHeistPlanner.Voice;

namespace GtaHeistPlanner.App.Services;

public sealed class LocalWhisperModelStore
{
    private readonly HttpClient _http;
    private readonly Func<string, LocalWhisperModelInfo> _modelResolver;

    public LocalWhisperModelStore(HttpClient? httpClient = null, string? directory = null,
        Func<string, LocalWhisperModelInfo>? modelResolver = null)
    {
        _http = httpClient ?? new HttpClient(CreateDefaultHandler());
        _modelResolver = modelResolver ?? LocalWhisperModelCatalog.Get;
        DirectoryPath = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GtaHeistPlanner", "models");
    }

    public event EventHandler<ModelDownloadDiagnostic>? DiagnosticAvailable;
    public static HttpClientHandler CreateDefaultHandler() => new() { AllowAutoRedirect = true };
    public string DirectoryPath { get; }
    public string PathFor(string modelId) => Path.Combine(DirectoryPath, _modelResolver(modelId).FileName);
    public bool IsInstalled(string modelId) => File.Exists(PathFor(modelId));

    public async Task DownloadAsync(string modelId, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var model = _modelResolver(modelId);
        var destination = PathFor(modelId);
        var temporary = destination + ".part";
        HttpStatusCode? status = null;
        string? finalHost = null;
        Uri? finalResponseUrl = null;
        long? contentLength = null;
        long bytesDownloaded = 0;
        var directoryCreated = false;
        string validation = "Not run";
        string? actualHash = null;
        long? actualSize = null;
        Exception? failure = null;
        var cancelled = false;

        try
        {
            if (model.DownloadUri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Whisper models must be downloaded over HTTPS.");
            Directory.CreateDirectory(DirectoryPath);
            directoryCreated = Directory.Exists(DirectoryPath);

            using var request = new HttpRequestMessage(HttpMethod.Get, model.DownloadUri);
            request.Headers.UserAgent.ParseAdd("GtaHeistPlanner/1.0 (+https://github.com/ggml-org/whisper.cpp)");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            status = response.StatusCode;
            finalResponseUrl = response.RequestMessage?.RequestUri;
            finalHost = finalResponseUrl?.Host;
            contentLength = response.Content.Headers.ContentLength;
            response.EnsureSuccessStatusCode();
            progress?.Report(new(0, contentLength));

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                hash.AppendData(buffer, 0, read);
                bytesDownloaded += read;
                progress?.Report(new(bytesDownloaded, contentLength));
            }
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            await output.DisposeAsync().ConfigureAwait(false);
            actualSize = bytesDownloaded;
            actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();

            if (contentLength is { } suppliedLength && suppliedLength != bytesDownloaded)
                throw new ModelValidationException($"Content-Length was {suppliedLength:N0} bytes but only {bytesDownloaded:N0} bytes were received.");
            if (model.ExpectedBytes > 0 && bytesDownloaded != model.ExpectedBytes)
                throw new ModelValidationException($"Downloaded model size was {bytesDownloaded:N0} bytes; expected {model.ExpectedBytes:N0}.");
            if (!string.IsNullOrWhiteSpace(model.ExpectedSha256) &&
                !actualHash.Equals(model.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new ModelValidationException("Downloaded model SHA-256 did not match the configured hash.");
            validation = "Passed";

            File.Move(temporary, destination, true);
            progress?.Report(new(bytesDownloaded, contentLength));
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            cancelled = true; failure = exception; validation = "Cancelled"; throw;
        }
        catch (Exception exception)
        {
            failure = exception;
            if (exception is ModelValidationException) validation = "Failed";
            throw;
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception cleanupException) { failure ??= cleanupException; }
            DiagnosticAvailable?.Invoke(this, new ModelDownloadDiagnostic
            {
                ModelId = modelId, SourceUrl = new Uri(ModelDownloadDiagnostic.Sanitize(model.DownloadUri)),
                HttpStatusCode = status, FinalHost = finalHost,
                FinalResponseUrl = finalResponseUrl is null ? null : new Uri(ModelDownloadDiagnostic.Sanitize(finalResponseUrl)),
                ContentLength = contentLength,
                BytesDownloaded = bytesDownloaded, TemporaryPath = temporary, DestinationPath = destination,
                DestinationDirectoryCreated = directoryCreated, ValidationResult = validation,
                ExpectedSize = model.ExpectedBytes, ActualSize = actualSize,
                ExpectedSha256 = model.ExpectedSha256, ActualSha256 = actualHash,
                Cancelled = cancelled, Exception = failure,
            });
        }
    }

    public static string ConciseFailure(Exception exception) => exception switch
    {
        OperationCanceledException => "Download cancelled",
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } => "Download failed — HTTP 404",
        HttpRequestException { StatusCode: HttpStatusCode.Forbidden } => "Download failed — HTTP 403",
        HttpRequestException { StatusCode: { } status } => $"Download failed — HTTP {(int)status}",
        UnauthorizedAccessException => "Download failed — access denied",
        ModelValidationException => "Download failed — file validation failed",
        HttpRequestException => "Download failed — connection error",
        IOException => "Download failed — file system error",
        _ => "Download failed",
    };

    public void Remove(string modelId) { var path = PathFor(modelId); if (File.Exists(path)) File.Delete(path); }
}
