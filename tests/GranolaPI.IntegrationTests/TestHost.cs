using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GranolaPI.Application.Fireflies;
using GranolaPI.Application.Granola;
using GranolaPI.Application.Interfaces;
using GranolaPI.Application.Synchronization;
using GranolaPI.Application.WhisperAi;
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

/// <summary>Deterministic in-memory stand-in for the Fireflies API (only used in tests).</summary>
public sealed class FakeFirefliesApi : IFirefliesApiClient
{
    public ConcurrentDictionary<string, FirefliesTranscript> Calls { get; } = new();
    public ErrorCategory? FailEverythingWith { get; set; }
    public int GetTranscriptCalls;

    // Recent, so incremental runs (which look back a few days) still list these calls.
    public static readonly DateTimeOffset T0 = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).AddDays(-1).AddHours(14);

    public FirefliesTranscript Add(string id, string title, string attendeeEmail = "client@acme.com", bool withSummary = true, int hoursOffset = 0)
    {
        var call = new FirefliesTranscript
        {
            Id = id,
            Title = title,
            Date = T0.AddHours(hoursOffset).ToUnixTimeMilliseconds(),
            Duration = 30,
            TranscriptUrl = "https://app.fireflies.ai/view/" + id,
            OrganizerEmail = "owner@me.com",
            User = new FirefliesUser { Name = "Owner Person", Email = "owner@me.com" },
            Participants = ["owner@me.com," + attendeeEmail],
            MeetingAttendees = [new FirefliesAttendee { DisplayName = "Carol Client", Email = attendeeEmail }],
            Sentences =
            [
                new() { Index = 0, Text = "Welcome everyone.", StartTime = 0, EndTime = 2, SpeakerName = "Owner Person" },
                new() { Index = 1, Text = "Thanks, let's review the requirements.", StartTime = 3, EndTime = 6, SpeakerName = "Carol Client" },
                new() { Index = 2, Text = "Sounds good.", StartTime = 7, EndTime = 8, SpeakerName = "Speaker 3" },
            ],
            Summary = withSummary ? Summary(title) : null,
        };
        Calls[id] = call;
        return call;
    }

    public static FirefliesSummary Summary(string title) => new()
    {
        Overview = $"Overview of {title}.",
        ShorthandBullet = "- Requirements reviewed",
        ActionItems = "**Carol Client**\nSend the spec (00:05)",
        Keywords = ["requirements", "spec"],
    };

    private void ThrowIfFailing()
    {
        if (FailEverythingWith is { } c) throw new FirefliesApiException(c, c == ErrorCategory.Authentication ? 401 : 500, $"fake fireflies {c}");
    }

    public Task<GranolaConnectionResult> TestConnectionAsync(CancellationToken ct) =>
        Task.FromResult(FailEverythingWith is { } c
            ? new GranolaConnectionResult(false, c, "fake failure", "test")
            : new GranolaConnectionResult(true, ErrorCategory.None, "ok", "test"));

    public Task<IReadOnlyList<FirefliesTranscriptSummary>> ListTranscriptsAsync(DateTimeOffset? fromDate, int skip, int limit, CancellationToken ct)
    {
        ThrowIfFailing();
        IReadOnlyList<FirefliesTranscriptSummary> page = Calls.Values
            .Where(c => fromDate is null || c.Date >= fromDate.Value.ToUnixTimeMilliseconds())
            .OrderByDescending(c => c.Date)
            .Skip(skip).Take(limit)
            .Select(c => new FirefliesTranscriptSummary { Id = c.Id, Title = c.Title, Date = c.Date })
            .ToList();
        return Task.FromResult(page);
    }

    public Task<FirefliesTranscript?> GetTranscriptAsync(string transcriptId, CancellationToken ct)
    {
        Interlocked.Increment(ref GetTranscriptCalls);
        ThrowIfFailing();
        return Task.FromResult(Calls.TryGetValue(transcriptId, out var c) ? c : null);
    }
}

