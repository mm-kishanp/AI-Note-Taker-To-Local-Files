using System.Net;
using Chroniq.Application.Export;
using Chroniq.Application.Recordings;
using Chroniq.Domain.Enums;

namespace Chroniq.Infrastructure.Recordings;

/// <summary>
/// Downloads a signed recording link over HTTPS. Uses its own <see cref="HttpClient"/> with no default
/// credentials, because the link points at a file host rather than the Fireflies API.
/// </summary>
public sealed class RecordingDownloader(HttpClient http) : IRecordingDownloader
{
    // A stalled connection is abandoned after this long without a single byte; the total time is not limited.
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);
    private const int BufferSize = 128 * 1024;

    private static readonly Dictionary<string, string> ExtensionByContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/mpeg"] = ".mp3",
        ["audio/mp3"] = ".mp3",
        ["audio/mp4"] = ".m4a",
        ["audio/x-m4a"] = ".m4a",
        ["audio/aac"] = ".aac",
        ["audio/wav"] = ".wav",
        ["audio/x-wav"] = ".wav",
        ["audio/ogg"] = ".ogg",
        ["audio/webm"] = ".webm",
        ["video/mp4"] = ".mp4",
        ["video/webm"] = ".webm",
        ["video/quicktime"] = ".mov",
    };

    private static readonly HashSet<string> KnownExtensions = new(ExtensionByContentType.Values.Concat([".flac", ".mkv"]), StringComparer.OrdinalIgnoreCase);

    public async Task<RecordingFile> DownloadAsync(Uri url, string directory, string stem, string fallbackExtension, CancellationToken ct)
    {
        if (url.Scheme != Uri.UriSchemeHttps)
            throw new ExportFileException(ErrorCategory.InvalidResponse, "The recording link is not an HTTPS address, so it was not downloaded.");

        Directory.CreateDirectory(directory);
        var tmp = Path.Combine(directory, $".{stem}.{Guid.NewGuid():N}{AtomicFile.TempSuffix}");
        try
        {
            using var stall = CancellationTokenSource.CreateLinkedTokenSource(ct);
            stall.CancelAfter(StallTimeout);
            long bytes;
            string extension;
            try
            {
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, stall.Token);
                if (!response.IsSuccessStatusCode) throw FailureFor(response.StatusCode);

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (contentType is not null && (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                        || contentType.Contains("json", StringComparison.OrdinalIgnoreCase)))
                    throw new ExportFileException(ErrorCategory.InvalidResponse, $"The recording link returned '{contentType}' instead of a media file; it may have expired.");

                if (response.Content.Headers.ContentLength is { } expected and > 0)
                    AtomicFile.EnsureDiskSpace(directory, expected, factor: 1);

                extension = ExtensionFor(url, contentType, fallbackExtension);
                await using var source = await response.Content.ReadAsStreamAsync(stall.Token);
                await using var target = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous);
                var buffer = new byte[BufferSize];
                bytes = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, stall.Token)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), stall.Token);
                    bytes += read;
                    stall.CancelAfter(StallTimeout);
                }
                await target.FlushAsync(stall.Token);
                target.Flush(flushToDisk: true);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new ExportFileException(ErrorCategory.TransientApi, $"The recording download stalled for {StallTimeout.TotalSeconds:0} seconds and was abandoned.");
            }
            catch (HttpRequestException ex)
            {
                // The message of an HttpRequestException can echo the address; the link is a secret, so it is not passed on.
                throw new ExportFileException(ErrorCategory.TransientApi, $"Could not download the recording ({ex.HttpRequestError}).");
            }

            if (bytes == 0) throw new ExportFileException(ErrorCategory.InvalidResponse, "The recording download was empty.");

            var final = Path.Combine(directory, stem + extension);
            File.Move(tmp, final, overwrite: false);
            return new RecordingFile(final, bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(tmp);
            throw AtomicFile.Classify(ex, directory);
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
    }

    private static ExportFileException FailureFor(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            new ExportFileException(ErrorCategory.Permission, "The recording link was refused (it may have expired). It is requested again on the next sync."),
        HttpStatusCode.NotFound or HttpStatusCode.Gone =>
            new ExportFileException(ErrorCategory.NotFound, "The recording file no longer exists at Fireflies."),
        HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError =>
            new ExportFileException(ErrorCategory.TransientApi, $"The recording host is busy ({(int)status}); it is retried on the next sync."),
        _ => new ExportFileException(ErrorCategory.InvalidResponse, $"The recording host answered {(int)status}."),
    };

    internal static string ExtensionFor(Uri url, string? contentType, string fallback)
    {
        var fromUrl = Path.GetExtension(url.AbsolutePath);
        if (KnownExtensions.Contains(fromUrl)) return fromUrl.ToLowerInvariant();
        if (contentType is not null && ExtensionByContentType.TryGetValue(contentType, out var fromType)) return fromType;
        return fallback;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Left behind at worst; the next sync sweeps stale temp files.
        }
    }
}
