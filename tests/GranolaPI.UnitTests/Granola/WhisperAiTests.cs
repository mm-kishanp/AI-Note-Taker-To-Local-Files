using System.Net;
using System.Text;
using GranolaPI.Application.Export;
using GranolaPI.Application.Interfaces;
using GranolaPI.Application.Settings;
using GranolaPI.Application.WhisperAi;
using GranolaPI.Domain.Enums;
using GranolaPI.Infrastructure.WhisperAi;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GranolaPI.UnitTests.Granola;

public class WhisperAiMapperTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 21, 14, 0, 0, TimeSpan.Zero);

    private static WhisperAiTranscript Transcript() => new()
    {
        Id = "tr_8f1c2d3e4a5b",
        Status = "completed",
        AudioUrl = "https://files.example.com/calls/Acme%20kickoff.mp3?X-Amz-Signature=secret",
        Text = "Hello there. Hi.",
        Utterances =
        [
            new() { Speaker = "B", Text = "Hi.", Start = 5500, End = 6000 },
            new() { Speaker = "A", Text = "Hello there.", Start = 240, End = 2560 },
            new() { Speaker = "A", Text = "  ", Start = 7000 },
        ],
    };

    [Fact]
    public void Maps_transcript_metadata_without_exposing_the_audio_url()
    {
        var n = WhisperAiMapper.ToFetch(Transcript(), Created).Note;

        Assert.Equal("tr_8f1c2d3e4a5b", n.Id);
        Assert.Equal("Acme kickoff", n.Title);
        Assert.Equal(Created, n.CreatedAt);
        Assert.Null(n.WebUrl);
        Assert.Null(n.SummaryMarkdown);
        Assert.Null(n.Attendees);
    }

    [Fact]
    public void Orders_utterances_drops_blank_ones_and_keeps_speakers_anonymous()
    {
        var segments = WhisperAiMapper.ToFetch(Transcript(), Created).Transcript!.Segments;

        Assert.Equal(["Hello there.", "Hi."], segments.Select(s => s.Text));
        Assert.Null(segments[0].Speaker!.Name);
        Assert.Equal("Speaker A", segments[0].Speaker!.DiarizationLabel);
        Assert.Equal(Created.AddMilliseconds(240), segments[0].StartTime);
        Assert.Equal(Created.AddMilliseconds(2560), segments[0].EndTime);
    }

    [Fact]
    public void Falls_back_to_sentences_then_plain_text()
    {
        var sentences = WhisperAiMapper.ToFetch(Transcript() with
        {
            Utterances = null,
            Sentences = [new() { Text = "Hello there.", Start = 240, End = 2560 }],
        }, Created).Transcript!.Segments;
        Assert.Equal("Hello there.", Assert.Single(sentences).Text);
        Assert.Null(sentences[0].Speaker);

        var text = WhisperAiMapper.ToFetch(Transcript() with { Utterances = null }, null).Transcript!.Segments;
        Assert.Equal("Hello there. Hi.", Assert.Single(text).Text);
    }

    [Fact]
    public void Unfinished_transcript_is_a_failed_retrieval_not_an_empty_transcript()
    {
        var result = WhisperAiMapper.ToFetch(Transcript() with { Status = "error", ErrorCode = "no_speech_detected" }, Created).Transcript!;
        Assert.False(result.Success);
        Assert.Equal(ErrorCategory.TranscriptUnavailable, result.ErrorCategory);
        Assert.Contains("no_speech_detected", result.Error);
    }

    [Theory]
    [InlineData("https://api.whisperai.com/v1/uploads/up_8f1c", WhisperAiMapper.DefaultTitle)]
    [InlineData(null, WhisperAiMapper.DefaultTitle)]
    [InlineData("https://example.com/", WhisperAiMapper.DefaultTitle)]
    [InlineData("https://example.com/a/weekly-sync.m4a", "weekly-sync")]
    public void Title_comes_from_the_audio_file_name(string? url, string expected) =>
        Assert.Equal(expected, WhisperAiMapper.Title(url));

    [Fact]
    public void Export_front_matter_identifies_whisperai_files()
    {
        Assert.Equal("whisperai_transcript_id", MarkdownRenderer.IdKey(MeetingSource.WhisperAi));
        Assert.Equal("WhisperAI", MarkdownRenderer.SourceName(MeetingSource.WhisperAi));
    }
}

public class WhisperAiApiClientTests
{
    private sealed class ScriptedHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        private int _i;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(responses[Math.Min(_i++, responses.Length - 1)](request));
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static WhisperAiApiClient Client(ScriptedHandler handler, string? key = "wai_test_key_1234567")
    {
        var secrets = Substitute.For<ISecretProvider>();
        secrets.Get(SecretNames.WhisperAiApiKey).Returns(key is null ? null : new ResolvedSecret(key, "test"));
        var settings = Substitute.For<ISettingsService>();
        settings.GetSectionAsync<SyncSettings>(SettingKeys.Sync, Arg.Any<CancellationToken>()).Returns(new SyncSettings { MaxRequestAttempts = 3 });
        return new WhisperAiApiClient(new HttpClient(handler), secrets, settings,
            new WhisperAiRequestThrottle { MinInterval = TimeSpan.Zero }, NullLogger<WhisperAiApiClient>.Instance);
    }

