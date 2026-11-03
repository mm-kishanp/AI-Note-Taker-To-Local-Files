using Chroniq.Application.Classification;
using Chroniq.Application.Settings;
using Chroniq.Domain.Enums;

namespace Chroniq.Application.DTOs;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

// ---------- Dashboard ----------

public sealed record DashboardDto(
    int TotalMeetings,
    int MeetingsExported,
    int NotesExported,
    int TranscriptsExported,
    int ProjectsConfigured,
    int UnassignedMeetings,
    int NeedsReview,
    int FailedExports,
    int Conflicts,
    DateTimeOffset? LastSuccessfulSync,
    DateTimeOffset? NextScheduledSync,
    bool AutoSyncEnabled,
    bool CredentialsConfigured,
    IReadOnlyList<MeetingListItemDto> RecentMeetings,
    IReadOnlyList<SyncRunDto> RecentRuns);

// ---------- Meetings ----------

public sealed record MeetingListItemDto(
    Guid Id,
    string GranolaNoteId,
    string Title,
    DateTimeOffset MeetingDate,
    IReadOnlyList<string> Participants,
    Guid? ProjectId,
    string? ProjectName,
    string? ClientName,
    ClassificationStatus ClassificationStatus,
    AssignmentMethod AssignmentMethod,
    Certainty Certainty,
    bool NeedsReview,
    ContentStatus NotesStatus,
    ContentStatus TranscriptStatus,
    ExportStatus ExportStatus,
    SourceStatus SourceStatus,
    DateTimeOffset? LastSyncedAt,
    string? LastError,
    MeetingSource Source = MeetingSource.Granola);

public sealed record MeetingQuery(
    string? Search = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    Guid? ProjectId = null,
    Guid? ClientId = null,
    ClassificationStatus? Classification = null,
    ExportStatus? ExportStatus = null,
    ContentStatus? TranscriptStatus = null,
    bool? NeedsReview = null,
    MeetingSource? Source = null,
    string? Sort = "date",
    bool Desc = true,
    int Page = 1,
    int PageSize = 25);

public sealed record ParticipantDto(string? Name, string? Email, bool IsOwner, bool IsAttendee, bool IsInvitee, int SpokenSegments);

public sealed record ExportRecordDto(
    Guid Id,
    ExportKind Kind,
    ExportLocation Location,
    Guid? ProjectId,
    string? ProjectName,
    ExportRole Role,
    string RelativePath,
    string AbsolutePath,
    bool FileExists,
    ExportStatus Status,
    DateTimeOffset? LastExportedAt,
    DateTimeOffset? ConflictDetectedAt,
    string? LastError);

public sealed record ClassificationExplanationDto(
    ClassificationStatus Status,
    AssignmentMethod Method,
    Certainty Certainty,
    string? Tier,
    string Reason,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<ProjectRefDto> Candidates,
    IReadOnlyList<string> Conflicts,
    bool NeedsReview,
    string? SuggestedNextAction,
    ManualAssignmentKind ManualAssignment,
    DateTimeOffset? ManualAssignedAt);

public sealed record ProjectRefDto(Guid Id, string Name, string Slug);

public sealed record SyncRunItemDto(Guid Id, Guid SyncRunId, Guid? MeetingId, string? GranolaNoteId, string Operation, string Outcome, ErrorCategory ErrorCategory, bool Retryable, string? Message, DateTimeOffset Timestamp);

public sealed record MeetingDetailDto(
    MeetingListItemDto Meeting,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? MeetingStart,
    DateTimeOffset? MeetingEnd,
    string? OwnerName,
    string? OwnerEmail,
    string? SourceUrl,
    string? CalendarEventTitle,
    IReadOnlyList<string> GranolaFolders,
    IReadOnlyList<ParticipantDto> Participants,
    IReadOnlyList<SpeakerDto> IdentifiedSpeakers,
    string? Summary,
    string? NotesContent,
    int TranscriptSegmentCount,
    ClassificationExplanationDto Classification,
    IReadOnlyList<ExportRecordDto> Exports,
    IReadOnlyList<SyncRunItemDto> History,
    int FailureCount,
    DateTimeOffset? NextRetryAt,
    ErrorCategory LastErrorCategory);

public sealed record SpeakerDto(string Name, int Segments);

public sealed record TranscriptSegmentDto(string Speaker, string Text, DateTimeOffset? StartTime, DateTimeOffset? EndTime);

public sealed record AssignMeetingRequest(ManualAssignmentKind Kind, Guid? ProjectId, string? Note);

public sealed record ReExportRequest(bool Notes = true, bool Transcript = true, bool Force = false);

public enum ConflictResolution
{
    KeepLocal = 0,
    ReplaceWithSource = 1,
    SaveConflictCopy = 2,
}

public sealed record ResolveConflictRequest(ConflictResolution Resolution);

public sealed record ConflictReviewDto(Guid ExportRecordId, string RelativePath, string? LocalContent, string SourceContent);

public sealed record DestinationPreviewDto(string Location, string? ProjectName, string? NotesPath, string? TranscriptPath);

// ---------- Projects ----------

public sealed record RuleDto(Guid Id, RuleKind Kind, string Value, bool Enabled);

public sealed record RuleInput(RuleKind Kind, string Value, bool Enabled = true);

public sealed record ProjectDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    Guid? ClientId,
    string? ClientName,
    ProjectStatus Status,
    string? NotesFolderPath,
    string? TranscriptFolderPath,
    int DefaultClassificationPriority,
    IReadOnlyList<RuleDto> Rules,
    int MeetingCount,
    DateTimeOffset? LastMeetingAt,
    string ResolvedNotesDirectory,
    string ResolvedTranscriptsDirectory,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ProjectInput(
    string Name,
    string? Slug,
    string? Description,
    Guid? ClientId,
    ProjectStatus Status,
    string? NotesFolderPath,
    string? TranscriptFolderPath,
    int DefaultClassificationPriority,
    IReadOnlyList<RuleInput> Rules);

