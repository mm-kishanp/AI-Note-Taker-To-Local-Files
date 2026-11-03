using Chroniq.Application.DTOs;
using Chroniq.Application.Interfaces;
using Chroniq.Application.Meetings;
using Chroniq.Application.Settings;

namespace Chroniq.Application.Export;

/// <summary>Opens files/folders in the OS shell (local desktop use only).</summary>
public interface IShellLauncher
{
    bool IsSupported { get; }
    void Open(string path);
}

/// <summary>
/// Read-only browsing of the export repository plus path validation and root migration.
/// Every path is resolved under a configured root; nothing outside the roots can be read or opened.
/// </summary>
public sealed class ExportBrowserService(ISettingsService settings, IAppPaths paths, IShellLauncher shell)
{
    private const int MaxNodes = 5000;
    private const long MaxReadBytes = 20 * 1024 * 1024;

    public async Task<ExportTreeDto> TreeAsync(string rootKind, CancellationToken ct)
    {
        var layout = await LayoutAsync(ct);
        var root = RootOf(layout, rootKind);
        if (!Directory.Exists(root))
            return new ExportTreeDto(root, rootKind, false, "The export folder does not exist yet. It is created on the first sync.", null);
        var budget = MaxNodes;
        var tree = Build(new DirectoryInfo(root), root, ref budget);
        var warning = budget <= 0 ? $"Only the first {MaxNodes} entries are shown." : null;
        return new ExportTreeDto(root, rootKind, true, warning, tree);
    }

    private static ExportTreeNodeDto Build(DirectoryInfo dir, string root, ref int budget)
    {
        var children = new List<ExportTreeNodeDto>();
        var fileCount = 0;
        DateTimeOffset? latest = null;
        try
        {
            foreach (var sub in dir.EnumerateDirectories().Where(d => !d.Name.StartsWith('.')).OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (budget-- <= 0) break;
                if ((sub.Attributes & FileAttributes.ReparsePoint) != 0) continue; // don't follow links out of the root
                var node = Build(sub, root, ref budget);
                fileCount += node.FileCount;
                if (node.LastModified > latest || latest is null) latest = node.LastModified ?? latest;
                children.Add(node);
            }
            foreach (var f in dir.EnumerateFiles("*.md").OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (budget-- <= 0) break;
                fileCount++;
                var modified = new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero);
                if (latest is null || modified > latest) latest = modified;
                children.Add(new ExportTreeNodeDto(f.Name, Rel(root, f.FullName), false, 0, modified, f.Length, []));
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Unreadable folder: show it empty.
        }
        return new ExportTreeNodeDto(dir.Name, Rel(root, dir.FullName), true, fileCount, latest, null, children);
    }

