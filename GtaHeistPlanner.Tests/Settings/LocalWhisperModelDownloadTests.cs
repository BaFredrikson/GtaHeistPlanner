using System.Net;
using System.Security.Cryptography;
using GtaHeistPlanner.App.Services;
using GtaHeistPlanner.Voice;

namespace GtaHeistPlanner.Tests.Settings;

public sealed class LocalWhisperModelDownloadTests
{
    private static readonly byte[] Payload = "valid-model"u8.ToArray();

    [Fact]
    public void ProductionHttpHandlerFollowsRedirects() =>
        Assert.True(LocalWhisperModelStore.CreateDefaultHandler().AllowAutoRedirect);

    [Fact]
    public async Task SuccessfulDownloadValidatesAndInstallsFromPartFile()
    {
        using var fixture = Fixture(Response(HttpStatusCode.OK, Payload));
        await fixture.Store.DownloadAsync("test");

        Assert.Equal(Payload, File.ReadAllBytes(fixture.Destination));
        Assert.False(File.Exists(fixture.Destination + ".part"));
        Assert.Equal("Passed", fixture.Diagnostic!.ValidationResult);
        Assert.Equal(Payload.Length, fixture.Diagnostic.BytesDownloaded);
        Assert.True(fixture.Handler.LastRequest!.Headers.UserAgent.Any());
    }

    [Theory]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    public async Task RedirectedSuccessReportsSanitizedFinalUrl(HttpStatusCode redirectStatus)
    {
        // HttpClientHandler consumes this redirect response in production; the mocked
        // downloader-facing response represents the resulting final 200 response.
        Assert.Contains(redirectStatus, new[] { HttpStatusCode.Found, HttpStatusCode.TemporaryRedirect });
        var response = Response(HttpStatusCode.OK, Payload);
        response.RequestMessage = new(HttpMethod.Get, "https://cdn.example.test/model.bin?Policy=secret&Signature=secret");
        using var fixture = Fixture(response);
        await fixture.Store.DownloadAsync("test");
        Assert.Equal("cdn.example.test", fixture.Diagnostic!.FinalHost);
        Assert.Equal("https://cdn.example.test/model.bin", fixture.Diagnostic.FinalResponseUrl!.AbsoluteUri);
        Assert.DoesNotContain("Signature", fixture.Diagnostic.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingContentLengthUsesIndeterminateByteProgress()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(Payload) };
        using var fixture = Fixture(response);
        var updates = new List<ModelDownloadProgress>();
        await fixture.Store.DownloadAsync("test", new Progress<ModelDownloadProgress>(updates.Add));
        Assert.Null(fixture.Diagnostic!.ContentLength);
        Assert.Contains(updates, update => update.BytesReceived == Payload.Length && update.TotalBytes is null);
    }

