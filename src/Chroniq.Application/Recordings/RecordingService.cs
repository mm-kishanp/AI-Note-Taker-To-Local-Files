using Chroniq.Application.Export;
using Chroniq.Application.Fireflies;
using Chroniq.Application.Interfaces;
using Chroniq.Application.Meetings;
using Chroniq.Application.Settings;
using Chroniq.Domain.Entities;
using Chroniq.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Chroniq.Application.Recordings;

/// <summary>
/// Downloads the audio and video of Fireflies calls into the recordings folder of the project the call was
/// exported to. A recording never fails the meeting: problems are kept on its <see cref="MeetingRecording"/>
/// row (visible in the UI) and retried on later syncs. Fireflies' links expire after 24 hours, so a link is
/// requested right before each download and never stored.
/// </summary>
public sealed class RecordingService(
    IAppDbContext db,
    IFirefliesApiClient fireflies,
    IRecordingDownloader downloader,
    IClock clock,
    ILogger<RecordingService> logger)
{
    /// <summary>Failed attempts before a recording is left alone until a reconcile or re-export is requested.</summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// A call this young may simply not be processed yet, so "no link" is not final. Kept shorter than the default
    /// Fireflies look-back (3 days), so sync still lists the call while it is waiting.
    /// </summary>
    public static readonly TimeSpan NotReadyWindow = TimeSpan.FromDays(2);

    public static IReadOnlyList<RecordingKind> EnabledKinds(ExportSettings s)
    {
        var kinds = new List<RecordingKind>(2);
        if (s.ExportAudio) kinds.Add(RecordingKind.Audio);
        if (s.ExportVideo) kinds.Add(RecordingKind.Video);
        return kinds;
    }

    /// <summary>True when an enabled kind has no row yet, or failed and still has attempts left.</summary>
    public static bool NeedsDownload(ExportSettings s, IEnumerable<MeetingRecording> existing)
    {
        var rows = existing.ToList();
        foreach (var kind in EnabledKinds(s))
        {
            var row = rows.FirstOrDefault(r => r.Kind == kind);
            if (row is null || (row.Status == RecordingStatus.Failed && row.Attempts < MaxAttempts)) return true;
        }
        return false;
    }

    /// <returns>The number of files downloaded by this call.</returns>
    public async Task<int> SyncAsync(
        Meeting meeting,
        ExportLayout layout,
        IReadOnlyDictionary<Guid, ProjectInfo> projects,
        ExportMode mode,
        CancellationToken ct)
    {
        var kinds = EnabledKinds(layout.Settings);
        if (kinds.Count == 0 || meeting.Source != MeetingSource.Fireflies || meeting.SourceStatus != SourceStatus.Active) return 0;

        // Recordings follow the primary export of the call; an excluded call has none.
        var primary = meeting.ExportRecords
            .Where(r => r.Role == ExportRole.Primary && r.Status != ExportStatus.Superseded)
            .OrderBy(r => r.Kind)
            .FirstOrDefault();
        if (primary is null) return 0;
        var project = primary.ProjectId is { } pid ? projects.GetValueOrDefault(pid) : null;

        var date = meeting.MeetingDate();
        var relDir = layout.RecordingsDirectory(primary.Location, project, date, meeting.GranolaNoteId, meeting.Title);
        var absDir = layout.AbsoluteRecordingPath(relDir.Length == 0 ? "." : relDir);
        var stem = layout.FileStem(project, date, meeting.GranolaNoteId, meeting.Title);
        var recreate = mode is ExportMode.Reconcile or ExportMode.Force;

        var downloaded = 0;
        foreach (var kind in kinds)
        {
            ct.ThrowIfCancellationRequested();
            var row = meeting.Recordings.FirstOrDefault(r => r.Kind == kind);
            if (await SyncKindAsync(meeting, row, kind, layout, absDir, $"{stem}-{kind.ToString().ToLowerInvariant()}", date, recreate, ct)) downloaded++;
        }
        return downloaded;
    }

    private async Task<bool> SyncKindAsync(
        Meeting meeting, MeetingRecording? row, RecordingKind kind, ExportLayout layout, string absDir, string stem,
        DateTimeOffset date, bool recreate, CancellationToken ct)
    {
        switch (row?.Status)
        {
            case RecordingStatus.Downloaded:
                var current = layout.AbsoluteRecordingPath(row.RelativePath);
                if (File.Exists(current))
                {
                    MoveIfReassigned(meeting, row, current, absDir, layout);
                    return false;
                }
                // The user moved or deleted it: leave it alone unless they asked to recreate missing files.
                if (!recreate) return false;
                break;
            case RecordingStatus.Unavailable when !recreate:
                return false;
            case RecordingStatus.Failed when row.Attempts >= MaxAttempts && !recreate:
                return false;
        }

        string? url;
        try
        {
            url = await fireflies.GetRecordingUrlAsync(meeting.GranolaNoteId, kind, ct);
        }
        catch (FirefliesApiException ex) when (ex.Category == ErrorCategory.Permission)
        {
            Settle(meeting, row, kind, RecordingStatus.Unavailable,
                $"This Fireflies key cannot download {Name(kind)}. Fireflies needs a Pro plan or higher for recordings.");
            return false;
        }
        catch (FirefliesApiException ex) when (ex.Category != ErrorCategory.Authentication)
        {
            Fail(meeting, row, kind, ex.Message);
            return false;
        }

        if (url is null)
        {
            // Too young to judge: Fireflies may still be processing the call. Leave no row so the next sync asks again.
            if (clock.UtcNow - date < NotReadyWindow) return false;
            Settle(meeting, row, kind, RecordingStatus.Unavailable, kind == RecordingKind.Video
                ? "Fireflies has no video for this call. Turn on \"Record meeting video\" in Fireflies (Pro plan or higher)."
                : "Fireflies has no audio for this call.");
            return false;
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            Fail(meeting, row, kind, "Fireflies returned a recording link that is not a valid address.");
            return false;
        }

        try
        {
            var fallback = kind == RecordingKind.Audio ? ".mp3" : ".mp4";
            RecordingFile file;
            try
            {
                file = await downloader.DownloadAsync(uri, absDir, stem, fallback, ct);
            }
            catch (ExportFileException ex) when (ex.Category == ErrorCategory.FileConflict)
            {
                // Another call with the same title and date owns that name: make the file name unique.
                var shortId = meeting.GranolaNoteId.Length > 6 ? meeting.GranolaNoteId[^6..] : meeting.GranolaNoteId;
                file = await downloader.DownloadAsync(uri, absDir, $"{stem}-{shortId}", fallback, ct);
            }

            row = Row(meeting, row, kind);
            row.Status = RecordingStatus.Downloaded;
            row.RelativePath = SafePath.ToPortable(Path.GetRelativePath(layout.RecordingsRoot, file.Path));
            row.SizeBytes = file.Bytes;
            row.Attempts = 0;
            row.LastError = null;
            row.UpdatedAt = clock.UtcNow.UtcDateTime;
            logger.LogInformation("Downloaded {Kind} for {NoteId}: {Bytes} bytes", kind, meeting.GranolaNoteId, file.Bytes);
            return true;
        }
        catch (ExportFileException ex)
        {
            Fail(meeting, row, kind, ex.Message);
            return false;
        }
    }

    /// <summary>Keeps the recording next to the call's other files when the call is reassigned to another project.</summary>
    private void MoveIfReassigned(Meeting meeting, MeetingRecording row, string currentAbs, string targetDirAbs, ExportLayout layout)
    {
        if (!layout.Settings.MoveFilesOnReassignment) return;
        var targetDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDirAbs));
        if (string.Equals(Path.GetDirectoryName(currentAbs), targetDir, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return;
        var to = Path.Combine(targetDir, Path.GetFileName(currentAbs));
        if (File.Exists(to)) return;
        try
        {
            AtomicFile.Move(currentAbs, to);
            row.RelativePath = SafePath.ToPortable(Path.GetRelativePath(layout.RecordingsRoot, to));
            row.UpdatedAt = clock.UtcNow.UtcDateTime;
        }
        catch (ExportFileException ex)
        {
            logger.LogWarning("Could not move the {Kind} recording of {NoteId}: {Message}", row.Kind, meeting.GranolaNoteId, ex.Message);
        }
    }

    private void Fail(Meeting meeting, MeetingRecording? row, RecordingKind kind, string message)
    {
        row = Row(meeting, row, kind);
        row.Status = RecordingStatus.Failed;
        row.Attempts++;
        row.LastError = Truncate(message);
        row.UpdatedAt = clock.UtcNow.UtcDateTime;
        logger.LogWarning("Downloading the {Kind} recording of {NoteId} failed (attempt {Attempt}/{Max}): {Message}",
            kind, meeting.GranolaNoteId, row.Attempts, MaxAttempts, message);
    }

    private void Settle(Meeting meeting, MeetingRecording? row, RecordingKind kind, RecordingStatus status, string message)
    {
        row = Row(meeting, row, kind);
        row.Status = status;
        row.LastError = Truncate(message);
        row.UpdatedAt = clock.UtcNow.UtcDateTime;
    }

    private MeetingRecording Row(Meeting meeting, MeetingRecording? row, RecordingKind kind)
    {
        if (row is not null) return row;
        row = new MeetingRecording { MeetingId = meeting.Id, Kind = kind };
        meeting.Recordings.Add(row);
        db.MeetingRecordings.Add(row);
        return row;
    }

    private static string Name(RecordingKind kind) => kind == RecordingKind.Audio ? "audio" : "video";

    private static string Truncate(string s) => s.Length <= 1000 ? s : s[..1000];
}