    public async Task<FileContentDto> ReadFileAsync(string rootKind, string relativePath, CancellationToken ct)
    {
        var layout = await LayoutAsync(ct);
        var full = Resolve(layout, rootKind, relativePath);
        if (!full.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) throw ValidationException.Single("path", "Only Markdown files can be opened.");
        var info = new FileInfo(full);
        if (!info.Exists) throw new NotFoundException("File not found.");
        if (info.Length > MaxReadBytes) throw ValidationException.Single("path", "File is too large to display.");
        var content = await File.ReadAllTextAsync(full, ct);
        return new FileContentDto(Rel(RootOf(layout, rootKind), full), content, info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero));
    }

    public async Task OpenAsync(string rootKind, string? relativePath, CancellationToken ct)
    {
        if (!shell.IsSupported) throw ValidationException.Single("path", "Opening files is only supported when the app runs on your desktop.");
        var layout = await LayoutAsync(ct);
        var full = string.IsNullOrWhiteSpace(relativePath) ? RootOf(layout, rootKind) : Resolve(layout, rootKind, relativePath);
        if (!File.Exists(full) && !Directory.Exists(full)) throw new NotFoundException("Path not found.");
        shell.Open(full);
    }

    public void OpenAbsoluteUnderRoots(ExportLayout layout, string fullPath)
    {
        if (!shell.IsSupported) throw ValidationException.Single("path", "Opening files is only supported when the app runs on your desktop.");
        if (!SafePath.IsUnder(layout.NotesRoot, fullPath) && !SafePath.IsUnder(layout.TranscriptsRoot, fullPath) && !SafePath.IsUnder(layout.Root, fullPath))
            throw ValidationException.Single("path", "Path is outside the export roots.");
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath)) throw new NotFoundException("Path not found.");
        shell.Open(fullPath);
    }

    public ValidatePathResultDto ValidatePath(ValidatePathRequest req)
    {
        var messages = new List<string>();
        string full;
        try
        {
            full = SafePath.NormalizeRoot(req.Path, paths.BaseDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new ValidatePathResultDto(false, req.Path, false, false, null, [$"Invalid path: {ex.Message}"]);
        }
        var exists = Directory.Exists(full);
        if (!exists && File.Exists(full)) return new ValidatePathResultDto(false, full, false, false, null, ["A file exists at this path."]);
        if (!exists && req.CreateIfMissing)
        {
            try
            {
                Directory.CreateDirectory(full);
                exists = true;
                messages.Add("Folder created.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                messages.Add($"Could not create the folder: {ex.Message}");
            }
        }
        var writable = false;
        if (exists)
        {
            var probe = Path.Combine(full, $".gpi-write-test-{Guid.NewGuid():N}");
            try
            {
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                writable = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                messages.Add($"The folder is not writable: {ex.Message}");
            }
        }
        else
        {
            messages.Add("The folder does not exist. It will be created on the next sync (or choose 'create').");
        }
        long? free = null;
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(full)!);
            if (drive.IsReady)
            {
                free = drive.AvailableFreeSpace;
                if (free < 100L * 1024 * 1024) messages.Add("Less than 100 MB free on this drive.");
            }
        }
        catch (ArgumentException) { }
        var valid = exists ? writable : true;
        return new ValidatePathResultDto(valid, full, exists, writable, free, messages);
    }

    public DirectoryListingDto ListDirectories(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady)
                .Select(d => new DirectoryEntryDto(d.Name, d.RootDirectory.FullName)).ToList();
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home)) drives.Insert(0, new DirectoryEntryDto("Home", home));
            return new DirectoryListingDto(null, null, drives);
        }
        var full = Path.GetFullPath(path);
        if (!Directory.Exists(full)) throw new NotFoundException("Folder not found.");
        var dirs = new List<DirectoryEntryDto>();
        try
        {
            dirs = new DirectoryInfo(full).EnumerateDirectories()
                .Where(d => (d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Take(500)
                .Select(d => new DirectoryEntryDto(d.Name, d.FullName)).ToList();
        }
        catch (UnauthorizedAccessException) { }
        return new DirectoryListingDto(full, Directory.GetParent(full)?.FullName, dirs);
    }

    /// <summary>
    /// Copies the export repository to a new root and points the settings at it. The old folder is
    /// never deleted, existing files at the destination are never overwritten.
    /// </summary>
    public async Task<MigrateRootResultDto> MigrateRootAsync(MigrateRootRequest req, CancellationToken ct)
    {
        if (!req.Confirm) throw ValidationException.Single("confirm", "Confirm the migration.");
        var export = await settings.GetSectionAsync<ExportSettings>(SettingKeys.Export, ct);
        var layout = new ExportLayout(export, paths.BaseDirectory);
        var from = layout.Root;
        var to = SafePath.NormalizeRoot(req.NewRoot, paths.BaseDirectory);
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) throw ValidationException.Single("newRoot", "The new root is the current root.");
        if (SafePath.IsUnder(from, to) || SafePath.IsUnder(to, from)) throw ValidationException.Single("newRoot", "The new root cannot be inside the current root (or vice versa).");
        var check = ValidatePath(new ValidatePathRequest(to, CreateIfMissing: true));
        if (!check.Valid || !check.Writable) throw ValidationException.Single("newRoot", string.Join(" ", check.Messages));

        int copied = 0, skipped = 0;
        var warnings = new List<string>();
        if (Directory.Exists(from))
        {
            foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                if (AtomicFile.IsTempFile(file)) continue;
                var rel = Path.GetRelativePath(from, file);
                var dest = Path.Combine(to, rel);
                if (File.Exists(dest))
                {
                    var same = false;
                    if (new FileInfo(dest).Length == new FileInfo(file).Length)
                    {
                        var a = await File.ReadAllBytesAsync(dest, ct);
                        var b = await File.ReadAllBytesAsync(file, ct);
                        same = BytesEqual(a, b);
                    }
                    if (same)
                        skipped++;
                    else
                        warnings.Add($"Kept existing different file at destination: {SafePath.ToPortable(rel)}");
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: false);
                copied++;
            }
        }
        if (!string.IsNullOrWhiteSpace(export.NotesRootDirectory) || !string.IsNullOrWhiteSpace(export.TranscriptsRootDirectory))
            warnings.Add("Separate notes/transcripts roots are configured; they were not moved. Update them in Settings if needed.");
        await settings.SaveSectionAsync(SettingKeys.Export, export with { RootDirectory = to }, ct);
        warnings.Add($"The previous folder was left in place: {from}");
        return new MigrateRootResultDto(from, to, copied, skipped, warnings, true);
    }

    private async Task<ExportLayout> LayoutAsync(CancellationToken ct) =>
        new(await settings.GetSectionAsync<ExportSettings>(SettingKeys.Export, ct), paths.BaseDirectory);

    private static bool BytesEqual(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b);

    private static string RootOf(ExportLayout layout, string kind) => kind.ToLowerInvariant() switch
    {
        "notes" => layout.NotesRoot,
        "transcripts" => layout.TranscriptsRoot,
        _ => layout.Root,
    };

    private static string Resolve(ExportLayout layout, string rootKind, string relativePath)
    {
        try
        {
            return SafePath.Combine(RootOf(layout, rootKind), relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
        catch (InvalidOperationException ex)
        {
            throw ValidationException.Single("path", ex.Message);
        }
    }

    private static string Rel(string root, string full) => SafePath.ToPortable(Path.GetRelativePath(root, full)) is "." ? "" : SafePath.ToPortable(Path.GetRelativePath(root, full));
}
