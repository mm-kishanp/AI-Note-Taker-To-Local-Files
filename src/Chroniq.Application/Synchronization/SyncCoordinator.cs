using Chroniq.Application.Interfaces;
using Chroniq.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Chroniq.Application.Synchronization;

public sealed record SyncStartResult(bool Started, Guid? RunId, string Message);

/// <summary>
/// Starts runs in the background so the API and UI stay responsive, prevents overlapping runs in
/// this process (the database lease covers other processes) and supports cancellation.
/// </summary>
public sealed class SyncCoordinator(SyncEngine engine, SyncProgress progress, IClock clock, ILogger<SyncCoordinator> logger)
{
    private CancellationTokenSource? _cts;
    private Task? _current;

    public bool IsRunning => progress.IsRunning;

    public SyncStartResult TryStart(SyncRequest request)
    {
        if (!progress.TryBegin(request, clock.UtcNow))
            return new SyncStartResult(false, progress.Snapshot.RunId, "A synchronization is already running.");

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _current = Task.Run(async () =>
        {
            var status = SyncRunStatus.Failed;
            try
            {
                var run = await engine.RunAsync(request, token);
                status = run.Status;
            }
            catch (SyncAlreadyRunningException)
            {
                logger.LogInformation("Sync skipped: another process holds the sync lease.");
                status = SyncRunStatus.Cancelled;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync run crashed");
            }
            finally
            {
                progress.End(status, clock.UtcNow);
            }
        }, CancellationToken.None);
        return new SyncStartResult(true, request.RunId, "Synchronization started.");
    }

    public bool Cancel()
    {
        if (!progress.IsRunning || _cts is null) return false;
        _cts.Cancel();
        return true;
    }

    /// <summary>Waits for the current run (used by tests and graceful shutdown).</summary>
    public Task WaitAsync() => _current ?? Task.CompletedTask;
}
