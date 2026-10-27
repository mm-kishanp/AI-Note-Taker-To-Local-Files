using System.Text.Json;
using GranolaPI.Application.Interfaces;
using GranolaPI.Application.Meetings;
using GranolaPI.Application.Settings;
using GranolaPI.Domain.Entities;
using GranolaPI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GranolaPI.Infrastructure.Persistence;

/// <summary>
/// Typed settings sections stored as JSON rows. Missing sections/properties fall back to defaults, so
/// new settings added in later versions are backward compatible.
/// </summary>
public sealed class SettingsService(AppDbContext db) : ISettingsService
{
    public async Task<AppSettingsSnapshot> GetAsync(CancellationToken ct = default) => new(
        await GetSectionAsync<ExportSettings>(SettingKeys.Export, ct),
        await GetSectionAsync<SyncSettings>(SettingKeys.Sync, ct),
        SettingsValidator.Normalize(await GetSectionAsync<ClassificationSettings>(SettingKeys.Classification, ct)),
        await GetSectionAsync<LoggingSettings>(SettingKeys.Logging, ct));

    public async Task<T> GetSectionAsync<T>(string key, CancellationToken ct = default) where T : new()
    {
        var row = await db.ApplicationSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null) return new T();
        try
        {
            return JsonSerializer.Deserialize<T>(row.ValueJson, MeetingJson.Options) ?? new T();
        }
        catch (JsonException)
        {
            return new T();
        }
    }

    public async Task SaveSectionAsync<T>(string key, T value, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value, MeetingJson.Options);
        var row = await db.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null)
        {
            db.ApplicationSettings.Add(new ApplicationSetting { Key = key, ValueJson = json, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            row.ValueJson = json;
            row.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Atomic compare-and-set lease row; safe across processes sharing the same database.</summary>
public sealed class SqlSyncLease(IServiceProvider services) : Application.Synchronization.ISyncLease
{
    private const string LeaseId = "sync";

    public async Task<bool> TryAcquireAsync(string owner, TimeSpan duration, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var expires = now.Add(duration);
        try
        {
            if (!await db.SyncLeases.AnyAsync(l => l.Id == LeaseId, ct))
            {
                db.SyncLeases.Add(new SyncLease { Id = LeaseId, Owner = owner, ExpiresAt = expires });
                await db.SaveChangesAsync(ct);
                return true;
            }
        }
        catch (DbUpdateException)
        {
            // Another process inserted the row first; fall through to the conditional update.
            db.ChangeTracker.Clear();
        }
        var updated = await db.SyncLeases
            .Where(l => l.Id == LeaseId && (l.ExpiresAt < now || l.Owner == owner))
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.Owner, owner).SetProperty(l => l.ExpiresAt, expires), ct);
        return updated == 1;
    }

    public async Task<bool> RenewAsync(string owner, TimeSpan duration, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expires = DateTime.UtcNow.Add(duration);
        return await db.SyncLeases.Where(l => l.Id == LeaseId && l.Owner == owner)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresAt, expires), ct) == 1;
    }

    public async Task ReleaseAsync(string owner, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.SyncLeases.Where(l => l.Id == LeaseId && l.Owner == owner)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)), ct);
    }
}