public sealed record TestMappingRequest(IReadOnlyList<RuleInput>? Rules, int Limit = 200);

public sealed record TestMappingItemDto(
    Guid MeetingId,
    string Title,
    DateTimeOffset MeetingDate,
    ClassificationStatus CurrentStatus,
    string? CurrentProject,
    ClassificationStatus ProposedStatus,
    string? ProposedProject,
    bool Changes,
    bool IsManual,
    string Reason);

public sealed record TestMappingResultDto(int Evaluated, int WouldMatch, int WouldChange, IReadOnlyList<TestMappingItemDto> Items);

public sealed record ApplyRulesRequest(IReadOnlyList<Guid> MeetingIds, bool Confirm);

public sealed record ClientDto(Guid Id, string Name, string? Description, IReadOnlyList<RuleDto> Rules, IReadOnlyList<ProjectRefDto> Projects);

public sealed record ClientInput(string Name, string? Description, IReadOnlyList<RuleInput> Rules);

public sealed record ProjectConfigExport(int Version, IReadOnlyList<ClientConfig> Clients, IReadOnlyList<ProjectConfig> Projects);

public sealed record ClientConfig(string Name, string? Description, IReadOnlyList<RuleInput> Rules);

public sealed record ProjectConfig(
    string? Id,
    string Name,
    string Slug,
    string? Description,
    string? ClientName,
    ProjectStatus Status,
    string? NotesFolder,
    string? TranscriptsFolder,
    int Priority,
    IReadOnlyList<RuleInput> Rules);

public sealed record ImportResultDto(int ClientsCreated, int ClientsUpdated, int ProjectsCreated, int ProjectsUpdated, IReadOnlyList<string> Warnings);

// ---------- Unassigned ----------

public sealed record UnassignedItemDto(
    MeetingListItemDto Meeting,
    IReadOnlyList<ParticipantDto> Participants,
    IReadOnlyList<SpeakerDto> IdentifiedSpeakers,
    IReadOnlyList<ProjectRefDto> Candidates,
    string Reason,
    string? SuggestedNextAction,
    string? AiSuggestion);

public sealed record SaveMappingFromReviewRequest(Guid ProjectId, RuleKind Kind, string Value, bool AssignThisMeeting = true);

// ---------- Sync ----------

public sealed record SyncRunDto(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    SyncTrigger Trigger,
    SyncMode Mode,
    SyncRunStatus Status,
    int NotesDiscovered,
    int MeetingsProcessed,
    int NotesExported,
    int TranscriptsExported,
    int FilesUpdated,
    int MeetingsSkipped,
    int ClassificationFailures,
    int ApiFailures,
    int FileSystemFailures,
    int Conflicts,
    long DurationMs,
    string? Error);

public sealed record StartSyncRequest(SyncMode Mode = SyncMode.Incremental, IReadOnlyList<Guid>? MeetingIds = null, bool Reclassify = false, bool Force = false);

// ---------- Settings ----------

public sealed record SettingsDto(
    ExportSettings Export,
    SyncSettings Sync,
    ClassificationSettings Classification,
    LoggingSettings Logging,
    ResolvedPathsDto ResolvedPaths);

public sealed record ResolvedPathsDto(string Root, string NotesRoot, string TranscriptsRoot, string BaseDirectory);

public sealed record UpdateSettingsRequest(ExportSettings? Export, SyncSettings? Sync, ClassificationSettings? Classification, LoggingSettings? Logging);

public sealed record ValidationErrorDto(string Field, string Message);

public sealed record CredentialStatusDto(
    bool Configured,
    string? Source,
    string? MaskedKey,
    bool CanStore,
    string StoreName,
    bool? LastCheckSucceeded,
    DateTimeOffset? LastCheckedAt,
    DateTimeOffset? LastSuccessAt,
    string? LastError,
    ErrorCategory LastErrorCategory);

public sealed record SetCredentialRequest(string ApiKey, bool Validate = true);

public sealed record ConnectionTestDto(bool Success, ErrorCategory ErrorCategory, string Message, string? CredentialSource);

public sealed record AiStatusDto(bool KeyConfigured, string? MaskedKey, bool CanStore);

// ---------- Exports ----------

public sealed record ExportTreeNodeDto(string Name, string RelativePath, bool IsDirectory, int FileCount, DateTimeOffset? LastModified, long? Size, IReadOnlyList<ExportTreeNodeDto> Children);

public sealed record ExportTreeDto(string Root, string RootKind, bool Exists, string? Warning, ExportTreeNodeDto? Tree);

public sealed record ValidatePathRequest(string Path, bool CreateIfMissing = false);

public sealed record ValidatePathResultDto(bool Valid, string FullPath, bool Exists, bool Writable, long? FreeBytes, IReadOnlyList<string> Messages);

public sealed record MigrateRootRequest(string NewRoot, bool Confirm);

public sealed record MigrateRootResultDto(string From, string To, int FilesCopied, int FilesSkipped, IReadOnlyList<string> Warnings, bool SettingsUpdated);

public sealed record DirectoryListingDto(string? Path, string? Parent, IReadOnlyList<DirectoryEntryDto> Directories);

public sealed record DirectoryEntryDto(string Name, string FullPath);

public sealed record FileContentDto(string RelativePath, string Content, long Size, DateTimeOffset LastModified);

public sealed record HealthDto(string Status, string Version, bool Database, bool CredentialsConfigured, bool SyncRunning, DateTimeOffset Time);
