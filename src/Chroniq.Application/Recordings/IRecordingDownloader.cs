namespace Chroniq.Application.Recordings;

public sealed record RecordingFile(string Path, long Bytes);

public interface IRecordingDownloader
{
    /// <summary>
    /// Streams <paramref name="url"/> into <paramref name="directory"/> as <c>stem + extension</c>. The extension comes from
    /// the link, then the response's content type, then <paramref name="fallbackExtension"/>. The bytes go to a temp file
    /// first and are renamed only when complete, so an interrupted download never leaves a partial recording.
    /// The link is a signed URL for another host: the Fireflies API key is never sent with it, and it is never logged.
    /// Throws <see cref="Chroniq.Application.Export.ExportFileException"/> with the failure category.
    /// </summary>
    Task<RecordingFile> DownloadAsync(Uri url, string directory, string stem, string fallbackExtension, CancellationToken ct);
}
