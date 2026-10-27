using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GranolaPI.Application.Interfaces;

namespace GranolaPI.IntegrationTests;

public class ApiIntegrationTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _f;

    public ApiIntegrationTests(AppFactory f) => _f = f;

    [Fact]
    public async Task Health_is_public_and_reports_status()
    {
        var health = await _f.CreateClient().GetFromJsonAsync<JsonElement>("/api/health");
        Assert.Equal("Healthy", health.GetProperty("status").GetString());
        Assert.True(health.GetProperty("database").GetBoolean());
    }

    [Fact]
    public async Task State_changing_requests_require_the_csrf_header()
    {
        var res = await _f.CreateClient().PostAsJsonAsync("/api/sync/start", new { mode = "Incremental" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _f.CreateClient().GetAsync("/api/dashboard")).StatusCode);
    }

    [Fact]
    public async Task Cross_origin_preflight_from_unknown_site_is_not_allowed()
    {
        var req = new HttpRequestMessage(HttpMethod.Options, "/api/settings");
        req.Headers.Add("Origin", "https://evil.example");
        req.Headers.Add("Access-Control-Request-Method", "PUT");
        req.Headers.Add("Access-Control-Request-Headers", "x-gpi-request,content-type");
        var res = await _f.CreateClient().SendAsync(req);
        Assert.False(res.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Credentials_are_masked_and_never_returned_in_full()
    {
        var api = _f.Api();
        var status = await (await api.PutAsJsonAsync("/api/settings/credentials/granola", new { apiKey = "grn_SuperSecretKey_ABCDEFG1234", validate = true })).Content.ReadAsStringAsync();
        Assert.DoesNotContain("SuperSecretKey", status);
        Assert.Contains("grn_", status);
        Assert.Contains("1234", status);
        var get = await api.GetStringAsync("/api/settings/credentials/granola");
        Assert.DoesNotContain("SuperSecretKey", get);
        var settings = await api.GetStringAsync("/api/settings");
        Assert.DoesNotContain("SuperSecretKey", settings);
        Assert.Equal("grn_SuperSecretKey_ABCDEFG1234", _f.Credentials.Read(SecretNames.GranolaApiKey));
    }

    [Fact]
    public async Task Invalid_key_input_is_rejected_with_problem_details()
    {
        var res = await _f.Api().PutAsJsonAsync("/api/settings/credentials/granola", new { apiKey = "short", validate = false });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Validation failed", problem.GetProperty("title").GetString());
        Assert.True(problem.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task Invalid_settings_are_rejected_with_field_errors()
    {
        var api = _f.Api();
        var current = await api.GetFromJsonAsync<JsonElement>("/api/settings");
        var export = JsonSerializer.Deserialize<Dictionary<string, object?>>(current.GetProperty("export").GetRawText())!;
        export["projectDirectoryTemplate"] = "../../outside/{ProjectSlug}";
        var res = await api.PutAsJsonAsync("/api/settings", new { export, sync = new { intervalMinutes = 1, maxConcurrency = 99, maxRequestAttempts = 3, maxMeetingRetries = 5, incrementalOverlapMinutes = 10 } });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("'..'", body);
        Assert.Contains("sync.intervalMinutes", body);
        Assert.Contains("sync.maxConcurrency", body);
    }

    [Fact]
    public async Task Settings_round_trip_and_resolve_paths()
    {
        var api = _f.Api();
        var current = await api.GetFromJsonAsync<JsonElement>("/api/settings");
        var sync = JsonSerializer.Deserialize<Dictionary<string, object?>>(current.GetProperty("sync").GetRawText())!;
        sync["intervalMinutes"] = 45;
        var res = await api.PutAsJsonAsync("/api/settings", new { sync });
        res.EnsureSuccessStatusCode();
        var updated = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(45, updated.GetProperty("sync").GetProperty("intervalMinutes").GetInt32());
        Assert.StartsWith(_f.BaseDir, updated.GetProperty("resolvedPaths").GetProperty("root").GetString());
    }

    [Fact]
    public async Task Export_browser_blocks_path_traversal()
    {
        var res = await _f.Api().GetAsync("/api/exports/file?root=root&path=..%2F..%2Fsecret.md");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Project_validation_and_unique_slugs()
    {
        var api = _f.Api();
        var bad = await api.PostAsJsonAsync("/api/projects", new { name = "", slug = "../x", status = "Active", defaultClassificationPriority = 1, rules = new[] { new { kind = "Email", value = "nope", enabled = true } } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var name = "Tanyo " + Guid.NewGuid().ToString("N")[..6];
        var a = await (await api.PostAsJsonAsync("/api/projects", new { name, status = "Active", defaultClassificationPriority = 1, rules = Array.Empty<object>() })).Content.ReadFromJsonAsync<JsonElement>();
        var b = await (await api.PostAsJsonAsync("/api/projects", new { name, status = "Active", defaultClassificationPriority = 1, rules = Array.Empty<object>() })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(a.GetProperty("slug").GetString(), b.GetProperty("slug").GetString());

        // Renaming keeps the same ID (history is not disconnected).
        var renamed = await (await api.PutAsJsonAsync($"/api/projects/{a.GetProperty("id")}", new { name = name + " Renamed", slug = a.GetProperty("slug").GetString(), status = "Active", defaultClassificationPriority = 1, rules = Array.Empty<object>() })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(a.GetProperty("id").GetString(), renamed.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Project_config_export_import_round_trip()
    {
        var api = _f.Api();
        await api.PostAsJsonAsync("/api/clients", new { name = "Client " + Guid.NewGuid().ToString("N")[..6], rules = new[] { new { kind = "EmailDomain", value = "@client.example", enabled = true } } });
        var exported = await api.GetFromJsonAsync<JsonElement>("/api/projects/config/export");
        var res = await api.PostAsJsonAsync("/api/projects/config/import", exported);
        res.EnsureSuccessStatusCode();
        var result = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, result.GetProperty("clientsCreated").GetInt32()); // idempotent: nothing duplicated
        Assert.Equal(0, result.GetProperty("projectsCreated").GetInt32());
    }

    [Fact]
    public async Task Unknown_meeting_returns_404_problem()
    {
        var res = await _f.Api().GetAsync($"/api/meetings/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Openapi_document_is_not_exposed_outside_development()
    {
        var res = await _f.CreateClient().GetAsync("/openapi/v1.json");
        Assert.DoesNotContain("\"openapi\"", await res.Content.ReadAsStringAsync());
    }
}
