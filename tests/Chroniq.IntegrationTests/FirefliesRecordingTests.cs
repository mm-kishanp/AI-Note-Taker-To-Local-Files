using System.Net.Http.Json;
using System.Text.Json;
using Chroniq.Application.Interfaces;
using Chroniq.Domain.Entities;
using Chroniq.Domain.Enums;
using Chroniq.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chroniq.IntegrationTests;

public class FirefliesRecordingTests
{
    private static string S(JsonElement e, string p) => e.GetProperty(p).ToString();

    private static async Task<T> Db<T>(AppFactory f, Func<AppDbContext, Task<T>> q)
    {
        using var scope = f.Services.CreateScope();
        return await q(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static Task<List<MeetingRecording>> Rows(AppFactory f) =>
        Db(f, db => db.MeetingRecordings.AsNoTracking().OrderBy(r => r.Kind).ToListAsync());

    private static AppFactory FirefliesOnly()
    {
        var f = new AppFactory();
        f.Credentials.Delete(SecretNames.GranolaApiKey);
        f.Credentials.Write(SecretNames.FirefliesApiKey, "ff_integration_test_key_00000");
        return f;
    }

    private static async Task SetRecordings(HttpClient api, bool audio, bool video)
    {
        var current = await api.GetFromJsonAsync<JsonElement>("/api/settings");
        var export = JsonSerializer.Deserialize<Dictionary<string, object?>>(current.GetProperty("export").GetRawText())!;
        export["exportAudio"] = audio;
        export["exportVideo"] = video;
        (await api.PutAsJsonAsync("/api/settings", new { export })).EnsureSuccessStatusCode();
    }

    private static string[] Recordings(AppFactory f, string relativeDir)
    {
        var dir = Path.Combine(f.ExportRoot, relativeDir);
        return Directory.Exists(dir) ? Directory.GetFiles(dir, "*", SearchOption.AllDirectories) : [];
    }

    [Fact]
    public async Task Recordings_are_not_requested_unless_enabled()
    {
        using var f = FirefliesOnly();
        f.Fireflies.Add("01JFFREC01", "Acme kickoff");
        f.Fireflies.AddRecording("01JFFREC01", RecordingKind.Audio);

        var run = await f.RunSync(f.Api(), "Full");

        Assert.Equal("Succeeded", S(run, "status"));
        Assert.Equal(0, f.Fireflies.RecordingUrlCalls);
        Assert.Empty(f.Downloader.Requested);
        Assert.Empty(await Rows(f));
    }

    [Fact]
    public async Task Audio_and_video_are_downloaded_into_a_recordings_folder()
    {
        using var f = FirefliesOnly();
        f.Fireflies.Add("01JFFREC02", "Acme kickoff");
        f.Fireflies.AddRecording("01JFFREC02", RecordingKind.Audio);
        f.Fireflies.AddRecording("01JFFREC02", RecordingKind.Video);
        var api = f.Api();
        await SetRecordings(api, audio: true, video: true);

        var run = await f.RunSync(api, "Full");

        Assert.Equal("Succeeded", S(run, "status"));
        var files = Recordings(f, "Unassigned/Recordings").Select(Path.GetFileName).OrderBy(n => n).ToList();
        Assert.Equal(2, files.Count);
        Assert.Contains(files, n => n!.Contains("Acme-kickoff-audio") && n.EndsWith(".mp3"));
        Assert.Contains(files, n => n!.Contains("Acme-kickoff-video") && n.EndsWith(".mp4"));

        var rows = await Rows(f);
        Assert.Equal([RecordingKind.Audio, RecordingKind.Video], rows.Select(r => r.Kind));
        Assert.All(rows, r =>
        {
            Assert.Equal(RecordingStatus.Downloaded, r.Status);
            Assert.Equal(4, r.SizeBytes);
            Assert.StartsWith("Unassigned/Recordings/", r.RelativePath);
        });
        // The signed link is never stored.
        Assert.DoesNotContain(rows, r => (r.LastError ?? "").Contains("sig=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Recording_follows_the_project_the_call_is_assigned_to()
    {
        using var f = FirefliesOnly();
        f.Fireflies.Add("01JFFREC03", "Requirements review", attendeeEmail: "carol@acme.com");
        f.Fireflies.AddRecording("01JFFREC03", RecordingKind.Audio);
        var api = f.Api();
        var created = await api.PostAsJsonAsync("/api/projects", new
        {
            name = "Acme Portal",
            slug = "acme-portal",
            status = "Active",
            defaultClassificationPriority = 1,
            rules = new[] { new { kind = "Email", value = "carol@acme.com", enabled = true } },
        });
        created.EnsureSuccessStatusCode();
        await SetRecordings(api, audio: true, video: false);

        await f.RunSync(api, "Full");

        Assert.Single(Recordings(f, "Projects/acme-portal/Recordings"));
        Assert.Empty(Recordings(f, "Unassigned/Recordings"));
        Assert.Empty(f.Downloader.Requested.Where(u => u.AbsolutePath.EndsWith(".mp4")));
    }

    [Fact]
    public async Task A_plan_without_recording_access_never_fails_the_call_and_is_not_asked_again()
    {
        using var f = FirefliesOnly();
        f.Fireflies.Add("01JFFREC04", "Acme kickoff");
        f.Fireflies.FailRecordingsWith = ErrorCategory.Permission;
        var api = f.Api();
        await SetRecordings(api, audio: true, video: true);

        var run = await f.RunSync(api, "Full");

        Assert.Equal("Succeeded", S(run, "status"));
        Assert.Single(f.Files("Unassigned/Notes"));
        var rows = await Rows(f);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal(RecordingStatus.Unavailable, r.Status);
            Assert.Contains("Pro plan", r.LastError);
        });
        var asked = f.Fireflies.RecordingUrlCalls;

        await f.RunSync(api, "Incremental");

        Assert.Equal(asked, f.Fireflies.RecordingUrlCalls);
    }

    [Fact]
    public async Task A_failed_download_is_kept_on_the_recording_and_retried_on_the_next_sync()
    {
        using var f = FirefliesOnly();
        f.Fireflies.Add("01JFFREC05", "Acme kickoff");
        f.Fireflies.AddRecording("01JFFREC05", RecordingKind.Audio);
        f.Downloader.FailWith = ErrorCategory.TransientApi;
        var api = f.Api();
        await SetRecordings(api, audio: true, video: false);

        var first = await f.RunSync(api, "Full");

        Assert.Equal("Succeeded", S(first, "status")); // the notes and transcript are exported; a recording never fails the call
        var failed = Assert.Single(await Rows(f));
        Assert.Equal(RecordingStatus.Failed, failed.Status);
        Assert.Equal(1, failed.Attempts);
        Assert.Contains("fake download failure", failed.LastError);
        var transcriptFetches = f.Fireflies.GetTranscriptCalls;

        f.Downloader.FailWith = null;
        await f.RunSync(api, "Incremental");

        var done = Assert.Single(await Rows(f));
        Assert.Equal(RecordingStatus.Downloaded, done.Status);
        Assert.Equal(0, done.Attempts);
        Assert.Null(done.LastError);
        Assert.Single(Recordings(f, "Unassigned/Recordings"));
        // The call was complete, so only the recording was redone, not the transcript.
        Assert.Equal(transcriptFetches, f.Fireflies.GetTranscriptCalls);
    }

    [Fact]
    public async Task Turning_recordings_on_later_downloads_them_for_calls_that_are_already_imported()
    {
        using var f = FirefliesOnly();
        f.Fireflies.Add("01JFFREC06", "Acme kickoff");
        f.Fireflies.AddRecording("01JFFREC06", RecordingKind.Audio);
        var api = f.Api();
        await f.RunSync(api, "Full");
        Assert.Empty(await Rows(f));
        var transcriptFetches = f.Fireflies.GetTranscriptCalls;

        await SetRecordings(api, audio: true, video: false);
        await f.RunSync(api, "Incremental");

        Assert.Equal(RecordingStatus.Downloaded, Assert.Single(await Rows(f)).Status);
        Assert.Equal(transcriptFetches, f.Fireflies.GetTranscriptCalls);
    }

    [Fact]
    public async Task Missing_recordings_are_final_only_once_the_call_is_old_enough_to_be_processed()
    {
        using var f = FirefliesOnly();
        f.Fireflies.Add("01JFFOLD01", "Old call", hoursOffset: -72);
        f.Fireflies.Add("01JFFNEW01", "New call");
        f.Fireflies.AddRecording("01JFFOLD01", RecordingKind.Audio);
        f.Fireflies.AddRecording("01JFFNEW01", RecordingKind.Audio);
        var api = f.Api();
        await SetRecordings(api, audio: true, video: true);

        await f.RunSync(api, "Full");

        var rows = await Db(f, async db => await db.MeetingRecordings.AsNoTracking()
            .Join(db.Meetings, r => r.MeetingId, m => m.Id, (r, m) => new { m.GranolaNoteId, r.Kind, r.Status, r.LastError }).ToListAsync());
        Assert.Equal(RecordingStatus.Downloaded, rows.Single(r => r.GranolaNoteId == "01JFFOLD01" && r.Kind == RecordingKind.Audio).Status);
        var oldVideo = rows.Single(r => r.GranolaNoteId == "01JFFOLD01" && r.Kind == RecordingKind.Video);
        Assert.Equal(RecordingStatus.Unavailable, oldVideo.Status);
        Assert.Contains("no video", oldVideo.LastError);
        // The new call may still be processing at Fireflies: no verdict is recorded, so the next sync asks again.
        Assert.Equal(RecordingStatus.Downloaded, rows.Single(r => r.GranolaNoteId == "01JFFNEW01" && r.Kind == RecordingKind.Audio).Status);
        Assert.DoesNotContain(rows, r => r.GranolaNoteId == "01JFFNEW01" && r.Kind == RecordingKind.Video);
    }

    [Fact]
    public async Task Meeting_detail_lists_the_recordings()
    {
        using var f = FirefliesOnly();
        f.Fireflies.Add("01JFFREC07", "Acme kickoff");
        f.Fireflies.AddRecording("01JFFREC07", RecordingKind.Audio);
        var api = f.Api();
        await SetRecordings(api, audio: true, video: false);
        await f.RunSync(api, "Full");

        var list = await api.GetFromJsonAsync<JsonElement>("/api/meetings?source=Fireflies", AppFactory.Json);
        var id = S(list.GetProperty("items")[0], "id");
        var detail = await api.GetFromJsonAsync<JsonElement>($"/api/meetings/{id}", AppFactory.Json);

        var recording = detail.GetProperty("recordings").EnumerateArray().Single();
        Assert.Equal("Audio", S(recording, "kind"));
        Assert.Equal("Downloaded", S(recording, "status"));
        Assert.True(recording.GetProperty("fileExists").GetBoolean());
        Assert.Equal(4, recording.GetProperty("sizeBytes").GetInt64());
    }

    [Fact]
    public async Task Recordings_are_only_ever_taken_from_fireflies()
    {
        using var f = new AppFactory();
        f.Granola.Add("g1", "Granola meeting");
        var api = f.Api();
        await SetRecordings(api, audio: true, video: true);

        var run = await f.RunSync(api, "Full");

        Assert.Equal("Succeeded", S(run, "status"));
        Assert.Equal(0, f.Fireflies.RecordingUrlCalls);
        Assert.Empty(await Rows(f));
    }
}
