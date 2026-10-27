using GranolaPI.Application.Export;
using GranolaPI.Application.Granola;
using GranolaPI.Application.Interfaces;
using GranolaPI.Application.Meetings;
using GranolaPI.Application.Settings;
using GranolaPI.Domain.Entities;
using GranolaPI.Domain.Enums;
using GranolaPI.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GranolaPI.UnitTests;

/// <summary>In-memory SQLite database (real relational behaviour, no files) plus a temp export folder.</summary>
public sealed class TestEnv : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestEnv()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        Db = NewContext();
        Db.Database.EnsureCreated();
        Root = Path.Combine(Path.GetTempPath(), "gpi-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public AppDbContext Db { get; private set; }
    public string Root { get; }
    public FixedClock Clock { get; } = new(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));

    public AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    /// <summary>Fresh context, as a new request/sync scope would get.</summary>
    public void ResetContext()
    {
        Db.Dispose();
        Db = NewContext();
    }

    public ExportLayout Layout(ExportSettings? s = null) => new(s ?? new ExportSettings { RootDirectory = Root }, Root);

    public MeetingExporter Exporter() => new(Db, Clock, NullLogger<MeetingExporter>.Instance);

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
        try { Directory.Delete(Root, recursive: true); } catch { /* best effort */ }
    }
}

public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

public static class Fixtures
{
    public static GranolaTranscriptSegment Seg(string? name, string text, int second, string attribution = "them") => new()
    {
        Speaker = new GranolaSpeaker { Source = attribution == "me" ? "microphone" : "speaker", Attribution = attribution, Name = name },
        Text = text,
        StartTime = new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.Zero).AddSeconds(second),
        EndTime = new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.Zero).AddSeconds(second + 2),
    };

    public static GranolaNote Note(string id = "not_aaaaaaaaaaaaaa", string title = "Client Requirement Discussion", DateTimeOffset? updated = null,
        List<GranolaTranscriptSegment>? transcript = null, List<GranolaUser>? attendees = null, string? summary = "## Decisions\n- Ship v2") => new()
        {
            Id = id,
            Title = title,
            Owner = new GranolaUser("Kishan Parmar", "owner@me.com"),
            CreatedAt = new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.Zero),
            UpdatedAt = updated ?? new DateTimeOffset(2026, 10, 26, 11, 0, 0, TimeSpan.Zero),
            WebUrl = $"https://notes.granola.ai/d/{id}",
            Attendees = attendees ?? [new GranolaUser("Alice Client", "alice@client-a.com"), new GranolaUser("Kishan Parmar", "owner@me.com")],
            SummaryMarkdown = summary,
            PrivateNotesMarkdown = "ask about budget",
            Transcript = transcript ?? [Seg(null, "Hello everyone", 0, "me"), Seg("Alice Client", "Hi, let's start.", 4), Seg("Alice Client", "Requirements first.", 8)],
        };

    /// <summary>Creates and saves a meeting as the sync pipeline would after fetching.</summary>
    public static Meeting AddMeeting(TestEnv env, GranolaNote? note = null, bool transcriptOk = true,
        ClassificationStatus status = ClassificationStatus.Unassigned, Guid? projectId = null)
    {
        note ??= Note();
        var m = new Meeting();
        var transcript = transcriptOk
            ? new GranolaTranscriptResult(true, note.Transcript ?? [])
            : new GranolaTranscriptResult(false, [], ErrorCategory.TransientApi, "boom");
        MeetingIngestor.Apply(m, new GranolaNoteFetch(note, transcript), env.Clock.UtcNow);
        m.ClassificationStatus = status;
        m.AssignedProjectId = projectId;
        env.Db.Meetings.Add(m);
        env.Db.SaveChanges();
        return m;
    }

    public static Project AddProject(TestEnv env, string name, string slug)
    {
        var p = new Project { Name = name, Slug = slug };
        env.Db.Projects.Add(p);
        env.Db.SaveChanges();
        return p;
    }

    public static Dictionary<Guid, ProjectInfo> Infos(TestEnv env) =>
        env.Db.Projects.Include(p => p.Client).ToDictionary(p => p.Id, ProjectInfo.From);
}
