using GranolaPI.Application.Settings;
using GranolaPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GranolaPI.Application.Interfaces;

public interface IAppDbContext
{
    DbSet<Client> Clients { get; }
    DbSet<Project> Projects { get; }
    DbSet<ClassificationRule> ClassificationRules { get; }
    DbSet<Meeting> Meetings { get; }
    DbSet<MeetingParticipant> MeetingParticipants { get; }
    DbSet<MeetingProjectAssignment> MeetingProjectAssignments { get; }
    DbSet<ExportRecord> ExportRecords { get; }
    DbSet<SyncRun> SyncRuns { get; }
    DbSet<SyncRunItem> SyncRunItems { get; }
    DbSet<ApplicationSetting> ApplicationSettings { get; }
    DbSet<ClassificationReview> ClassificationReviews { get; }
    DbSet<SyncLease> SyncLeases { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Reads and writes typed settings sections stored in the ApplicationSettings table.</summary>
public interface ISettingsService
{
    Task<AppSettingsSnapshot> GetAsync(CancellationToken ct = default);
    Task<T> GetSectionAsync<T>(string key, CancellationToken ct = default) where T : new();
    Task SaveSectionAsync<T>(string key, T value, CancellationToken ct = default);
}

public static class SettingKeys
{
    public const string Export = "export";
    public const string Sync = "sync";
    public const string Classification = "classification";
    public const string Logging = "logging";
    public const string Checkpoint = "sync.checkpoint";
    public const string GranolaConnection = "granola.connection";
}

/// <summary>
/// Secure secret storage. On Windows this is Windows Credential Manager; elsewhere secrets come from
/// environment variables / a secrets manager and cannot be written by the app.
/// </summary>
public interface ICredentialStore
{
    string StoreName { get; }
    bool CanWrite { get; }
    string? Read(string name);
    void Write(string name, string secret);
    bool Delete(string name);
}

public sealed record ResolvedSecret(string Value, string Source);

public interface ISecretProvider
{
    /// <summary>Returns the secret and where it came from, or null if not configured.</summary>
    ResolvedSecret? Get(string name);
}

public static class SecretNames
{
    public const string GranolaApiKey = "GranolaApiKey";
    public const string AiApiKey = "AiApiKey";
}

/// <summary>Base directory that relative export roots and log directories resolve against.</summary>
public interface IAppPaths
{
    string BaseDirectory { get; }
}

public sealed class AppPaths(string baseDirectory) : IAppPaths
{
    public string BaseDirectory { get; } = baseDirectory;
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