    [Fact]
    public async Task Lists_with_raw_authorization_header_and_before_id_cursor()
    {
        var h = new ScriptedHandler(_ => Json("""
            {"page_details":{"limit":100,"result_count":1,"current_url":"x","prev_url":null,"next_url":"https://api.whisperai.com/v1/transcript?before_id=tr_A"},
             "transcripts":[{"id":"tr_A","status":"completed","created":"2026-10-21T14:00:00.123456","completed":null,"audio_url":null}]}
            """));
        var page = await Client(h).ListTranscriptsAsync("tr_B", 100, CancellationToken.None);

        var item = Assert.Single(page.Transcripts!);
        Assert.True(page.HasMore);
        // A timestamp without an offset is UTC.
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 14, 0, 0, 123, TimeSpan.Zero).AddTicks(4560), item.CreatedAt);
        var req = h.Requests.Single();
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Equal("https://api.whisperai.com/v1/transcript?limit=100&before_id=tr_B", req.RequestUri!.ToString());
        Assert.Equal("wai_test_key_1234567", Assert.Single(req.Headers.GetValues("Authorization")));
    }

    [Fact]
    public async Task Completed_transcript_without_utterances_gets_its_sentences()
    {
        var h = new ScriptedHandler(
            _ => Json("""{"id":"tr_A","status":"completed","text":"Hi there.","audio_duration":"12.5","utterances":null}"""),
            _ => Json("""{"id":"tr_A","sentences":[{"text":"Hi there.","start":10,"end":900,"confidence":0.98}]}"""));
        var t = await Client(h).GetTranscriptAsync("tr_A", CancellationToken.None);

        Assert.NotNull(t);
        Assert.Equal(12.5, t.AudioDuration);
        Assert.Equal("Hi there.", Assert.Single(t.Sentences!).Text);
        Assert.Equal("https://api.whisperai.com/v1/transcript/tr_A/sentences", h.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task Transcript_with_utterances_needs_one_request()
    {
        var h = new ScriptedHandler(_ => Json("""{"id":"tr_A","status":"completed","utterances":[{"speaker":"A","text":"Hi","start":0,"end":500}]}"""));
        var t = await Client(h).GetTranscriptAsync("tr_A", CancellationToken.None);
        Assert.Equal("A", Assert.Single(t!.Utterances!).Speaker);
        Assert.Single(h.Requests);
    }

    [Fact]
    public async Task Unauthorized_maps_to_authentication_without_retry_or_leaking_the_key()
    {
        var h = new ScriptedHandler(_ => Json("""{"error":"Invalid API key","status":"error"}""", HttpStatusCode.Unauthorized));
        var ex = await Assert.ThrowsAsync<WhisperAiApiException>(() => Client(h).ListTranscriptsAsync(null, 10, CancellationToken.None));
        Assert.Equal(ErrorCategory.Authentication, ex.Category);
        Assert.Single(h.Requests);
        Assert.DoesNotContain("wai_test_key", ex.Message);
    }

    [Fact]
    public async Task Quota_exceeded_is_a_permission_error()
    {
        var h = new ScriptedHandler(_ => Json("""{"error":"Monthly usage quota exceeded","status":"error"}""", HttpStatusCode.PaymentRequired));
        var ex = await Assert.ThrowsAsync<WhisperAiApiException>(() => Client(h).ListTranscriptsAsync(null, 10, CancellationToken.None));
        Assert.Equal(ErrorCategory.Permission, ex.Category);
        Assert.Contains("quota", ex.Message);
    }

    [Fact]
    public async Task Not_found_returns_null()
    {
        var h = new ScriptedHandler(_ => Json("""{"error":"Transcript not found","status":"error"}""", HttpStatusCode.NotFound));
        Assert.Null(await Client(h).GetTranscriptAsync("tr_MISSING", CancellationToken.None));
    }

    [Fact]
    public async Task Rate_limit_is_retried_then_succeeds()
    {
        var h = new ScriptedHandler(
            _ =>
            {
                var r = Json("""{"error":"Too many requests","status":"error"}""", HttpStatusCode.TooManyRequests);
                r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                return r;
            },
            _ => Json("""{"page_details":{"next_url":null},"transcripts":[]}"""));
        var page = await Client(h).ListTranscriptsAsync(null, 10, CancellationToken.None);
        Assert.Empty(page.Transcripts!);
        Assert.False(page.HasMore);
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
    public void Log_redaction_covers_whisperai_keys() =>
        Assert.Equal("key [REDACTED] rejected", GranolaPI.Infrastructure.Logging.SecretRedactor.Redact("key wai_test_key_1234567 rejected"));

    [Fact]
    public async Task Rejects_unsafe_transcript_ids_before_calling_the_api()
    {
        var h = new ScriptedHandler(_ => Json("{}"));
        await Assert.ThrowsAsync<WhisperAiApiException>(() => Client(h).GetTranscriptAsync("../etc", CancellationToken.None));
        await Assert.ThrowsAsync<WhisperAiApiException>(() => Client(h).ListTranscriptsAsync("a&limit=1", 10, CancellationToken.None));
        Assert.Empty(h.Requests);
    }
}
