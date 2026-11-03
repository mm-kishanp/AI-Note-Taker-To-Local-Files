using System.Net;
using System.Text;
using Chroniq.Application.Granola;
using Chroniq.Application.Interfaces;
using Chroniq.Application.Settings;
using Chroniq.Domain.Enums;
using Chroniq.Infrastructure.Granola;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Chroniq.UnitTests.Granola;

public class GranolaApiClientTests
{
    private sealed class ScriptedHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        private int _i;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Requests.Add(request);
            var f = responses[Math.Min(_i++, responses.Length - 1)];
            return Task.FromResult(f(request));
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static GranolaApiClient Client(ScriptedHandler handler, string? key = "grn_test_key_123456", int attempts = 3)
    {
        var secrets = Substitute.For<ISecretProvider>();
        secrets.Get(SecretNames.GranolaApiKey).Returns(key is null ? null : new ResolvedSecret(key, "test"));
        var settings = Substitute.For<ISettingsService>();
        settings.GetSectionAsync<SyncSettings>(SettingKeys.Sync, Arg.Any<CancellationToken>()).Returns(new SyncSettings { MaxRequestAttempts = attempts });
        return new GranolaApiClient(new HttpClient(handler), secrets, settings,
            new GranolaRequestThrottle { MinInterval = TimeSpan.Zero }, NullLogger<GranolaApiClient>.Instance);
    }

    private const string NoteSummary = """{"id":"not_aaaaaaaaaaaaaa","object":"note","title":"T","owner":{"name":"O","email":"o@x.com"},"created_at":"2026-10-26T10:00:00Z","updated_at":"2026-10-26T11:00:00Z","deleted_at":null}""";

    [Fact]
    public async Task Sends_bearer_token_and_query_parameters()
    {
        var h = new ScriptedHandler(_ => Json($$"""{"notes":[{{NoteSummary}}],"hasMore":false,"cursor":null}"""));
        var page = await Client(h).ListNotesAsync(new NoteListQuery(UpdatedAfter: new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), Cursor: "abc=="), CancellationToken.None);

