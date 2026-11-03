using Chroniq.Domain.Enums;

namespace Chroniq.Application.Synchronization;

public sealed record SyncRequest(
    SyncMode Mode,
    SyncTrigger Trigger,
    IReadOnlyList<Guid>? MeetingIds = null,
    bool Reclassify = false,
    bool Force = false)
{
    public Guid RunId { get; init; } = Guid.NewGuid();
}

public sealed class SyncAlreadyRunningException() : Exception("A synchronization is already running.");

/// <summary>Cross-process mutual exclusion for sync runs (database-backed).</summary>
public interface ISyncLease
{
    Task<bool> TryAcquireAsync(string owner, TimeSpan duration, CancellationToken ct);
    Task<bool> RenewAsync(string owner, TimeSpan duration, CancellationToken ct);
    Task ReleaseAsync(string owner, CancellationToken ct);
}

public sealed record SyncProgressSnapshot(
    bool IsRunning,
    Guid? RunId,
    SyncMode? Mode,
    SyncTrigger? Trigger,
    string Phase,
    int Discovered,
    int Queued,
    int Processed,
    int Failed,
    string? CurrentItem,
    DateTimeOffset? StartedAt,
    Guid? LastRunId,
    SyncRunStatus? LastRunStatus,
    DateTimeOffset? LastRunFinishedAt);

/// <summary>In-memory live progress of the current run (polled by the UI).</summary>
public sealed class SyncProgress
{
    private readonly object _gate = new();
    private SyncProgressSnapshot _snapshot = new(false, null, null, null, "Idle", 0, 0, 0, 0, null, null, null, null, null);
    private int _processed;
    private int _failed;
    private int _discovered;
    private int _queued;

    public SyncProgressSnapshot Snapshot
    {
        get
        {
            lock (_gate) return _snapshot with { Processed = _processed, Failed = _failed, Discovered = _discovered, Queued = _queued };
        }
    }

    public bool IsRunning { get { lock (_gate) return _snapshot.IsRunning; } }

    public bool TryBegin(SyncRequest r, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_snapshot.IsRunning) return false;
            _processed = _failed = _discovered = _queued = 0;
            _snapshot = _snapshot with { IsRunning = true, RunId = r.RunId, Mode = r.Mode, Trigger = r.Trigger, Phase = "Starting", CurrentItem = null, StartedAt = now };
            return true;
        }
    }

    public void Phase(string phase) { lock (_gate) _snapshot = _snapshot with { Phase = phase }; }
    public void Current(string? item) { lock (_gate) _snapshot = _snapshot with { CurrentItem = item }; }
    public void Discovered(int n = 1) => Interlocked.Add(ref _discovered, n);
    public void Queued(int n) => Interlocked.Exchange(ref _queued, n);
    public void Processed(bool failed)
    {
        Interlocked.Increment(ref _processed);
        if (failed) Interlocked.Increment(ref _failed);
    }

    public void End(SyncRunStatus status, DateTimeOffset now)
    {
        lock (_gate)
            _snapshot = _snapshot with { IsRunning = false, Phase = "Idle", CurrentItem = null, LastRunId = _snapshot.RunId, LastRunStatus = status, LastRunFinishedAt = now };
    }
}

/// <summary>Thread-safe counters copied onto the SyncRun at the end.</summary>
public sealed class SyncCounters
{
    public int NotesDiscovered;
    public int MeetingsProcessed;
    public int NotesExported;
    public int TranscriptsExported;
    public int FilesUpdated;
    public int MeetingsSkipped;
    public int ClassificationFailures;
    public int ApiFailures;
    public int FileSystemFailures;
    public int Conflicts;
}
