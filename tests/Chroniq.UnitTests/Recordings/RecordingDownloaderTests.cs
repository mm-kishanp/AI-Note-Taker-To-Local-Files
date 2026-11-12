using System.Net;
using System.Net.Http.Headers;
using Chroniq.Application.Export;
using Chroniq.Domain.Enums;
using Chroniq.Infrastructure.Recordings;

namespace Chroniq.UnitTests.Recordings;

public sealed class RecordingDownloaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "chroniq-rec-" + Guid.NewGuid().ToString("N"));
    private static readonly Uri Link = new("https://files.example.com/abc/call.mp3?sig=secret-token");

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Media(string type, byte[] body)
    {
        var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
        r.Content.Headers.ContentType = new MediaTypeHeaderValue(type);
        return r;
    }

    private (RecordingDownloader Downloader, StubHandler Handler) Make(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        return (new RecordingDownloader(new HttpClient(handler)), handler);
    }

    [Fact]
    public async Task Streams_the_file_and_takes_the_extension_from_the_link()
    {
        var (d, handler) = Make(_ => Media("audio/mpeg", [1, 2, 3, 4, 5]));

        var file = await d.DownloadAsync(Link, _dir, "2026-11-10-Kickoff-audio", ".bin", CancellationToken.None);

        Assert.Equal(Path.Combine(_dir, "2026-11-10-Kickoff-audio.mp3"), file.Path);
        Assert.Equal(5, file.Bytes);
        Assert.Equal([1, 2, 3, 4, 5], await File.ReadAllBytesAsync(file.Path));
        Assert.Single(Directory.GetFiles(_dir)); // no temp file left behind
        // The link is a signed URL for a file host: no credentials may travel with it.
        Assert.Null(handler.Requests.Single().Headers.Authorization);
    }

    [Theory]
    [InlineData("https://files.example.com/x/call?sig=1", "video/mp4", ".mp4")]
    [InlineData("https://files.example.com/x/call?sig=1", "audio/x-m4a", ".m4a")]
    [InlineData("https://files.example.com/x/call?sig=1", "application/octet-stream", ".fallback")]
    // An extension that is not a known media type is never used, so the link cannot make us save a script or program.
    [InlineData("https://files.example.com/x/run.exe?sig=1", "audio/mpeg", ".mp3")]
    [InlineData("https://files.example.com/x/run.exe?sig=1", "application/octet-stream", ".fallback")]
    public async Task Chooses_a_safe_extension(string url, string contentType, string expected)
    {
        var (d, _) = Make(_ => Media(contentType, [9]));

        var file = await d.DownloadAsync(new Uri(url), _dir, "rec", ".fallback", CancellationToken.None);

        Assert.Equal(expected, Path.GetExtension(file.Path));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, ErrorCategory.Permission)]
    [InlineData(HttpStatusCode.Unauthorized, ErrorCategory.Permission)]
    [InlineData(HttpStatusCode.NotFound, ErrorCategory.NotFound)]
    [InlineData(HttpStatusCode.Gone, ErrorCategory.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests, ErrorCategory.TransientApi)]
    [InlineData(HttpStatusCode.BadGateway, ErrorCategory.TransientApi)]
    public async Task Maps_http_failures_to_categories_without_leaving_files(HttpStatusCode status, ErrorCategory expected)
    {
        var (d, _) = Make(_ => new HttpResponseMessage(status));

        var ex = await Assert.ThrowsAsync<ExportFileException>(() => d.DownloadAsync(Link, _dir, "rec", ".mp3", CancellationToken.None));

        Assert.Equal(expected, ex.Category);
        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/json")]
    public async Task Refuses_an_error_page_served_as_the_recording(string contentType)
    {
        var (d, _) = Make(_ => Media(contentType, "<html>expired</html>"u8.ToArray()));

        var ex = await Assert.ThrowsAsync<ExportFileException>(() => d.DownloadAsync(Link, _dir, "rec", ".mp3", CancellationToken.None));

        Assert.Equal(ErrorCategory.InvalidResponse, ex.Category);
        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task Refuses_an_empty_download()
    {
        var (d, _) = Make(_ => Media("audio/mpeg", []));

        var ex = await Assert.ThrowsAsync<ExportFileException>(() => d.DownloadAsync(Link, _dir, "rec", ".mp3", CancellationToken.None));

        Assert.Equal(ErrorCategory.InvalidResponse, ex.Category);
        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task Only_https_links_are_downloaded()
    {
        var (d, handler) = Make(_ => Media("audio/mpeg", [1]));

        var ex = await Assert.ThrowsAsync<ExportFileException>(() =>
            d.DownloadAsync(new Uri("http://files.example.com/call.mp3"), _dir, "rec", ".mp3", CancellationToken.None));

        Assert.Equal(ErrorCategory.InvalidResponse, ex.Category);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Never_overwrites_an_existing_file_and_the_signed_link_stays_out_of_errors()
    {
        var (d, _) = Make(_ => Media("audio/mpeg", [1, 2]));
        Directory.CreateDirectory(_dir);
        var existing = Path.Combine(_dir, "rec.mp3");
        await File.WriteAllBytesAsync(existing, [7, 7, 7]);

        var ex = await Assert.ThrowsAsync<ExportFileException>(() => d.DownloadAsync(Link, _dir, "rec", ".mp3", CancellationToken.None));

        Assert.Equal(ErrorCategory.FileConflict, ex.Category);
        Assert.Equal([7, 7, 7], await File.ReadAllBytesAsync(existing));
        Assert.Single(Directory.GetFiles(_dir)); // the temp file was removed
        Assert.DoesNotContain("secret-token", ex.Message);
    }

    [Fact]
    public async Task A_network_error_does_not_echo_the_signed_link()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection to https://files.example.com/abc/call.mp3?sig=secret-token failed"));
        var d = new RecordingDownloader(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ExportFileException>(() => d.DownloadAsync(Link, _dir, "rec", ".mp3", CancellationToken.None));

        Assert.Equal(ErrorCategory.TransientApi, ex.Category);
        Assert.DoesNotContain("secret-token", ex.Message);
        Assert.DoesNotContain("files.example.com", ex.Message);
    }
}