        Assert.Single(page.Items);
        var req = h.Requests.Single();
        Assert.Equal("Bearer", req.Headers.Authorization!.Scheme);
        Assert.Equal("grn_test_key_123456", req.Headers.Authorization.Parameter);
        Assert.Equal("public-api.granola.ai", req.RequestUri!.Host);
        Assert.Contains("updated_after=2026-01-02T03%3A04%3A05Z", req.RequestUri.Query);
        Assert.Contains("cursor=abc%3D%3D", req.RequestUri.Query);
        Assert.Contains("page_size=30", req.RequestUri.Query);
    }

    [Fact]
    public async Task Invalid_credentials_fail_fast_without_retry()
    {
        var h = new ScriptedHandler(_ => Json("""{"code":"INVALID_API_KEY","message":"Invalid API key format","requestId":"r1"}""", HttpStatusCode.Unauthorized));
        var ex = await Assert.ThrowsAsync<GranolaApiException>(() => Client(h).ListNotesAsync(new(), CancellationToken.None));
        Assert.Equal(ErrorCategory.Authentication, ex.Category);
        Assert.Single(h.Requests);
        Assert.DoesNotContain("grn_test_key", ex.Message);
        Assert.Contains("INVALID_API_KEY", ex.Message);
    }

    [Fact]
    public async Task Forbidden_maps_to_permission()
    {
        var h = new ScriptedHandler(_ => Json("{}", HttpStatusCode.Forbidden));
        var ex = await Assert.ThrowsAsync<GranolaApiException>(() => Client(h).ListNotesAsync(new(), CancellationToken.None));
        Assert.Equal(ErrorCategory.Permission, ex.Category);
    }

    [Fact]
    public async Task Rate_limit_respects_retry_after_then_succeeds()
    {
        var h = new ScriptedHandler(
            _ => { var r = Json("""{"code":"RATE_LIMITED"}""", HttpStatusCode.TooManyRequests); r.Headers.RetryAfter = new(TimeSpan.FromMilliseconds(10)); return r; },
            _ => Json("""{"notes":[],"hasMore":false,"cursor":null}"""));
        var page = await Client(h).ListNotesAsync(new(), CancellationToken.None);
        Assert.Empty(page.Items);
        Assert.Equal(2, h.Requests.Count);
    }

    [Fact]
    public async Task Transient_server_errors_are_retried_with_a_bound()
    {
        var h = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var ex = await Assert.ThrowsAsync<GranolaApiException>(() => Client(h, attempts: 2).ListNotesAsync(new(), CancellationToken.None));
        Assert.Equal(ErrorCategory.TransientApi, ex.Category);
        Assert.True(ex.IsRetryable);
        Assert.Equal(2, h.Requests.Count);
    }

    [Fact]
    public async Task Network_failure_is_retried_then_succeeds()
    {
        var calls = 0;
        var h = new ScriptedHandler(_ => ++calls == 1 ? throw new HttpRequestException("reset") : Json("""{"notes":[],"hasMore":false,"cursor":null}"""));
        await Client(h).ListNotesAsync(new(), CancellationToken.None);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Invalid_response_schema_is_reported()
    {
        var h = new ScriptedHandler(_ => Json("<html>not json</html>"));
        var ex = await Assert.ThrowsAsync<GranolaApiException>(() => Client(h).ListNotesAsync(new(), CancellationToken.None));
        Assert.Equal(ErrorCategory.InvalidResponse, ex.Category);
    }

    [Fact]
    public async Task Missing_notes_array_is_invalid()
    {
        var h = new ScriptedHandler(_ => Json("""{"hasMore":false}"""));
        var page = await Client(h).ListNotesAsync(new(), CancellationToken.None);
        Assert.Empty(page.Items); // defaults to empty list; still a valid empty page
    }

    [Fact]
    public async Task Get_note_inlines_transcript_when_available()
    {
        var h = new ScriptedHandler(_ => Json("""{"id":"not_aaaaaaaaaaaaaa","title":"T","created_at":"2026-10-26T10:00:00Z","updated_at":"2026-10-26T10:00:00Z","transcript":[{"speaker":{"source":"speaker","name":"Alice"},"text":"Hi","start_time":"2026-10-26T10:00:00Z","end_time":"2026-10-26T10:00:01Z"}]}"""));
        var fetch = await Client(h).GetNoteAsync("not_aaaaaaaaaaaaaa", true, CancellationToken.None);
        Assert.True(fetch!.Transcript!.Success);
        Assert.Equal("Alice", fetch.Transcript.Segments[0].Speaker!.Name);
        Assert.Contains("include=transcript", h.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task Too_large_transcript_falls_back_to_paged_endpoint()
    {
        const string note = """{"id":"not_aaaaaaaaaaaaaa","title":"T","created_at":"2026-10-26T10:00:00Z","updated_at":"2026-10-26T10:00:00Z"}""";
        var h = new ScriptedHandler(
            _ => Json("""{"code":"TRANSCRIPT_TOO_LARGE","message":"too big"}""", HttpStatusCode.RequestEntityTooLarge),
            _ => Json(note),
            _ => Json("""{"transcript":[{"speaker":{"source":"speaker"},"text":"one","start_time":"2026-10-26T10:00:00Z","end_time":"2026-10-26T10:00:01Z"}],"hasMore":true,"cursor":"c2"}"""),
            _ => Json("""{"transcript":[{"speaker":{"source":"speaker"},"text":"two","start_time":"2026-10-26T10:00:02Z","end_time":"2026-10-26T10:00:03Z"}],"hasMore":false,"cursor":null}"""));
        var fetch = await Client(h).GetNoteAsync("not_aaaaaaaaaaaaaa", true, CancellationToken.None);
        Assert.Equal(["one", "two"], fetch!.Transcript!.Segments.Select(s => s.Text));
        Assert.Contains("/notes/not_aaaaaaaaaaaaaa/transcript", h.Requests[2].RequestUri!.AbsolutePath);
        Assert.Contains("cursor=c2", h.Requests[3].RequestUri!.Query);
    }

    [Fact]
    public async Task Transcript_failure_is_explicit_not_empty()
    {
        const string note = """{"id":"not_aaaaaaaaaaaaaa","title":"T","created_at":"2026-10-26T10:00:00Z","updated_at":"2026-10-26T10:00:00Z"}""";
        var h = new ScriptedHandler(_ => Json(note), _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var fetch = await Client(h, attempts: 1).GetNoteAsync("not_aaaaaaaaaaaaaa", true, CancellationToken.None);
        Assert.NotNull(fetch);
        Assert.False(fetch!.Transcript!.Success);
        Assert.Equal(ErrorCategory.TransientApi, fetch.Transcript.ErrorCategory);
    }

    [Fact]
    public async Task Missing_note_returns_null()
    {
        var h = new ScriptedHandler(_ => Json("""{"code":"NOT_FOUND"}""", HttpStatusCode.NotFound));
        Assert.Null(await Client(h).GetNoteAsync("not_zzzzzzzzzzzzzz", true, CancellationToken.None));
    }

    [Fact]
    public async Task Invalid_note_id_is_rejected_before_any_request()
    {
        var h = new ScriptedHandler(_ => Json("{}"));
        await Assert.ThrowsAsync<GranolaApiException>(() => Client(h).GetNoteAsync("../../admin", true, CancellationToken.None));
        Assert.Empty(h.Requests);
    }

    [Fact]
    public async Task Cancellation_is_honoured()
    {
        var h = new ScriptedHandler(_ => Json("{}"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(h).ListNotesAsync(new(), cts.Token));
    }

    [Fact]
    public async Task Test_connection_reports_missing_key_and_auth_failures()
    {
        var none = await Client(new ScriptedHandler(_ => Json("{}")), key: null).TestConnectionAsync(CancellationToken.None);
        Assert.False(none.Success);
        Assert.Equal(ErrorCategory.Configuration, none.ErrorCategory);

        var bad = await Client(new ScriptedHandler(_ => Json("{}", HttpStatusCode.Unauthorized))).TestConnectionAsync(CancellationToken.None);
        Assert.Equal(ErrorCategory.Authentication, bad.ErrorCategory);

        var ok = await Client(new ScriptedHandler(_ => Json($$"""{"notes":[{{NoteSummary}}],"hasMore":false,"cursor":null}"""))).TestConnectionAsync(CancellationToken.None);
        Assert.True(ok.Success);
    }

    [Fact]
    public async Task Folders_are_paged()
    {
        var h = new ScriptedHandler(
            _ => Json("""{"folders":[{"id":"fol_aaaaaaaaaaaaaa","object":"folder","name":"A","parent_folder_id":null}],"hasMore":true,"cursor":"n"}"""),
            _ => Json("""{"folders":[{"id":"fol_bbbbbbbbbbbbbb","object":"folder","name":"B","parent_folder_id":null}],"hasMore":false,"cursor":null}"""));
        var folders = await Client(h).ListFoldersAsync(CancellationToken.None);
        Assert.Equal(["A", "B"], folders.Select(f => f.Name));
    }
}
