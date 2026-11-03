using Chroniq.Application.Interfaces;
using Chroniq.Application.Meetings;
using Chroniq.Application.Settings;
using Chroniq.Application.Synchronization;
using Chroniq.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chroniq.Worker;

/// <summary>
/// Scheduled synchronization. Runs only when enabled in settings and a Granola key is configured,
/// never overlaps with another run (in-process check plus the database lease), and resumes safely
/// after restarts because all progress lives in the database.
/// </summary>
public sealed class SynchronizationWorker(
    IServiceScopeFactory scopes,
    SyncCoordinator coordinator,
    ILogger<SynchronizationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverAsync(stoppingToken);

        using var timer = new PeriodicTimer(Tick);
        do
        {
            try
            {
                await MaybeStartAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Scheduled sync check failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        coordinator.Cancel();
        await Task.WhenAny(coordinator.WaitAsync(), Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));
        await base.StopAsync(cancellationToken);
    }

    private async Task RecoverAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var history = scope.ServiceProvider.GetRequiredService<SyncHistoryService>();
        var n = await history.MarkInterruptedAsync(ct);
        if (n > 0) logger.LogWarning("Marked {Count} unfinished sync run(s) as interrupted; pending meetings will be retried.", n);
    }

    private async Task MaybeStartAsync(CancellationToken ct)
    {
        if (coordinator.IsRunning) return;
        using var scope = scopes.CreateScope();
        var settings = await scope.ServiceProvider.GetRequiredService<ISettingsService>().GetSectionAsync<SyncSettings>(SettingKeys.Sync, ct);
        if (!settings.AutoSyncEnabled) return;
        if (scope.ServiceProvider.GetRequiredService<ISecretProvider>().Get(SecretNames.GranolaApiKey) is null) return;

        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var last = await db.SyncRuns.AsNoTracking()
            .Where(r => r.Trigger == SyncTrigger.Scheduled || r.Trigger == SyncTrigger.Manual)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => new { r.StartedAt, r.FinishedAt })
            .FirstOrDefaultAsync(ct);
        var interval = TimeSpan.FromMinutes(Math.Max(5, settings.IntervalMinutes));
        var due = last is null || (last.FinishedAt ?? last.StartedAt) + interval <= DateTime.UtcNow;
        if (!due) return;

        var result = coordinator.TryStart(new SyncRequest(SyncMode.Incremental, SyncTrigger.Scheduled));
        if (result.Started) logger.LogInformation("Scheduled sync {RunId} started", result.RunId);
    }
}

public static class WorkerRegistration
{
    public static IServiceCollection AddSynchronizationWorker(this IServiceCollection services) =>
        services.AddHostedService<SynchronizationWorker>();
}
