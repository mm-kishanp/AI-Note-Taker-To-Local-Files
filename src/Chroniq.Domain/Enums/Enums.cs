namespace Chroniq.Domain.Enums;

public enum ProjectStatus
{
    Active = 0,
    Archived = 1,
}

/// <summary>Where a meeting currently lives in the export repository.</summary>
public enum ClassificationStatus
{
    Pending = 0,
    Assigned = 1,
    Shared = 2,
    MultiProject = 3,
    NeedsReview = 4,
    Unassigned = 5,
    Excluded = 6,
}

public enum AssignmentMethod
{
    None = 0,
    Manual = 1,
    Rule = 2,
    Ai = 3,
}

/// <summary>Certainty category for an automatic assignment. Deterministic rules map to a category, never a percentage.</summary>
public enum Certainty
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Certain = 4,
}

/// <summary>An explicit manual decision that overrides automatic classification.</summary>
public enum ManualAssignmentKind
{
    None = 0,
    Project = 1,
    Shared = 2,
    Excluded = 3,
}

/// <summary>Availability of a piece of source content (notes or transcript).</summary>
public enum ContentStatus
{
    Unknown = 0,
    Available = 1,
    Empty = 2,
    Processing = 3,
    Failed = 4,
}

/// <summary>The service a meeting was imported from.</summary>
public enum MeetingSource
{
    Granola = 0,
    Fireflies = 1,
    WhisperAi = 2,
}

public enum SourceStatus
{
    Active = 0,
    Deleted = 1,
    Inaccessible = 2,
}

public enum ExportKind
{
    Notes = 0,
    Transcript = 1,
}

public enum ExportLocation
{
    Project = 0,
    Unassigned = 1,
    Shared = 2,
}

public enum ExportRole
{
    Primary = 0,
    Copy = 1,
}

public enum ExportStatus
{
    Pending = 0,
    Exported = 1,
    Failed = 2,
    Conflict = 3,
    KeptLocal = 4,
    LocallyModified = 5,
    Missing = 6,
    Superseded = 7,
    Skipped = 8,
}

/// <summary>Classification rule kinds, from strongest to weakest evidence.</summary>
public enum RuleKind
{
    Email = 0,
    EmailDomain = 1,
    ParticipantName = 2,
    SpeakerName = 3,
    GranolaFolder = 4,
    TitleKeyword = 5,
    Keyword = 6,
}

/// <summary>Evaluation tiers used by the classification engine. Order is configurable.</summary>
public enum ClassificationTier
{
    ParticipantEmail = 0,
    ParticipantIdentity = 1,
    SpeakerIdentity = 2,
    Client = 3,
    Metadata = 4,
    Keyword = 5,
    Ai = 6,
}

public enum MultiProjectMode
{
    SingleProject = 0,
    Shared = 1,
    MultipleExports = 2,
}

public enum ConflictPolicy
{
    PreserveUserEdits = 0,
    AlwaysOverwrite = 1,
}

public enum SyncTrigger
{
    Manual = 0,
    Scheduled = 1,
    Startup = 2,
    Api = 3,
}

public enum SyncMode
{
    Incremental = 0,
    Full = 1,
    RetryFailed = 2,
    ReExport = 3,
    Reconcile = 4,
}

public enum SyncRunStatus
{
    Running = 0,
    Succeeded = 1,
    PartiallySucceeded = 2,
    Failed = 3,
    Cancelled = 4,
    Interrupted = 5,
}

public enum ErrorCategory
{
    None = 0,
    Authentication = 1,
    Permission = 2,
    RateLimited = 3,
    TransientApi = 4,
    InvalidResponse = 5,
    NotFound = 6,
    TranscriptUnavailable = 7,
    Classification = 8,
    Configuration = 9,
    FilePermission = 10,
    FileConflict = 11,
    DiskSpace = 12,
    Database = 13,
    SyncConflict = 14,
    Cancelled = 15,
    Unknown = 99,
}

public enum ReviewAction
{
    AssignedProject = 0,
    AssignedShared = 1,
    Excluded = 2,
    RetriedClassification = 3,
    ClearedManualAssignment = 4,
    BulkRuleApplied = 5,
}
