using GranolaPI.Domain.Enums;

namespace GranolaPI.Application.Settings;

/// <summary>
/// Export destinations. Notes and transcripts each resolve against their own root, so they can live
/// on different drives. Directory templates are always relative to their root and may not escape it.
/// </summary>
public sealed record ExportSettings
{
    public string RootDirectory { get; init; } = "./GranolaProjectKnowledge";
    /// <summary>Optional separate root for notes. Null/empty uses <see cref="RootDirectory"/>.</summary>
    public string? NotesRootDirectory { get; init; }
    /// <summary>Optional separate root for transcripts. Null/empty uses <see cref="RootDirectory"/>.</summary>
    public string? TranscriptsRootDirectory { get; init; }
    public string ProjectDirectoryTemplate { get; init; } = "Projects/{ProjectSlug}";
    public string NotesDirectoryTemplate { get; init; } = "{ProjectDirectory}/Notes";
    public string TranscriptsDirectoryTemplate { get; init; } = "{ProjectDirectory}/Transcripts";
    public string UnassignedNotesDirectory { get; init; } = "Unassigned/Notes";
    public string UnassignedTranscriptsDirectory { get; init; } = "Unassigned/Transcripts";
    public string SharedNotesDirectory { get; init; } = "Shared/Notes";
    public string SharedTranscriptsDirectory { get; init; } = "Shared/Transcripts";
    public string FilenameTemplate { get; init; } = "{MeetingDate}-{MeetingTitle}";
    public bool ExportNotes { get; init; } = true;
    public bool ExportTranscripts { get; init; } = true;
    public bool IncludeAttachmentLinks { get; init; } = true;
    public ConflictPolicy ConflictPolicy { get; init; } = ConflictPolicy.PreserveUserEdits;
    /// <summary>When a meeting is reassigned, move its unmodified generated files to the new location.</summary>
    public bool MoveFilesOnReassignment { get; init; } = true;
    public bool GenerateProjectContext { get; init; } = true;
    public bool GenerateReviewQueue { get; init; } = true;
}

public sealed record SyncSettings
{
    public bool AutoSyncEnabled { get; init; }
    public int IntervalMinutes { get; init; } = 30;
    /// <summary>Initial historical import only includes notes created on/after this date. Null imports everything.</summary>
    public DateTimeOffset? InitialImportFrom { get; init; }
    public int MaxConcurrency { get; init; } = 2;
    /// <summary>Maximum HTTP attempts per Granola request for transient failures.</summary>
    public int MaxRequestAttempts { get; init; } = 5;
    /// <summary>How many sync runs may retry a failed meeting before it needs a manual retry.</summary>
    public int MaxMeetingRetries { get; init; } = 5;
    /// <summary>Overlap applied to the incremental checkpoint to tolerate clock skew.</summary>
    public int IncrementalOverlapMinutes { get; init; } = 10;
    /// <summary>
    /// Fireflies has no "updated since" filter and finishes transcripts and AI notes some time after
    /// the call, so each incremental run re-lists calls from this many days before the last run.
    /// </summary>
    public int FirefliesLookbackDays { get; init; } = 3;
}

public sealed record ClassificationSettings
{
    public List<ClassificationTier> TierOrder { get; init; } =
    [
        ClassificationTier.ParticipantEmail,
        ClassificationTier.ParticipantIdentity,
        ClassificationTier.SpeakerIdentity,
        ClassificationTier.Client,
        ClassificationTier.Metadata,
        ClassificationTier.Keyword,
        ClassificationTier.Ai,
    ];
    public List<ClassificationTier> DisabledTiers { get; init; } = [];
    public MultiProjectMode MultiProjectMode { get; init; } = MultiProjectMode.SingleProject;
    /// <summary>Ignore the note owner (you) when matching participants, so your own email never routes meetings.</summary>
    public bool IgnoreOwner { get; init; } = true;
    /// <summary>Use calendar invitee emails as participant evidence.</summary>
    public bool UseCalendarInvitees { get; init; } = true;
    public int MinimumSpeakerSegments { get; init; } = 1;
    public AiSettings Ai { get; init; } = new();
}

public sealed record AiSettings
{
    public bool Enabled { get; init; }
    public string Provider { get; init; } = "anthropic";
    public string Model { get; init; } = "claude-opus-5-5";
    /// <summary>When false (default) AI only suggests a project and the meeting goes to review.</summary>
    public bool AutoAssign { get; init; }
    /// <summary>Send Granola's summary text in addition to metadata. Off by default for privacy.</summary>
    public bool IncludeSummary { get; init; }
}

public sealed record LoggingSettings
{
    public string MinimumLevel { get; init; } = "Information";
    public string LogDirectory { get; init; } = "./data/logs";
    public int RetentionDays { get; init; } = 14;
}

public sealed record AppSettingsSnapshot(
    ExportSettings Export,
    SyncSettings Sync,
    ClassificationSettings Classification,
    LoggingSettings Logging);

public sealed record SyncCheckpoint
{
    public DateTimeOffset? LastSuccessfulIncrementalStart { get; init; }
    public DateTimeOffset? LastSuccessfulRunFinishedAt { get; init; }
}

public sealed record GranolaConnectionState
{
    public DateTimeOffset? LastCheckedAt { get; init; }
    public DateTimeOffset? LastSuccessAt { get; init; }
    public bool? LastCheckSucceeded { get; init; }
    public string? LastError { get; init; }
    public ErrorCategory LastErrorCategory { get; init; }
}
