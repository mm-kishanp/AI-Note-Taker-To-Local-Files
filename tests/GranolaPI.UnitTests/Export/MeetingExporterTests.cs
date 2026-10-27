using GranolaPI.Application.Export;
using GranolaPI.Application.Meetings;
using GranolaPI.Application.Settings;
using GranolaPI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GranolaPI.UnitTests.Export;

public class MeetingExporterTests : IDisposable
{
    private readonly TestEnv _env = new();

    public void Dispose() => _env.Dispose();

    private async Task<ExportOutcome> Export(Domain.Entities.Meeting m, ExportLayout? layout = null, ExportMode mode = ExportMode.Normal)
    {
        var outcome = await _env.Exporter().ExportAsync(m, layout ?? _env.Layout(), Fixtures.Infos(_env), mode, CancellationToken.None);
        await _env.Db.SaveChangesAsync();
        return outcome;
    }

    private string Abs(string rel) => Path.Combine(_env.Root, rel.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public async Task Unassigned_meeting_gets_separate_notes_and_transcript_files()
    {
        var m = Fixtures.AddMeeting(_env);
        var outcome = await Export(m);

        Assert.Equal(1, outcome.NotesWritten);
        Assert.Equal(1, outcome.TranscriptsWritten);
        Assert.Equal("Unassigned/Notes/2026-10-26-Client-Requirement-Discussion.md", m.NotesExportPath);
        Assert.Equal("Unassigned/Transcripts/2026-10-26-Client-Requirement-Discussion.md", m.TranscriptExportPath);
        Assert.True(File.Exists(Abs(m.NotesExportPath!)));
        Assert.Contains("[Client Requirement Discussion — Transcript](<../Transcripts/2026-10-26-Client-Requirement-Discussion.md>)", File.ReadAllText(Abs(m.NotesExportPath!)));
        Assert.Equal(ExportStatus.Exported, m.ExportStatus);
        Assert.False(m.ExportPending);
        Assert.Empty(Directory.GetFiles(_env.Root, "*" + AtomicFile.TempSuffix, SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Assigned_meeting_goes_to_project_folders()
    {
        var p = Fixtures.AddProject(_env, "Motor Dealer App", "MotorDealerApp");
        var m = Fixtures.AddMeeting(_env, status: ClassificationStatus.Assigned, projectId: p.Id);
        await Export(m);
        Assert.Equal("Projects/MotorDealerApp/Notes/2026-10-26-Client-Requirement-Discussion.md", m.NotesExportPath);
        Assert.Equal("Projects/MotorDealerApp/Transcripts/2026-10-26-Client-Requirement-Discussion.md", m.TranscriptExportPath);
    }

    [Fact]
    public async Task Separate_roots_for_notes_and_transcripts()
    {
        var notesRoot = Path.Combine(_env.Root, "MeetingNotes");
        var transRoot = Path.Combine(_env.Root, "MeetingTranscripts");
        var p = Fixtures.AddProject(_env, "Motor", "MotorDealerApp");
        var m = Fixtures.AddMeeting(_env, status: ClassificationStatus.Assigned, projectId: p.Id);
        var layout = _env.Layout(new ExportSettings
        {
            RootDirectory = _env.Root,
            NotesRootDirectory = notesRoot,
            TranscriptsRootDirectory = transRoot,
            NotesDirectoryTemplate = "{ProjectDirectory}",
            TranscriptsDirectoryTemplate = "{ProjectDirectory}",
        });
        await Export(m, layout);
        Assert.True(File.Exists(Path.Combine(notesRoot, "Projects", "MotorDealerApp", "2026-10-26-Client-Requirement-Discussion.md")));
        Assert.True(File.Exists(Path.Combine(transRoot, "Projects", "MotorDealerApp", "2026-10-26-Client-Requirement-Discussion.md")));
    }

    [Fact]
    public async Task Same_folder_for_both_kinds_gives_transcript_a_distinct_name()
    {
        var m = Fixtures.AddMeeting(_env);
        var layout = _env.Layout(new ExportSettings { RootDirectory = _env.Root, UnassignedNotesDirectory = "Inbox", UnassignedTranscriptsDirectory = "Inbox" });
        await Export(m, layout);
        Assert.Equal("Inbox/2026-10-26-Client-Requirement-Discussion.md", m.NotesExportPath);
        Assert.Equal("Inbox/2026-10-26-Client-Requirement-Discussion-transcript.md", m.TranscriptExportPath);
    }

    [Fact]
    public async Task Duplicate_titles_on_same_day_get_unique_stable_names()
    {
        var a = Fixtures.AddMeeting(_env, Fixtures.Note("not_aaaaaaaaaaaaaa", "Standup"));
        var b = Fixtures.AddMeeting(_env, Fixtures.Note("not_bbbbbbbbbbbbbb", "Standup"));
        await Export(a);
        await Export(b);
        Assert.Equal("Unassigned/Notes/2026-10-26-Standup.md", a.NotesExportPath);
        Assert.Equal("Unassigned/Notes/2026-10-26-Standup-bbbbbb.md", b.NotesExportPath);

        // Re-export keeps the same (sticky) file name.
        await Export(b);
        Assert.Equal("Unassigned/Notes/2026-10-26-Standup-bbbbbb.md", b.NotesExportPath);
        Assert.Equal(4, Directory.GetFiles(_env.Root, "*.md", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task Unrelated_existing_file_is_never_overwritten()
    {
        var target = Abs("Unassigned/Notes/2026-10-26-Client-Requirement-Discussion.md");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "my own unrelated notes");
        var m = Fixtures.AddMeeting(_env);
        await Export(m);
        Assert.Equal("my own unrelated notes", File.ReadAllText(target));
        Assert.EndsWith("-aaaaaa.md", m.NotesExportPath);
    }

    [Fact]
    public async Task Re_export_without_changes_does_not_rewrite_file()
    {
        var m = Fixtures.AddMeeting(_env);
        await Export(m);
        var path = Abs(m.NotesExportPath!);
        var before = File.GetLastWriteTimeUtc(path);
        await Task.Delay(50);
        _env.Clock.UtcNow = _env.Clock.UtcNow.AddHours(1);
        var outcome = await Export(m);
        Assert.Equal(0, outcome.NotesWritten + outcome.TranscriptsWritten);
        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public async Task Source_change_updates_unmodified_file_in_place()
    {
        var m = Fixtures.AddMeeting(_env);
        await Export(m);
        var path = m.NotesExportPath;
        MeetingIngestor.Apply(m, new(Fixtures.Note(summary: "Updated summary"), new(true, Fixtures.Note().Transcript!)), _env.Clock.UtcNow);
        var outcome = await Export(m);
        Assert.Equal(1, outcome.FilesUpdated);
        Assert.Equal(path, m.NotesExportPath);
        Assert.Contains("Updated summary", File.ReadAllText(Abs(path!)));
    }

    [Fact]
    public async Task User_edit_plus_source_change_is_a_conflict_and_edit_is_kept()
    {
        var m = Fixtures.AddMeeting(_env);
        await Export(m);
        var path = Abs(m.NotesExportPath!);
        File.AppendAllText(path, "\nMy local edit\n");
        var edited = File.ReadAllText(path);

        // Source unchanged: local edit is only noted.
        await Export(m);
        Assert.Equal(ExportStatus.LocallyModified, m.ExportRecords.Single(r => r.Kind == ExportKind.Notes).Status);

        MeetingIngestor.Apply(m, new(Fixtures.Note(summary: "Changed at source"), new(true, Fixtures.Note().Transcript!)), _env.Clock.UtcNow);
        var outcome = await Export(m);
        Assert.Equal(1, outcome.Conflicts);
        Assert.Equal(ExportStatus.Conflict, m.ExportStatus);
        Assert.Equal(edited, File.ReadAllText(path));

        // Explicit force regenerates.
        await Export(m, mode: ExportMode.Force);
        Assert.Contains("Changed at source", File.ReadAllText(path));
    }

    [Fact]
    public async Task Always_overwrite_policy_replaces_edits()
    {
        var m = Fixtures.AddMeeting(_env);
        var layout = _env.Layout(new ExportSettings { RootDirectory = _env.Root, ConflictPolicy = ConflictPolicy.AlwaysOverwrite });
        await Export(m, layout);
        var path = Abs(m.NotesExportPath!);
        File.AppendAllText(path, "edit");
        MeetingIngestor.Apply(m, new(Fixtures.Note(summary: "New"), new(true, Fixtures.Note().Transcript!)), _env.Clock.UtcNow);
        await Export(m, layout);
        Assert.DoesNotContain("edit\n", File.ReadAllText(path).Replace("\r", ""));
        Assert.Equal(ExportStatus.Exported, m.ExportStatus);
    }

    [Fact]
    public async Task Reassignment_moves_unmodified_files_without_duplicates()
    {
        var p = Fixtures.AddProject(_env, "Film", "FilmWale");
        var m = Fixtures.AddMeeting(_env);
        await Export(m);
        var old = Abs(m.NotesExportPath!);

        m.ClassificationStatus = ClassificationStatus.Assigned;
        m.AssignedProjectId = p.Id;
        await Export(m);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(Abs(m.NotesExportPath!)));
        Assert.StartsWith("Projects/FilmWale/Notes/", m.NotesExportPath);
        Assert.Equal(2, Directory.GetFiles(_env.Root, "*.md", SearchOption.AllDirectories).Length);
        Assert.Contains(m.ExportRecords, r => r.Status == ExportStatus.Superseded);
    }

    [Fact]
    public async Task Reassignment_leaves_edited_file_in_place()
    {
        var p = Fixtures.AddProject(_env, "Film", "FilmWale");
        var m = Fixtures.AddMeeting(_env);
        await Export(m);
        var old = Abs(m.NotesExportPath!);
        File.AppendAllText(old, "\nedited");

        m.ClassificationStatus = ClassificationStatus.Assigned;
        m.AssignedProjectId = p.Id;
        await Export(m);

        Assert.True(File.Exists(old));
        Assert.True(File.Exists(Abs(m.NotesExportPath!)));
    }

    [Fact]
    public async Task Failed_transcript_is_not_exported_as_empty()
    {
        var m = Fixtures.AddMeeting(_env, transcriptOk: false);
        var outcome = await Export(m);
        Assert.Equal(1, outcome.NotesWritten);
        Assert.Equal(0, outcome.TranscriptsWritten);
        Assert.Null(m.TranscriptExportPath is { } t && File.Exists(Abs(t)) ? t : null);
        Assert.Equal(ExportStatus.Failed, m.ExportRecords.Single(r => r.Kind == ExportKind.Transcript).Status);
        Assert.True(m.ExportPending);

        // Retry succeeds without duplicating the notes file.
        MeetingIngestor.Apply(m, new(Fixtures.Note(), new(true, Fixtures.Note().Transcript!)), _env.Clock.UtcNow);
        var notesPath = m.NotesExportPath;
        outcome = await Export(m);
        Assert.Equal(1, outcome.TranscriptsWritten);
        Assert.False(m.ExportPending);
        // The notes file is updated in place (it now links to the transcript), never duplicated.
        Assert.Equal(notesPath, m.NotesExportPath);
        Assert.Single(Directory.GetFiles(Path.Combine(_env.Root, "Unassigned", "Notes")));
        Assert.Contains("../Transcripts/", File.ReadAllText(Abs(notesPath!)));
    }

    [Fact]
    public async Task Meeting_without_usable_content_is_skipped_not_faked()
    {
        var m = Fixtures.AddMeeting(_env, Fixtures.Note(summary: null, transcript: []) with { PrivateNotesMarkdown = null });
        await Export(m);
        Assert.Equal(ExportStatus.Skipped, m.ExportStatus);
        Assert.False(Directory.Exists(Path.Combine(_env.Root, "Unassigned")) && Directory.GetFiles(Path.Combine(_env.Root, "Unassigned"), "*.md", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public async Task Excluded_meeting_writes_nothing_new()
    {
        var m = Fixtures.AddMeeting(_env, status: ClassificationStatus.Excluded);
        await Export(m);
        Assert.Empty(Directory.GetFiles(_env.Root, "*.md", SearchOption.AllDirectories));
        Assert.Equal(ExportStatus.Skipped, m.ExportStatus);
    }

    [Fact]
    public async Task Deleted_by_user_file_is_reported_missing_and_recreated_only_on_reconcile()
    {
        var m = Fixtures.AddMeeting(_env);
        await Export(m);
        var path = Abs(m.NotesExportPath!);
        File.Delete(path);

        await Export(m);
        Assert.False(File.Exists(path));
        Assert.Equal(ExportStatus.Missing, m.ExportRecords.Single(r => r.Kind == ExportKind.Notes).Status);

        await Export(m, mode: ExportMode.Reconcile);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Read_only_destination_is_reported_as_permission_failure()
    {
        if (!OperatingSystem.IsWindows()) return;
        var m = Fixtures.AddMeeting(_env);
        await Export(m);
        var path = Abs(m.NotesExportPath!);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            MeetingIngestor.Apply(m, new(Fixtures.Note(summary: "changed"), new(true, Fixtures.Note().Transcript!)), _env.Clock.UtcNow);
            var outcome = await Export(m);
            Assert.Contains(outcome.Errors, e => e.Category == ErrorCategory.FilePermission);
            Assert.True(m.ExportPending);
            Assert.Empty(Directory.GetFiles(_env.Root, "*" + AtomicFile.TempSuffix, SearchOption.AllDirectories));
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task Moving_the_export_root_keeps_records_valid()
    {
        var m = Fixtures.AddMeeting(_env);
        await Export(m);
        var newRoot = Path.Combine(_env.Root, "..", Guid.NewGuid().ToString("N"));
        CopyDir(_env.Root, newRoot);
        try
        {
            var layout = _env.Layout(new ExportSettings { RootDirectory = newRoot });
            var outcome = await Export(m, layout);
            Assert.Equal(0, outcome.NotesWritten + outcome.TranscriptsWritten + outcome.Conflicts); // relative paths still resolve; nothing rewritten
            Assert.Equal(ExportStatus.Exported, m.ExportStatus);
        }
        finally
        {
            Directory.Delete(newRoot, true);
        }
    }

    [Fact]
    public async Task Atomic_write_cleans_up_stale_temp_files()
    {
        var dir = Path.Combine(_env.Root, "x");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ".a.md.123" + AtomicFile.TempSuffix), "partial");
        Assert.Equal(1, AtomicFile.CleanupTempFiles(_env.Root));
        await AtomicFile.WriteAsync(Path.Combine(dir, "a.md"), "complete ✅", CancellationToken.None);
        Assert.Equal("complete ✅", File.ReadAllText(Path.Combine(dir, "a.md")));
        Assert.Single(Directory.GetFiles(dir));
    }

    [Fact]
    public async Task Project_context_preserves_user_text_outside_markers()
    {
        var p = Fixtures.AddProject(_env, "Motor", "MotorDealerApp");
        var m = Fixtures.AddMeeting(_env, Fixtures.Note(summary: "## Next Steps\n- Send estimate to Alice\n## Other\n- noise"), status: ClassificationStatus.Assigned, projectId: p.Id);
        await Export(m);
        var svc = new ProjectContextService(_env.Db, _env.Clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProjectContextService>.Instance);
        await svc.UpdateProjectsAsync([p.Id], _env.Layout(), CancellationToken.None);

        var ctxPath = Path.Combine(_env.Root, "Projects", "MotorDealerApp", "PROJECT_CONTEXT.md");
        var text = File.ReadAllText(ctxPath);
        Assert.Contains("- Send estimate to Alice", text);
        Assert.DoesNotContain("- noise", text);
        Assert.True(File.Exists(Path.Combine(_env.Root, "Projects", "MotorDealerApp", "README.md")));

        File.WriteAllText(ctxPath, "# My header\nKeep me\n" + text[text.IndexOf(ProjectContextService.StartMarker, StringComparison.Ordinal)..] + "\nKeep me too\n");
        File.WriteAllText(Path.Combine(_env.Root, "Projects", "MotorDealerApp", "README.md"), "user readme");
        await svc.UpdateProjectsAsync([p.Id], _env.Layout(), CancellationToken.None);
        var updated = File.ReadAllText(ctxPath);
        Assert.StartsWith("# My header\nKeep me\n", updated);
        Assert.EndsWith("Keep me too\n", updated);
        Assert.Equal("user readme", File.ReadAllText(Path.Combine(_env.Root, "Projects", "MotorDealerApp", "README.md")));
    }

    private static void CopyDir(string from, string to)
    {
        foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var dest = Path.Combine(to, Path.GetRelativePath(from, f));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(f, dest);
        }
    }
}
