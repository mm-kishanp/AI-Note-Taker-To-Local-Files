using System.Net.Http.Json;
using System.Text.Json;
using Chroniq.Application.Fireflies;
using Chroniq.Application.Interfaces;
using Chroniq.Domain.Enums;
using Chroniq.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chroniq.IntegrationTests;

public class FirefliesSyncTests
{
    private static string S(JsonElement e, string p) => e.GetProperty(p).ToString();

    private static async Task<T> Db<T>(AppFactory f, Func<AppDbContext, Task<T>> q)
    {
        using var scope = f.Services.CreateScope();
        return await q(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static AppFactory FirefliesOnly()
    {
        var f = new AppFactory();
        f.Credentials.Delete(SecretNames.GranolaApiKey);
        f.Credentials.Write(SecretNames.FirefliesApiKey, "ff_integration_test_key_00000");
        return f;
    }

    [Fact]
    public async Task Fireflies_calls_are_exported_with_ai_notes_and_transcript()
    {
        using var f = FirefliesOnly();
        f.Fireflies.Add("01JFFCALLA", "Acme kickoff");
        f.Fireflies.Add("01JFFCALLB", "Weekly sync", hoursOffset: 2);
        var api = f.Api();

        var run = await f.RunSync(api, "Full");
        Assert.Equal("Succeeded", S(run, "status"));
        Assert.Equal("2", S(run, "notesDiscovered"));

        var notes = f.Files("Unassigned/Notes");
        Assert.Equal(2, notes.Length);
        Assert.Equal(2, f.Files("Unassigned/Transcripts").Length);

        var kickoff = File.ReadAllText(notes.Single(n => n.Contains("Acme-kickoff")));
        Assert.Contains("source: fireflies", kickoff);
        Assert.Contains("fireflies_transcript_id: \"01JFFCALLA\"", kickoff);
        Assert.Contains("## AI Notes", kickoff);
        Assert.Contains("Overview of Acme kickoff.", kickoff);
        Assert.Contains("Send the spec", kickoff);
        Assert.Contains("[Open in Fireflies](https://app.fireflies.ai/view/01JFFCALLA)", kickoff);

        var transcript = File.ReadAllText(f.Files("Unassigned/Transcripts").Single(n => n.Contains("Acme-kickoff")));
        Assert.Contains("Carol Client", transcript);
        Assert.Contains("let's review the requirements", transcript);

        var list = await api.GetFromJsonAsync<JsonElement>("/api/meetings?source=Fireflies", AppFactory.Json);
        Assert.Equal(2, list.GetProperty("total").GetInt32());
        Assert.Equal("Fireflies", S(list.GetProperty("items")[0], "source"));
    }

    [Fact]
    public async Task Fireflies_calls_are_classified_by_attendee_email_and_anonymous_speakers_are_not_people()
    {
        using var f = FirefliesOnly();
        f.Fireflies.Add("01JFFCALLC", "Requirements review", attendeeEmail: "carol@acme.com");
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

        var run = await f.RunSync(api, "Full");
        Assert.Equal("Succeeded", S(run, "status"));
        Assert.Single(f.Files("Projects/acme-portal/Notes"));
        Assert.Single(f.Files("Projects/acme-portal/Transcripts"));

        var participants = await Db(f, db => db.MeetingParticipants.Select(p => p.Name).ToListAsync());
        Assert.Contains("Carol Client", participants);
        Assert.DoesNotContain("Speaker 3", participants);
    }

    [Fact]
    public async Task Ai_notes_that_arrive_later_are_picked_up_and_complete_calls_are_not_refetched()
    {
        using var f = FirefliesOnly();
        f.Fireflies.Add("01JFFCALLD", "Design review", withSummary: false);
        f.Fireflies.Add("01JFFCALLE", "Planning", hoursOffset: 1);
        var api = f.Api();

        await f.RunSync(api, "Full");
        var pending = File.ReadAllText(f.Files("Unassigned/Notes").Single(n => n.Contains("Design-review")));
        Assert.Contains("Fireflies has not produced AI notes for this call yet", pending);
        Assert.Equal(2, f.Fireflies.GetTranscriptCalls);

        f.Fireflies.Calls["01JFFCALLD"] = f.Fireflies.Calls["01JFFCALLD"] with { Summary = FakeFirefliesApi.Summary("Design review") };
        var run = await f.RunSync(api);
        Assert.Equal("Succeeded", S(run, "status"));
        Assert.Equal(3, f.Fireflies.GetTranscriptCalls); // only the incomplete call was fetched again

        var updated = File.ReadAllText(f.Files("Unassigned/Notes").Single(n => n.Contains("Design-review")));
        Assert.Contains("Overview of Design review.", updated);
        Assert.Single(f.Files("Unassigned/Notes"), n => n.Contains("Design-review"));
    }

    [Fact]
    public async Task Both_sources_sync_together_and_a_fireflies_outage_does_not_block_granola()
    {
        using var f = new AppFactory();
        f.Credentials.Write(SecretNames.FirefliesApiKey, "ff_integration_test_key_00000");
        f.Granola.Add("g1", "Granola meeting");
        f.Fireflies.Add("01JFFCALLF", "Fireflies call");
        var api = f.Api();

        var run = await f.RunSync(api, "Full");
        Assert.Equal("Succeeded", S(run, "status"));
        Assert.Equal(2, f.Files("Unassigned/Notes").Length);
        var sources = await Db(f, db => db.Meetings.Select(m => m.Source).ToListAsync());
        Assert.Contains(MeetingSource.Granola, sources);
        Assert.Contains(MeetingSource.Fireflies, sources);

        f.Fireflies.FailEverythingWith = ErrorCategory.TransientApi;
        f.Granola.Add("g2", "Second Granola meeting", hoursOffset: 3);
        run = await f.RunSync(api, "Full");
        Assert.Equal("PartiallySucceeded", S(run, "status"));
        Assert.Contains("Fireflies", S(run, "error"));
        Assert.Equal(3, f.Files("Unassigned/Notes").Length);
    }

    [Fact]
    public async Task Fireflies_credentials_are_masked_and_tested_separately()
    {
        using var f = new AppFactory();
        var api = f.Api();
        var put = await api.PutAsJsonAsync("/api/settings/credentials/fireflies", new { apiKey = "ff_secret_key_1234567890", validate = true });
        put.EnsureSuccessStatusCode();
        var status = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(status.GetProperty("configured").GetBoolean());
        Assert.DoesNotContain("secret_key_123456", status.GetRawText());
        Assert.Equal("ff_secret_key_1234567890", f.Credentials.Read(SecretNames.FirefliesApiKey));
        Assert.Equal("grn_integration_test_key_000000", f.Credentials.Read(SecretNames.GranolaApiKey));

        var test = await (await api.PostAsync("/api/fireflies/test-connection", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(test.GetProperty("success").GetBoolean());

        f.Fireflies.FailEverythingWith = ErrorCategory.Authentication;
        var rejected = await api.PutAsJsonAsync("/api/settings/credentials/fireflies", new { apiKey = "ff_wrong_key_0000000000", validate = true });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("ff_secret_key_1234567890", f.Credentials.Read(SecretNames.FirefliesApiKey)); // previous key restored
    }
}
