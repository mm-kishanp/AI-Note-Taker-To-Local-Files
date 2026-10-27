using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GranolaPI.Application.Granola;
using GranolaPI.Application.Interfaces;
using GranolaPI.Application.Synchronization;
using GranolaPI.Domain.Enums;
using GranolaPI.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GranolaPI.IntegrationTests;

/// <summary>Deterministic in-memory stand-in for the Granola API (only used in tests).</summary>
public sealed class FakeGranolaApi : IGranolaApiClient
{
    public ConcurrentDictionary<string, GranolaNote> Notes { get; } = new();
    public HashSet<string> TranscriptFailures { get; } = [];
    public ErrorCategory? FailEverythingWith { get; set; }
    public int? FailListingOnPage { get; set; }
    public int PageSize { get; set; } = 2;
    public int GetNoteCalls;
    public TaskCompletionSource? BlockGetNote { get; set; }

    public static readonly DateTimeOffset T0 = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    public GranolaNote Add(string suffix, string title, List<GranolaUser>? attendees = null, List<GranolaTranscriptSegment>? transcript = null, int hoursOffset = 0)
    {
        var id = "not_" + suffix.PadRight(14, 'x')[..14];
        var note = new GranolaNote
        {
            Id = id,
            Title = title,
            Owner = new GranolaUser("Owner Person", "owner@me.com"),
            CreatedAt = T0.AddHours(hoursOffset),
            UpdatedAt = T0.AddHours(hoursOffset + 1),
            WebUrl = "https://notes.granola.ai/d/" + id,
            Attendees = attendees ?? [new GranolaUser("Owner Person", "owner@me.com")],
            SummaryMarkdown = $"## Summary of {title}\n- point",
            PrivateNotesMarkdown = "own notes",
            Transcript = transcript ??
            [
                new() { Speaker = new() { Source = "microphone", Attribution = "me" }, Text = "Hello", StartTime = T0, EndTime = T0.AddSeconds(1) },
                new() { Speaker = new() { Source = "speaker", Attribution = "them", Name = "Alice Client" }, Text = "Hi", StartTime = T0.AddSeconds(3), EndTime = T0.AddSeconds(4) },
            ],
        };
        Notes[id] = note;
        return note;
    }

    public void Touch(string id, Func<GranolaNote, GranolaNote> change)
    {
        var n = change(Notes[id]);
        // Granola stamps edits with the real current time, which is what the incremental checkpoint compares against.
        Notes[id] = n with { UpdatedAt = DateTimeOffset.UtcNow };
    }

    private void ThrowIfFailing()
    {
        if (FailEverythingWith is { } c) throw new GranolaApiException(c, c == ErrorCategory.Authentication ? 401 : 500, $"fake {c}");
    }

    public Task<GranolaConnectionResult> TestConnectionAsync(CancellationToken ct) =>
        Task.FromResult(FailEverythingWith is { } c
            ? new GranolaConnectionResult(false, c, "fake failure", "test")
            : new GranolaConnectionResult(true, ErrorCategory.None, "ok", "test"));

    public Task<PagedResult<GranolaNoteSummary>> ListNotesAsync(NoteListQuery query, CancellationToken ct)
    {
        ThrowIfFailing();
        var page = string.IsNullOrEmpty(query.Cursor) ? 0 : int.Parse(query.Cursor);
        if (FailListingOnPage == page) throw new GranolaApiException(ErrorCategory.TransientApi, 503, "fake outage during pagination");
        var all = Notes.Values
            .Where(n => query.UpdatedAfter is null || n.UpdatedAt > query.UpdatedAfter)
            .Where(n => query.CreatedAfter is null || n.CreatedAt >= query.CreatedAfter)
            .OrderBy(n => n.CreatedAt).ToList();
        var items = all.Skip(page * PageSize).Take(PageSize)
            .Select(n => new GranolaNoteSummary { Id = n.Id, Title = n.Title, Owner = n.Owner, CreatedAt = n.CreatedAt, UpdatedAt = n.UpdatedAt, DeletedAt = n.DeletedAt })
            .ToList();
        var more = (page + 1) * PageSize < all.Count;
        return Task.FromResult(new PagedResult<GranolaNoteSummary>(items, more, more ? (page + 1).ToString() : null));
    }

    public async Task<GranolaNoteFetch?> GetNoteAsync(string noteId, bool includeTranscript, CancellationToken ct)
    {
        Interlocked.Increment(ref GetNoteCalls);
        if (BlockGetNote is { } gate) await gate.Task.WaitAsync(ct);
        ThrowIfFailing();
        if (!Notes.TryGetValue(noteId, out var n)) return null;
        var transcript = TranscriptFailures.Contains(noteId)
            ? new GranolaTranscriptResult(false, [], ErrorCategory.TransientApi, "fake transcript outage")
            : new GranolaTranscriptResult(true, n.Transcript ?? []);
        return new GranolaNoteFetch(n with { Transcript = null }, includeTranscript ? transcript : null);
    }

    public Task<GranolaTranscriptResult> GetTranscriptAsync(string noteId, CancellationToken ct) =>
        Task.FromResult(new GranolaTranscriptResult(true, Notes[noteId].Transcript ?? []));

    public Task<IReadOnlyList<GranolaFolder>> ListFoldersAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<GranolaFolder>>([]);
}

public sealed class AppFactory : WebApplicationFactory<Program>
{
    public string BaseDir { get; } = Path.Combine(Path.GetTempPath(), "gpi-it", Guid.NewGuid().ToString("N"));
    public FakeGranolaApi Granola { get; } = new();
    public InMemoryCredentialStore Credentials { get; } = new();
    public string ExportRoot => Path.Combine(BaseDir, "GranolaProjectKnowledge");

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public AppFactory()
    {
        Directory.CreateDirectory(BaseDir);
        Credentials.Write(SecretNames.GranolaApiKey, "grn_integration_test_key_000000");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("App:BaseDirectory", BaseDir);
        builder.UseSetting("App:DataDirectory", Path.Combine(BaseDir, "data"));
        builder.UseSetting("Worker:Enabled", "false");
        builder.UseSetting("Security:UseWindowsCredentialManager", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGranolaApiClient>();
            services.AddSingleton<IGranolaApiClient>(Granola);
            services.RemoveAll<ICredentialStore>();
            services.AddSingleton<ICredentialStore>(Credentials);
        });
    }

    public HttpClient Api()
    {
        var c = CreateClient();
        c.DefaultRequestHeaders.Add("X-GPI-Request", "1");
        return c;
    }

    public async Task<JsonElement> RunSync(HttpClient api, string mode = "Incremental", object? extra = null)
    {
        var body = extra ?? new { mode };
        var res = await api.PostAsJsonAsync("/api/sync/start", body, Json);
        Assert.Equal(System.Net.HttpStatusCode.Accepted, res.StatusCode);
        await Services.GetRequiredService<SyncCoordinator>().WaitAsync();
        return await LastRun(api);
    }

    public async Task WaitForSync() => await Services.GetRequiredService<SyncCoordinator>().WaitAsync();

    public static async Task<JsonElement> LastRun(HttpClient api) =>
        (await api.GetFromJsonAsync<JsonElement>("/api/sync/history?pageSize=1")).GetProperty("items")[0];

    public string[] Files(string relativeDir) =>
        Directory.Exists(Path.Combine(ExportRoot, relativeDir))
            ? Directory.GetFiles(Path.Combine(ExportRoot, relativeDir), "*.md", SearchOption.AllDirectories)
            : [];

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(BaseDir, true); } catch { /* best effort */ }
    }
}
