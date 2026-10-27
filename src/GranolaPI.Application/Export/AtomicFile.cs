using System.Text;
using GranolaPI.Domain.Enums;

namespace GranolaPI.Application.Export;

public sealed class ExportFileException(ErrorCategory category, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public ErrorCategory Category { get; } = category;
}

public static class AtomicFile
{
    public const string TempSuffix = ".gpi-tmp";
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes to a temp file in the destination directory, flushes it to disk, then renames it over
    /// the destination. An interrupted write leaves at most a "*.gpi-tmp" file, never a partial .md.
    /// </summary>
    public static async Task WriteAsync(string path, string content, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(path)!;
        var tmp = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}{TempSuffix}");
        try
        {
            Directory.CreateDirectory(dir);
            EnsureDiskSpace(dir, Utf8NoBom.GetByteCount(content));
            await using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            await using (var writer = new StreamWriter(fs, Utf8NoBom, 64 * 1024))
            {
                await writer.WriteAsync(content.AsMemory(), ct);
                await writer.FlushAsync(ct);
                fs.Flush(flushToDisk: true);
            }
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ExportFileException)
        {
            TryDelete(tmp);
            throw Classify(ex, path);
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
    }

    public static async Task<string?> TryReadAsync(string path, CancellationToken ct)
    {
        try
        {
            return File.Exists(path) ? await File.ReadAllTextAsync(path, Encoding.UTF8, ct) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Classify(ex, path);
        }
    }

    /// <summary>Moves a file without overwriting anything at the destination.</summary>
    public static void Move(string from, string to)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Move(from, to, overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Classify(ex, to);
        }
    }

    /// <summary>Removes stale temp files left behind by an interrupted export.</summary>
    public static int CleanupTempFiles(string root)
    {
        if (!Directory.Exists(root)) return 0;
        var n = 0;
        foreach (var f in Directory.EnumerateFiles(root, "*" + TempSuffix, SearchOption.AllDirectories))
        {
            if (TryDelete(f)) n++;
        }
        return n;
    }

    public static ExportFileException Classify(Exception ex, string path)
    {
        const int diskFull = unchecked((int)0x80070070);
        const int handleDiskFull = unchecked((int)0x80070027);
        return ex switch
        {
            UnauthorizedAccessException => new ExportFileException(ErrorCategory.FilePermission, $"Access denied writing '{path}'.", ex),
            IOException io when io.HResult is diskFull or handleDiskFull => new ExportFileException(ErrorCategory.DiskSpace, $"Not enough disk space for '{path}'.", ex),
            PathTooLongException => new ExportFileException(ErrorCategory.Configuration, $"Path too long: '{path}'.", ex),
            IOException => new ExportFileException(ErrorCategory.FileConflict, $"I/O error on '{path}': {ex.Message}", ex),
            _ => new ExportFileException(ErrorCategory.Unknown, $"Unexpected error on '{path}': {ex.Message}", ex),
        };
    }

    private static void EnsureDiskSpace(string dir, long bytes)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(dir));
            if (string.IsNullOrEmpty(root)) return;
            var drive = new DriveInfo(root);
            if (drive.IsReady && drive.AvailableFreeSpace < bytes * 2 + 1024 * 1024)
                throw new ExportFileException(ErrorCategory.DiskSpace, $"Not enough free space on {drive.Name}.");
        }
        catch (ArgumentException)
        {
            // UNC or unusual path: let the write itself report problems.
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
