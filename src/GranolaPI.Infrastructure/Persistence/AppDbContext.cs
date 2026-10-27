using GranolaPI.Application.Interfaces;
using GranolaPI.Domain.Entities;
using GranolaPI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GranolaPI.Infrastructure.Persistence;

/// <summary>
/// Operational state only (meeting metadata, mappings, export records, sync history). Secrets are never
/// stored here. The schema avoids SQLite-specific features so SQL Server/PostgreSQL providers can be swapped in.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ClassificationRule> ClassificationRules => Set<ClassificationRule>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingParticipant> MeetingParticipants => Set<MeetingParticipant>();
    public DbSet<MeetingProjectAssignment> MeetingProjectAssignments => Set<MeetingProjectAssignment>();
    public DbSet<ExportRecord> ExportRecords => Set<ExportRecord>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();
    public DbSet<SyncRunItem> SyncRunItems => Set<SyncRunItem>();
    public DbSet<ApplicationSetting> ApplicationSettings => Set<ApplicationSetting>();
    public DbSet<ClassificationReview> ClassificationReviews => Set<ClassificationReview>();
    public DbSet<SyncLease> SyncLeases => Set<SyncLease>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Store enums as strings: readable in the database and stable if enum values are reordered.
        builder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
        // All timestamps are UTC; restore Kind=Utc when reading.
        builder.Properties<DateTime>().HaveConversion<UtcConverter>();
        builder.Properties<DateTime?>().HaveConversion<NullableUtcConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        // IDs are generated in code (Guid.NewGuid()). Marking them as never store-generated makes EF
        // insert new child entities added through navigations instead of treating them as existing rows.
        foreach (var entity in b.Model.GetEntityTypes())
        {
            var id = entity.FindProperty("Id");
            if (id?.ClrType == typeof(Guid)) id.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
        }

        b.Entity<Client>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Project>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.Status);
            e.Property(x => x.NotesFolderPath).HasMaxLength(400);
            e.Property(x => x.TranscriptFolderPath).HasMaxLength(400);
            e.HasOne(x => x.Client).WithMany(c => c.Projects).HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<ClassificationRule>(e =>
        {
            e.Property(x => x.Value).HasMaxLength(320).IsRequired();
            e.Property(x => x.NormalizedValue).HasMaxLength(320).IsRequired();
            e.HasOne(x => x.Project).WithMany(p => p.Rules).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Client).WithMany(c => c.Rules).HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.Kind, x.NormalizedValue });
            e.ToTable(t => t.HasCheckConstraint("CK_Rule_Owner", "(\"ProjectId\" IS NULL) <> (\"ClientId\" IS NULL)"));
        });

        b.Entity<Meeting>(e =>
        {
            e.Property(x => x.GranolaNoteId).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.GranolaNoteId).IsUnique();
            e.Property(x => x.Title).HasMaxLength(1000).IsRequired();
            e.HasIndex(x => x.MeetingStartTime);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.ClassificationStatus);
            e.HasIndex(x => x.ExportStatus);
            e.HasIndex(x => x.AssignedProjectId);
            e.HasIndex(x => new { x.ExportPending, x.NextRetryAt });
            e.HasOne(x => x.AssignedProject).WithMany().HasForeignKey(x => x.AssignedProjectId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Participants).WithOne().HasForeignKey(p => p.MeetingId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.ProjectAssignments).WithOne().HasForeignKey(p => p.MeetingId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.ExportRecords).WithOne(r => r.Meeting).HasForeignKey(r => r.MeetingId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MeetingParticipant>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(300);
            e.Property(x => x.Email).HasMaxLength(320);
            e.HasIndex(x => x.Email);
        });

        b.Entity<MeetingProjectAssignment>(e =>
        {
            e.HasIndex(x => new { x.MeetingId, x.ProjectId }).IsUnique();
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ExportRecord>(e =>
        {
            e.Property(x => x.RelativePath).HasMaxLength(1000).IsRequired();
            e.HasIndex(x => new { x.MeetingId, x.Kind, x.Location, x.ProjectId });
            e.HasIndex(x => x.RelativePath);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ProjectId);
        });

        b.Entity<SyncRun>(e =>
        {
            e.HasIndex(x => x.StartedAt);
            e.HasIndex(x => x.Status);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.SyncRunId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SyncRunItem>(e =>
        {
            e.HasIndex(x => x.MeetingId);
            e.Property(x => x.Operation).HasMaxLength(60);
            e.Property(x => x.Outcome).HasMaxLength(60);
            e.Property(x => x.Message).HasMaxLength(1000);
        });

        b.Entity<ApplicationSetting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
        });

        b.Entity<ClassificationReview>(e => e.HasIndex(x => x.MeetingId));

        b.Entity<SyncLease>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(40);
            e.Property(x => x.Owner).HasMaxLength(200);
        });
    }

    private sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class NullableUtcConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : v.Value.ToUniversalTime()) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
}
