using System.Net.Http.Json;
using System.Text.Json;
using Chroniq.Application.Interfaces;
using Chroniq.Domain.Enums;
using Chroniq.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chroniq.IntegrationTests;

public class WhisperAiSyncTests
{
    private static string S(JsonElement e, string p) => e.GetProperty(p).ToString();

    private static async Task<T> Db<T>(AppFactory f, Func<AppDbContext, Task<T>> q)
    {
        using var scope = f.Services.CreateScope();
        return await q(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static AppFactory WhisperAiOnly()
    {
        var f = new AppFactory();
        f.Credentials.Delete(SecretNames.GranolaApiKey);
        f.Credentials.Write(SecretNames.WhisperAiApiKey, "wai_integration_test_key_00000");
        return f;
    }

    [Fact]
    public async Task Completed_transcripts_are_exported_across_pages_and_unfinished_ones_are_skipped()
    {
        using var f = WhisperAiOnly();
        f.WhisperAi.Add("tr_A", "acme-kickoff");
        f.WhisperAi.Add("tr_B", "weekly-sync", minutesOffset: 10);
        f.WhisperAi.Add("tr_C", "design-review", minutesOffset: 20);
        f.WhisperAi.Add("tr_D", "still-running", status: "processing", minutesOffset: 30);
        f.WhisperAi.Add("tr_E", "silence", status: "error", minutesOffset: 40);
        var api = f.Api();

        var run = await f.RunSync(api, "Full");
        Assert.Equal("Succeeded", S(run, "status"));
        Assert.Equal("3", S(run, "notesDiscovered"));
        Assert.Equal(3, f.WhisperAi.ListCalls); // 5 transcripts, 2 per page

        var notes = f.Files("Unassigned/Notes");
        Assert.Equal(3, notes.Length);
        Assert.Equal(3, f.Files("Unassigned/Transcripts").Length);

        var kickoff = File.ReadAllText(notes.Single(n => n.Contains("acme-kickoff")));
        Assert.Contains("source: whisperai", kickoff);
        Assert.Contains("whisperai_transcript_id: \"tr_A\"", kickoff);
        Assert.Contains("WhisperAI provides a transcript only", kickoff);
        Assert.DoesNotContain("sig=private", kickoff);

        var transcript = File.ReadAllText(f.Files("Unassigned/Transcripts").Single(n => n.Contains("acme-kickoff")));
        Assert.Contains("Speaker A", transcript);
        Assert.Contains("Let's review the requirements.", transcript);
        Assert.DoesNotContain("sig=private", transcript);

        var list = await api.GetFromJsonAsync<JsonElement>("/api/meetings?source=WhisperAi", AppFactory.Json);
        Assert.Equal(3, list.GetProperty("total").GetInt32());
        Assert.Equal("WhisperAi", S(list.GetProperty("items")[0], "source"));
    }

    [Fact]
    public async Task Anonymous_speakers_are_not_people_and_complete_transcripts_are_not_refetched()
    {
        using var f = WhisperAiOnly();
        f.WhisperAi.Add("tr_A", "planning");
        f.WhisperAi.Add("tr_B", "late-call", status: "processing", minutesOffset: 5);
        var api = f.Api();

        await f.RunSync(api, "Full");
        Assert.Equal(1, f.WhisperAi.GetTranscriptCalls);
        var participants = await Db(f, db => db.MeetingParticipants.Select(p => p.Name).ToListAsync());
        Assert.DoesNotContain("Speaker A", participants);

        // The running job completes: the next incremental run imports it and leaves the finished one alone.
        f.WhisperAi.Transcripts["tr_B"] = f.WhisperAi.Transcripts["tr_B"] with { Transcript = f.WhisperAi.Transcripts["tr_B"].Transcript with { Status = "completed" } };
        var run = await f.RunSync(api);
        Assert.Equal("Succeeded", S(run, "status"));
        Assert.Equal(2, f.WhisperAi.GetTranscriptCalls);
        Assert.Equal(2, f.Files("Unassigned/Notes").Length);

        // The creation time from the listing is kept as the meeting date.
        var created = await Db(f, db => db.Meetings.Where(m => m.GranolaNoteId == "tr_A").Select(m => m.CreatedAt).SingleAsync());
        Assert.Equal(FakeWhisperAiApi.T0.UtcDateTime, created, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task All_three_sources_sync_together_and_a_whisperai_outage_does_not_block_the_others()
    {
        using var f = new AppFactory();
        f.Credentials.Write(SecretNames.FirefliesApiKey, "ff_integration_test_key_00000");
        f.Credentials.Write(SecretNames.WhisperAiApiKey, "wai_integration_test_key_00000");
        f.Granola.Add("g1", "Granola meeting");
        f.Fireflies.Add("01JFFCALLF", "Fireflies call");
        f.WhisperAi.Add("tr_A", "whisper-call");
        var api = f.Api();

        var run = await f.RunSync(api, "Full");
        Assert.Equal("Succeeded", S(run, "status"));
        Assert.Equal(3, f.Files("Unassigned/Notes").Length);
        var sources = await Db(f, db => db.Meetings.Select(m => m.Source).ToListAsync());
        Assert.Equal([MeetingSource.Granola, MeetingSource.Fireflies, MeetingSource.WhisperAi], sources.Order());

        f.WhisperAi.FailEverythingWith = ErrorCategory.TransientApi;
        f.Granola.Add("g2", "Second Granola meeting", hoursOffset: 3);
        run = await f.RunSync(api, "Full");
        Assert.Equal("PartiallySucceeded", S(run, "status"));
        Assert.Contains("WhisperAI", S(run, "error"));
        Assert.Equal(4, f.Files("Unassigned/Notes").Length);
    }

    [Fact]
    public async Task WhisperAi_only_with_a_rejected_key_fails_the_run()
    {
        using var f = WhisperAiOnly();
        f.WhisperAi.FailEverythingWith = ErrorCategory.Authentication;
        var run = await f.RunSync(f.Api(), "Full");
        Assert.Equal("Failed", S(run, "status"));
        Assert.Contains("Authentication", S(run, "error"));
        Assert.Equal(0, f.Granola.GetNoteCalls);
    }

    [Fact]
    public async Task WhisperAi_credentials_are_masked_and_tested_separately()
    {
        using var f = new AppFactory();
        var api = f.Api();
        var put = await api.PutAsJsonAsync("/api/settings/credentials/whisperai", new { apiKey = "wai_secret_key_1234567890", validate = true });
        put.EnsureSuccessStatusCode();
        var status = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(status.GetProperty("configured").GetBoolean());
        Assert.DoesNotContain("secret_key_123456", status.GetRawText());
        Assert.Equal("wai_secret_key_1234567890", f.Credentials.Read(SecretNames.WhisperAiApiKey));
        Assert.Equal("grn_integration_test_key_000000", f.Credentials.Read(SecretNames.GranolaApiKey));

        var test = await (await api.PostAsync("/api/whisperai/test-connection", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(test.GetProperty("success").GetBoolean());

        f.WhisperAi.FailEverythingWith = ErrorCategory.Authentication;
        var rejected = await api.PutAsJsonAsync("/api/settings/credentials/whisperai", new { apiKey = "wai_wrong_key_0000000000", validate = true });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("wai_secret_key_1234567890", f.Credentials.Read(SecretNames.WhisperAiApiKey)); // previous key restored

        var deleted = await (await api.DeleteAsync("/api/settings/credentials/whisperai")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(deleted.GetProperty("configured").GetBoolean());
    }
}