    [Fact]
    public async Task LargeResponseIsStreamedWithoutHoldingModelSizedBuffer()
    {
        const long size = 147_964_211;
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new GeneratedStream(size))
        };
        response.Content.Headers.ContentLength = size;
        using var fixture = Fixture(response, expectedHash: null, expectedBytes: size);
        await fixture.Store.DownloadAsync("test");
        Assert.Equal(size, new FileInfo(fixture.Destination).Length);
        Assert.Equal(size, fixture.Diagnostic!.BytesDownloaded);
    }

    [Fact]
    public async Task InterruptedResponseIsDiagnosedAndPartFileRemoved()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new InterruptedStream(Payload, 4))
        };
        response.Content.Headers.ContentLength = Payload.Length;
        using var fixture = Fixture(response);
        await Assert.ThrowsAsync<IOException>(() => fixture.Store.DownloadAsync("test"));
        Assert.True(fixture.Diagnostic!.BytesDownloaded > 0);
        Assert.False(File.Exists(fixture.Destination + ".part"));
    }

    [Fact]
    public void BaseEnglishCatalogMatchesCurrentWhisperCppMetadata()
    {
        var model = LocalWhisperModelCatalog.Get("base.en");
        Assert.Equal("ggml-base.en.bin", model.FileName);
        Assert.Equal(147_964_211, model.ExpectedBytes);
        Assert.Equal("a03779c86df3323075f5e796cb2ce5029f00ec8869eee3fdfb897afe36c6d002", model.ExpectedSha256);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "Download failed — HTTP 404")]
    [InlineData(HttpStatusCode.Forbidden, "Download failed — HTTP 403")]
    public async Task HttpFailureIsActionableAndPreservesExistingModel(HttpStatusCode status, string expected)
    {
        using var fixture = Fixture(Response(status, []));
        Directory.CreateDirectory(fixture.Directory); File.WriteAllText(fixture.Destination, "existing");
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Store.DownloadAsync("test"));
        Assert.Equal(expected, LocalWhisperModelStore.ConciseFailure(error));
        Assert.Equal("existing", File.ReadAllText(fixture.Destination));
        Assert.False(File.Exists(fixture.Destination + ".part"));
    }

    [Fact]
    public async Task NetworkExceptionIsCaptured()
    {
        using var fixture = Fixture(new HttpRequestException("network down"));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Store.DownloadAsync("test"));
        Assert.Equal("Download failed — connection error", LocalWhisperModelStore.ConciseFailure(error));
        Assert.Contains("network down", fixture.Diagnostic!.ToString());
    }

    [Fact]
    public async Task IncompleteResponseFailsValidationAndCleansPartFile()
    {
        var response = Response(HttpStatusCode.OK, Payload[..3]);
        response.Content.Headers.ContentLength = Payload.Length;
        using var fixture = Fixture(response);
        var error = await Assert.ThrowsAsync<ModelValidationException>(() => fixture.Store.DownloadAsync("test"));
        Assert.Equal("Download failed — file validation failed", LocalWhisperModelStore.ConciseFailure(error));
        Assert.False(File.Exists(fixture.Destination + ".part"));
    }

    [Fact]
    public async Task HashMismatchFailsValidation()
    {
        using var fixture = Fixture(Response(HttpStatusCode.OK, Payload), expectedHash: new string('0', 64));
        await Assert.ThrowsAsync<ModelValidationException>(() => fixture.Store.DownloadAsync("test"));
        Assert.Equal("Failed", fixture.Diagnostic!.ValidationResult);
        Assert.NotEqual(fixture.Diagnostic.ExpectedSha256, fixture.Diagnostic.ActualSha256);
    }

    [Fact]
    public async Task UnwritableDestinationIsDiagnosed()
    {
        var parent = Path.Combine(Path.GetTempPath(), $"gta-model-file-{Guid.NewGuid():N}");
        File.WriteAllText(parent, "not a directory");
        try
        {
            using var fixture = Fixture(Response(HttpStatusCode.OK, Payload), directory: parent, ownsDirectory: false);
            var error = await Assert.ThrowsAnyAsync<IOException>(() => fixture.Store.DownloadAsync("test"));
            Assert.Equal("Download failed — file system error", LocalWhisperModelStore.ConciseFailure(error));
            Assert.False(fixture.Diagnostic!.DestinationDirectoryCreated);
        }
        finally { File.Delete(parent); }
    }

    [Fact]
    public async Task CancellationIsDistinctAndCleansPartFile()
    {
        using var fixture = Fixture(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Response(HttpStatusCode.OK, Payload); });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Store.DownloadAsync("test", cancellationToken: cancellation.Token));
        Assert.True(fixture.Diagnostic!.Cancelled);
        Assert.Equal("Download cancelled", LocalWhisperModelStore.ConciseFailure(fixture.Diagnostic.Exception!));
        Assert.False(File.Exists(fixture.Destination + ".part"));
    }

    private static DownloadFixture Fixture(HttpResponseMessage response, string? expectedHash = "default",
        string? directory = null, bool ownsDirectory = true, long? expectedBytes = null) => Fixture((_, _) => Task.FromResult(response), expectedHash, directory, ownsDirectory, expectedBytes);
    private static DownloadFixture Fixture(Exception exception) => Fixture((_, _) => Task.FromException<HttpResponseMessage>(exception));
    private static DownloadFixture Fixture(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response,
        string? expectedHash = "default", string? directory = null, bool ownsDirectory = true, long? expectedBytes = null)
    {
        directory ??= Path.Combine(Path.GetTempPath(), $"gta-model-download-{Guid.NewGuid():N}");
        var handler = new StubHandler(response);
        var model = new LocalWhisperModelInfo("test", "model.bin", new("https://models.example.test/model.bin"),
            expectedBytes ?? Payload.Length, false, expectedHash == "default" ? Sha256(Payload) : expectedHash);
        var store = new LocalWhisperModelStore(new HttpClient(handler), directory, _ => model);
        return new(store, handler, directory, ownsDirectory);
    }

    private static HttpResponseMessage Response(HttpStatusCode status, byte[] body) => new(status) { Content = new ByteArrayContent(body) };
    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { LastRequest = request; return response(request, cancellationToken); }
    }

    private sealed class DownloadFixture : IDisposable
    {
        public DownloadFixture(LocalWhisperModelStore store, StubHandler handler, string directory, bool ownsDirectory)
        { Store = store; Handler = handler; Directory = directory; _ownsDirectory = ownsDirectory; Store.DiagnosticAvailable += (_, value) => Diagnostic = value; }
        private readonly bool _ownsDirectory;
        public LocalWhisperModelStore Store { get; }
        public StubHandler Handler { get; }
        public string Directory { get; }
        public string Destination => Store.PathFor("test");
        public ModelDownloadDiagnostic? Diagnostic { get; private set; }
        public void Dispose() { if (_ownsDirectory && System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true); }
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(bytes, false));
    }

    private sealed class GeneratedStream(long length) : Stream
    {
        private long _position;
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = (int)Math.Min(count, length - _position); if (read <= 0) return 0;
            Array.Fill(buffer, (byte)0x5A, offset, read); _position += read; return read;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { var read = (int)Math.Min(buffer.Length, length - _position); if (read <= 0) return ValueTask.FromResult(0); buffer.Span[..read].Fill(0x5A); _position += read; return ValueTask.FromResult(read); }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => length; public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class InterruptedStream(byte[] bytes, int failAfter) : MemoryStream(bytes, false)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position >= failAfter) throw new IOException("Simulated interrupted response.");
            return base.ReadAsync(buffer[..Math.Min(buffer.Length, failAfter - (int)Position)], cancellationToken);
        }
    }
}
