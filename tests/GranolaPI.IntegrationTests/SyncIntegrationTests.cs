using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GranolaPI.Application.Granola;
using GranolaPI.Application.Meetings;
using GranolaPI.Application.Synchronization;
using GranolaPI.Domain.Entities;
using GranolaPI.Domain.Enums;
using GranolaPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GranolaPI.IntegrationTests;

public class SyncIntegrationTests
{
    private static string S(JsonElement e, string p) => e.GetProperty(p).ToString();

    private static async Task<T> Db<T>(AppFactory f, Func<AppDbContext, Task<T>> q)
    {
        using var scope = f.Services.CreateScope();
        return await q(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public async Task Initial_import_exports_every_page_and_incremental_skips_unchanged()
    {
        using var f = new AppFactory();
        for (var i = 0; i < 5; i++) f.Granola.Add($"m{i}", $"Meeting {i}", hoursOffset: i);
        var api = f.Api();

        var run = await f.RunSync(api, "Full");
        Assert.Equal("Succeeded", S(run, "status"));
        Assert.Equal("5", S(run, "notesDiscovered")); // 3 pages of 2
        Assert.Equal(5, f.Files("Unassigned/Notes").Length);
        Assert.Equal(5, f.Files("Unassigned/Transcripts").Length);
        Assert.True(File.Exists(Path.Combine(f.ExportRoot, "Unassigned", "ReviewQueue.md")));

        var calls = f.Granola.GetNoteCalls;
        run = await f.RunSync(api);
        Assert.Equal("Succeeded", S(run, "status"));
        Assert.Equal(calls, f.Granola.GetNoteCalls); // nothing re-downloaded
        Assert.Equal("0", S(run, "filesUpdated"));
    }

    [Fact]
    public async Task Duplicate_runs_never_duplicate_meetings_or_files()
    {
        using var f = new AppFactory();
        f.Granola.Add("a", "Same title");
        f.Granola.Add("b", "Same title");
        var api = f.Api();
        await f.RunSync(api, "Full");
        await f.RunSync(api, "Full");
        await f.RunSync(api, "Full");
        Assert.Equal(2, await Db(f, db => db.Meetings.CountAsync()));
        Assert.Equal(2, f.Files("Unassigned/Notes").Length);
    }

    [Fact]
    public async Task Source_update_rewrites_only_the_changed_meeting_in_place()
    {
        using var f = new AppFactory();
        var a = f.Granola.Add("a", "Alpha");
        f.Granola.Add("b", "Beta", hoursOffset: 1);
        var api = f.Api();
        await f.RunSync(api, "Full");
        var beforeCalls = f.Granola.GetNoteCalls;
        var betaFiles = f.Files("Unassigned").Where(p => p.Contains("Beta")).ToDictionary(p => p, File.GetLastWriteTimeUtc);

        f.Granola.Touch(a.Id, n => n with { SummaryMarkdown = "Revised summary" });
        var run = await f.RunSync(api);
        Assert.Equal(beforeCalls + 1, f.Granola.GetNoteCalls);
        Assert.Equal("2", S(run, "filesUpdated")); // Alpha's notes + transcript (source_updated_at changed)
        Assert.All(betaFiles, kv => Assert.Equal(kv.Value, File.GetLastWriteTimeUtc(kv.Key))); // Beta untouched
        var notes = f.Files("Unassigned/Notes");
        Assert.Equal(2, notes.Length);
        Assert.Contains(notes, p => File.ReadAllText(p).Contains("Revised summary"));
    }

    [Fact]
    public async Task Partial_transcript_failure_is_recorded_and_retried_without_duplicating_notes()
    {
        using var f = new AppFactory();
        var a = f.Granola.Add("a", "Alpha");
        f.Granola.TranscriptFailures.Add(a.Id);
        var api = f.Api();

        var run = await f.RunSync(api, "Full");
        Assert.Equal("PartiallySucceeded", S(run, "status"));
        Assert.Single(f.Files("Unassigned/Notes"));
        Assert.Empty(f.Files("Unassigned/Transcripts"));
        var m = await Db(f, db => db.Meetings.SingleAsync());
        Assert.Equal(ContentStatus.Failed, m.TranscriptStatus);
        Assert.True(m.ExportPending);
        Assert.NotNull(m.NextRetryAt);

        f.Granola.TranscriptFailures.Clear();
        run = await f.RunSync(api, "RetryFailed");
        Assert.Equal("Succeeded", S(run, "status"));
        Assert.Single(f.Files("Unassigned/Notes"));
        Assert.Single(f.Files("Unassigned/Transcripts"));
        Assert.False((await Db(f, db => db.Meetings.SingleAsync())).ExportPending);
    }

    [Fact]
    public async Task Authentication_failure_aborts_the_run_with_clear_error()
    {
        using var f = new AppFactory();
        f.Granola.Add("a", "Alpha");
        f.Granola.FailEverythingWith = ErrorCategory.Authentication;
        var run = await f.RunSync(f.Api(), "Full");
        Assert.Equal("Failed", S(run, "status"));
        Assert.Contains("Authentication", S(run, "error"));
    }

    [Fact]
    public async Task Failure_during_pagination_does_not_advance_the_checkpoint()
    {
        using var f = new AppFactory();
        for (var i = 0; i < 5; i++) f.Granola.Add($"m{i}", $"Meeting {i}", hoursOffset: i);
        f.Granola.FailListingOnPage = 1;
        var api = f.Api();

        var run = await f.RunSync(api, "Full");
        Assert.Equal("Failed", S(run, "status"));
        Assert.Contains("pagination", S(run, "error"));

        f.Granola.FailListingOnPage = null;
        run = await f.RunSync(api); // incremental with no checkpoint => full listing
        Assert.Equal("Succeeded", S(run, "status"));
        Assert.Equal(5, f.Files("Unassigned/Notes").Length);
    }

    [Fact]
    public async Task Overlapping_runs_are_rejected_in_process_and_across_processes()
    {
        using var f = new AppFactory();
        f.Granola.Add("a", "Alpha");
        f.Granola.BlockGetNote = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = f.Api();

        var first = await api.PostAsJsonAsync("/api/sync/start", new { mode = "Full" });
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var second = await api.PostAsJsonAsync("/api/sync/start", new { mode = "Full" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // Another process (different lease owner) cannot acquire the lease while the run holds it.
        var lease = f.Services.GetRequiredService<ISyncLease>();
        await Task.Delay(300);
        Assert.False(await lease.TryAcquireAsync("other-process", TimeSpan.FromMinutes(1), CancellationToken.None));

        f.Granola.BlockGetNote.SetResult();
        await f.WaitForSync();
        Assert.True(await lease.TryAcquireAsync("other-process", TimeSpan.FromMinutes(1), CancellationToken.None));
        await lease.ReleaseAsync("other-process", CancellationToken.None);
    }

    [Fact]
    public async Task Restart_recovery_marks_interrupted_runs_and_resumes_pending_exports()
    {
        using var f = new AppFactory();
        var a = f.Granola.Add("a", "Alpha");
        var api = f.Api();
        await f.RunSync(api, "Full");

        // Simulate a crash: a run left "Running" and a meeting whose export never completed.
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SyncRuns.Add(new SyncRun { Status = SyncRunStatus.Running, StartedAt = DateTime.UtcNow.AddMinutes(-5) });
            var m = await db.Meetings.SingleAsync();
            m.ExportPending = true;
            await db.SaveChangesAsync();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<SyncHistoryService>().MarkInterruptedAsync(CancellationToken.None));
        }
        foreach (var file in f.Files("Unassigned/Notes")) File.Delete(file);

        var run = await f.RunSync(api, "Reconcile");
        Assert.Equal("Succeeded", S(run, "status"));
        Assert.Single(f.Files("Unassigned/Notes"));
        Assert.False((await Db(f, db => db.Meetings.SingleAsync())).ExportPending);
        Assert.True(await Db(f, db => db.SyncRuns.AnyAsync(r => r.Status == SyncRunStatus.Interrupted)));
    }

    [Fact]
    public async Task Manual_override_survives_source_updates_and_rule_changes()
    {
        using var f = new AppFactory();
        var a = f.Granola.Add("a", "Alpha", [new("Owner Person", "owner@me.com"), new("Alice", "alice@client-a.com")]);
        var api = f.Api();
        var motor = await (await api.PostAsJsonAsync("/api/projects", new { name = "Motor", status = "Active", defaultClassificationPriority = 100, rules = new[] { new { kind = "Email", value = "alice@client-a.com", enabled = true } } }, AppFactory.Json)).Content.ReadFromJsonAsync<JsonElement>();
        var film = await (await api.PostAsJsonAsync("/api/projects", new { name = "Film", status = "Active", defaultClassificationPriority = 100, rules = Array.Empty<object>() }, AppFactory.Json)).Content.ReadFromJsonAsync<JsonElement>();
        await f.RunSync(api, "Full");
        Assert.Single(f.Files("Projects/Motor/Notes"));

        var mid = await Db(f, db => db.Meetings.Select(m => m.Id).SingleAsync());
        var res = await api.PostAsJsonAsync($"/api/meetings/{mid}/assign-project", new { kind = "Project", projectId = S(film, "id") }, AppFactory.Json);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Single(f.Files("Projects/Film/Notes"));
        Assert.Empty(f.Files("Projects/Motor/Notes")); // moved, not copied

        f.Granola.Touch(a.Id, n => n with { SummaryMarkdown = "changed" });
        await f.RunSync(api);
        var m = await Db(f, db => db.Meetings.SingleAsync());
        Assert.Equal(AssignmentMethod.Manual, m.AssignmentMethod);
        Assert.Equal(Guid.Parse(S(film, "id")), m.AssignedProjectId);
        Assert.Contains("Motor", m.ClassificationDetailJson); // conflict with the email rule is recorded
    }

    [Fact]
    public async Task Unassigned_meeting_is_reassigned_after_saving_a_mapping()
    {
        using var f = new AppFactory();
        f.Granola.Add("a", "Alpha");
        f.Granola.Add("b", "Beta", hoursOffset: 1);
        var api = f.Api();
        await f.RunSync(api, "Full");
        var project = await (await api.PostAsJsonAsync("/api/projects", new { name = "Client A", status = "Active", defaultClassificationPriority = 100, rules = Array.Empty<object>() }, AppFactory.Json)).Content.ReadFromJsonAsync<JsonElement>();

        var unassigned = await api.GetFromJsonAsync<JsonElement>("/api/unassigned");
        Assert.Equal(2, unassigned.GetProperty("total").GetInt32());
        var first = unassigned.GetProperty("items")[0].GetProperty("meeting").GetProperty("id").GetString();

        // Save a speaker mapping from review: assigns this meeting and previews the other match.
        var preview = await (await api.PostAsJsonAsync($"/api/unassigned/{first}/mappings",
            new { projectId = S(project, "id"), kind = "SpeakerName", value = "Alice Client", assignThisMeeting = true }, AppFactory.Json)).Content.ReadFromJsonAsync<JsonElement>();
        var others = preview.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("meetingId").GetString()).ToList();
        Assert.Single(others);

        // Bulk apply requires confirmation.
        var unconfirmed = await api.PostAsJsonAsync($"/api/projects/{S(project, "id")}/apply-mapping", new { meetingIds = others, confirm = false });
        Assert.Equal(HttpStatusCode.BadRequest, unconfirmed.StatusCode);
        var apply = await api.PostAsJsonAsync($"/api/projects/{S(project, "id")}/apply-mapping", new { meetingIds = others, confirm = true });
        Assert.Equal(HttpStatusCode.Accepted, apply.StatusCode);
        await f.WaitForSync();

        Assert.Equal(2, f.Files("Projects/ClientA/Notes").Length);
        Assert.Equal(2, f.Files("Projects/ClientA/Transcripts").Length);
        Assert.Empty(f.Files("Unassigned/Notes"));
        Assert.True(File.Exists(Path.Combine(f.ExportRoot, "Projects", "ClientA", "PROJECT_CONTEXT.md")));
        Assert.Equal(0, (await api.GetFromJsonAsync<JsonElement>("/api/unassigned")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Deleted_or_inaccessible_notes_keep_exported_files()
    {
        using var f = new AppFactory();
        var a = f.Granola.Add("a", "Alpha");
        var api = f.Api();
        await f.RunSync(api, "Full");
        f.Granola.Notes.TryRemove(a.Id, out _);
        var mid = await Db(f, db => db.Meetings.Select(m => m.Id).SingleAsync());
        await api.PostAsync($"/api/meetings/{mid}/retry", null);
        await f.WaitForSync();
        var m = await Db(f, db => db.Meetings.SingleAsync());
        Assert.Equal(SourceStatus.Inaccessible, m.SourceStatus);
        Assert.Single(f.Files("Unassigned/Notes"));
    }
}