/// <summary>Deterministic in-memory stand-in for the WhisperAI API (only used in tests).</summary>
public sealed class FakeWhisperAiApi : IWhisperAiApiClient
{
    public ConcurrentDictionary<string, (WhisperAiTranscript Transcript, DateTimeOffset Created)> Transcripts { get; } = new();
    public ErrorCategory? FailEverythingWith { get; set; }
    public int PageSize { get; set; } = 2;
    public int GetTranscriptCalls;
    public int ListCalls;

    // Recent, so incremental runs (which look back a day) still list these transcripts.
    public static readonly DateTimeOffset T0 = DateTimeOffset.UtcNow.AddHours(-2);

    public WhisperAiTranscript Add(string id, string audioName, string status = "completed", int minutesOffset = 0)
    {
        var t = new WhisperAiTranscript
        {
            Id = id,
            Status = status,
            AudioUrl = $"https://files.example.com/{audioName}.mp3?sig=private",
            Text = "Welcome everyone. Let's review the requirements.",
            Utterances =
            [
                new() { Speaker = "A", Text = "Welcome everyone.", Start = 0, End = 2000 },
                new() { Speaker = "B", Text = "Let's review the requirements.", Start = 3000, End = 6000 },
            ],
        };
        Transcripts[id] = (t, T0.AddMinutes(minutesOffset));
        return t;
    }

    private void ThrowIfFailing()
    {
        if (FailEverythingWith is { } c) throw new WhisperAiApiException(c, c == ErrorCategory.Authentication ? 401 : 500, $"fake whisperai {c}");
    }

    public Task<GranolaConnectionResult> TestConnectionAsync(CancellationToken ct) =>
        Task.FromResult(FailEverythingWith is { } c
            ? new GranolaConnectionResult(false, c, "fake failure", "test")
            : new GranolaConnectionResult(true, ErrorCategory.None, "ok", "test"));

    public Task<WhisperAiTranscriptList> ListTranscriptsAsync(string? beforeId, int limit, CancellationToken ct)
    {
        Interlocked.Increment(ref ListCalls);
        ThrowIfFailing();
        var all = Transcripts.Values.OrderByDescending(x => x.Created).ThenByDescending(x => x.Transcript.Id).ToList();
        var start = beforeId is null ? 0 : all.FindIndex(x => x.Transcript.Id == beforeId) + 1;
        var page = all.Skip(start).Take(Math.Min(limit, PageSize)).ToList();
        var more = start + page.Count < all.Count;
        return Task.FromResult(new WhisperAiTranscriptList
        {
            PageDetails = new WhisperAiPageDetails { NextUrl = more ? "https://api.whisperai.com/v1/transcript?before_id=" + page[^1].Transcript.Id : null },
            Transcripts = page.Select(x => new WhisperAiTranscriptListItem
            {
                Id = x.Transcript.Id,
                Status = x.Transcript.Status,
                Created = x.Created.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffff"),
                AudioUrl = x.Transcript.AudioUrl,
            }).ToList(),
        });
    }

    public Task<WhisperAiTranscript?> GetTranscriptAsync(string transcriptId, CancellationToken ct)
    {
        Interlocked.Increment(ref GetTranscriptCalls);
        ThrowIfFailing();
        return Task.FromResult(Transcripts.TryGetValue(transcriptId, out var x) ? x.Transcript : null);
    }
}

public sealed class AppFactory : WebApplicationFactory<Program>
{
    public string BaseDir { get; } = Path.Combine(Path.GetTempPath(), "gpi-it", Guid.NewGuid().ToString("N"));
    public FakeGranolaApi Granola { get; } = new();
    public FakeFirefliesApi Fireflies { get; } = new();
    public FakeWhisperAiApi WhisperAi { get; } = new();
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
            services.RemoveAll<IFirefliesApiClient>();
            services.AddSingleton<IFirefliesApiClient>(Fireflies);
            services.RemoveAll<IWhisperAiApiClient>();
            services.AddSingleton<IWhisperAiApiClient>(WhisperAi);
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
