using System.Net;
using System.Text;
using GranolaPI.Application.Fireflies;
using GranolaPI.Application.Granola;
using GranolaPI.Application.Interfaces;
using GranolaPI.Application.Settings;
using GranolaPI.Domain.Enums;
using GranolaPI.Infrastructure.Fireflies;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GranolaPI.UnitTests.Granola;

public class FirefliesMapperTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 21, 14, 0, 0, TimeSpan.Zero);

    private static FirefliesTranscript Call() => new()
    {
        Id = "01JFFCALL",
        Title = "Acme kickoff",
        Date = Start.ToUnixTimeMilliseconds(),
        Duration = 45,
        TranscriptUrl = "https://app.fireflies.ai/view/01JFFCALL",
        OrganizerEmail = "owner@me.com",
        User = new FirefliesUser { Name = "Owner Person", Email = "owner@me.com" },
        Participants = ["owner@me.com, carol@acme.com", "dave@acme.com"],
        MeetingAttendees = [new FirefliesAttendee { DisplayName = "Carol Client", Email = "carol@acme.com" }],
        Sentences =
        [
            new() { Index = 1, Text = "Second", StartTime = 5.5, SpeakerName = "Speaker 2" },
            new() { Index = 0, Text = "First", StartTime = 1, EndTime = 2, SpeakerName = "Carol Client" },
            new() { Index = 2, Text = "  ", SpeakerName = "Carol Client" },
        ],
        Summary = new FirefliesSummary
        {
            Overview = "We agreed the scope.",
            ShorthandBullet = "- scope agreed",
            ActionItems = "Carol: send spec",
            TopicsDiscussed = ["Scope", "Timeline"],
            Keywords = ["scope", "spec"],
        },
    };

    [Fact]
    public void Maps_call_metadata_attendees_and_times()
    {
        var fetch = FirefliesMapper.ToFetch(Call());
        var n = fetch.Note;

        Assert.Equal("01JFFCALL", n.Id);
        Assert.Equal(Start, n.CreatedAt);
        Assert.Equal(Start.AddMinutes(45), n.CalendarEvent!.ScheduledEndTime);
        Assert.Equal("owner@me.com", n.Owner!.Email);
        Assert.Equal("https://app.fireflies.ai/view/01JFFCALL", n.WebUrl);
        // Comma-separated participant entries are split; attendees without calendar details are still listed.
        Assert.Equal(["owner@me.com", "carol@acme.com", "dave@acme.com"], n.CalendarEvent.Invitees!.Select(i => i.Email));
        Assert.Contains(n.Attendees!, a => a.Name == "Carol Client" && a.Email == "carol@acme.com");
        Assert.Contains(n.Attendees!, a => a.Name is null && a.Email == "dave@acme.com");
    }

    [Fact]
    public void Orders_sentences_drops_blank_ones_and_keeps_anonymous_labels_anonymous()
    {
        var segments = FirefliesMapper.ToFetch(Call()).Transcript!.Segments;

        Assert.Equal(["First", "Second"], segments.Select(s => s.Text));
        Assert.Equal("Carol Client", segments[0].Speaker!.Name);
        Assert.Equal(Start.AddSeconds(1), segments[0].StartTime);
        Assert.Null(segments[1].Speaker!.Name);
        Assert.Equal("Speaker 2", segments[1].Speaker!.DiarizationLabel);
    }

    [Fact]
    public void Builds_ai_notes_markdown_from_summary_sections()
    {
        var md = FirefliesMapper.ToFetch(Call()).Note.SummaryMarkdown!;

        Assert.StartsWith("## Overview\n\nWe agreed the scope.", md);
        Assert.Contains("## Notes\n\n- scope agreed", md);
        Assert.Contains("## Action Items\n\nCarol: send spec", md);
        Assert.Contains("## Topics Discussed\n\n- Scope\n- Timeline", md);
        Assert.Contains("## Keywords\n\nscope, spec", md);
    }

    [Fact]
    public void Missing_summary_and_sentences_are_reported_as_empty_not_failed()
    {
        var fetch = FirefliesMapper.ToFetch(Call() with { Summary = new FirefliesSummary(), Sentences = null });
        Assert.Null(fetch.Note.SummaryMarkdown);
        Assert.True(fetch.Transcript!.Success);
        Assert.Empty(fetch.Transcript.Segments);
    }
}

public class FirefliesApiClientTests
{
    private sealed class ScriptedHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];
        private int _i;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct)));
            return responses[Math.Min(_i++, responses.Length - 1)](request);
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static FirefliesApiClient Client(ScriptedHandler handler, string? key = "ff_test_key_1234567")
    {
        var secrets = Substitute.For<ISecretProvider>();
        secrets.Get(SecretNames.FirefliesApiKey).Returns(key is null ? null : new ResolvedSecret(key, "test"));
        var settings = Substitute.For<ISettingsService>();
        settings.GetSectionAsync<SyncSettings>(SettingKeys.Sync, Arg.Any<CancellationToken>()).Returns(new SyncSettings { MaxRequestAttempts = 3 });
        return new FirefliesApiClient(new HttpClient(handler), secrets, settings,
            new FirefliesRequestThrottle { MinInterval = TimeSpan.Zero }, NullLogger<FirefliesApiClient>.Instance);
    }

    [Fact]
    public async Task Posts_graphql_query_with_bearer_token_and_paging_variables()
    {
        var h = new ScriptedHandler(_ => Json("""{"data":{"transcripts":[{"id":"01A","title":"T","date":1792591200000}]}}"""));
        var page = await Client(h).ListTranscriptsAsync(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), 50, 50, CancellationToken.None);

        Assert.Equal("01A", Assert.Single(page).Id);
        var (req, body) = h.Requests.Single();
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("https://api.fireflies.ai/graphql", req.RequestUri!.ToString());
        Assert.Equal("Bearer", req.Headers.Authorization!.Scheme);
        Assert.Equal("ff_test_key_1234567", req.Headers.Authorization.Parameter);
        Assert.Contains("transcripts(fromDate: $fromDate, limit: $limit, skip: $skip)", body);
        Assert.Contains("\"skip\":50", body);
        Assert.Contains("\"fromDate\":\"2026-01-02T03:04:05.000Z\"", body);
    }

    [Fact]
    public async Task Parses_transcript_with_string_times_and_list_keywords()
    {
        var h = new ScriptedHandler(_ => Json("""
            {"data":{"transcript":{"id":"01A","title":"T","date":1792591200000,"duration":12.5,
              "sentences":[{"index":0,"text":"Hi","start_time":"1.25","end_time":2,"speaker_name":"Ann"}],
              "summary":{"overview":"O","keywords":["a","b"],"topics_discussed":null}}}}
            """));
        var t = await Client(h).GetTranscriptAsync("01A", CancellationToken.None);

        Assert.NotNull(t);
        Assert.Equal(1.25, t.Sentences![0].StartTime);
        Assert.Equal(["a", "b"], t.Summary!.Keywords);
    }

    [Fact]
    public async Task Graphql_auth_error_maps_to_authentication_without_retry()
    {
        var h = new ScriptedHandler(_ => Json("""{"errors":[{"message":"Invalid API key","code":"auth_failed","extensions":{"code":"auth_failed","status":401}}],"data":null}"""));
        var ex = await Assert.ThrowsAsync<FirefliesApiException>(() => Client(h).ListTranscriptsAsync(null, 0, 50, CancellationToken.None));
        Assert.Equal(ErrorCategory.Authentication, ex.Category);
        Assert.Single(h.Requests);
        Assert.DoesNotContain("ff_test_key", ex.Message);
    }

    [Fact]
    public async Task Object_not_found_returns_null()
    {
        var h = new ScriptedHandler(_ => Json("""{"errors":[{"message":"Transcript not found","code":"object_not_found","extensions":{"code":"object_not_found","status":404}}],"data":{"transcript":null}}"""));
        Assert.Null(await Client(h).GetTranscriptAsync("01MISSING", CancellationToken.None));
    }

    [Fact]
    public async Task Rate_limit_is_retried_then_succeeds()
    {
        var h = new ScriptedHandler(
            _ =>
            {
                var r = Json("""{"errors":[{"message":"Too many requests","code":"too_many_requests"}]}""", HttpStatusCode.TooManyRequests);
                r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                return r;
            },
            _ => Json("""{"data":{"transcripts":[]}}"""));
        var page = await Client(h).ListTranscriptsAsync(null, 0, 50, CancellationToken.None);
        Assert.Empty(page);
        Assert.Equal(2, h.Requests.Count);
    }

    [Fact]
    public async Task Missing_key_is_a_configuration_error()
    {
        var result = await Client(new ScriptedHandler(_ => Json("{}")), key: null).TestConnectionAsync(CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(ErrorCategory.Configuration, result.ErrorCategory);
    }

    [Fact]
    public async Task Rejects_unsafe_transcript_ids_before_calling_the_api()
    {
        var h = new ScriptedHandler(_ => Json("{}"));
        await Assert.ThrowsAsync<FirefliesApiException>(() => Client(h).GetTranscriptAsync("../etc", CancellationToken.None));
        Assert.Empty(h.Requests);
    }
}
