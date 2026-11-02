// TypeScript types hand-written from docs/openapi.json (Granola Project Intelligence API).
// Enums are serialized as JSON strings by the backend (JsonStringEnumConverter).
// Integer fields are declared as `integer | string` in the OpenAPI document because of .NET number
// handling, but the server always emits JSON numbers, so they are typed as `number` here.
// ---------- Enums ----------
export const CLASSIFICATION_STATUSES = ['Pending', 'Assigned', 'Shared', 'MultiProject', 'NeedsReview', 'Unassigned', 'Excluded'];
export const ASSIGNMENT_METHODS = ['None', 'Manual', 'Rule', 'Ai'];
export const CERTAINTIES = ['None', 'Low', 'Medium', 'High', 'Certain'];
export const CLASSIFICATION_TIERS = ['ParticipantEmail', 'ParticipantIdentity', 'SpeakerIdentity', 'Client', 'Metadata', 'Keyword', 'Ai'];
export const CONFLICT_POLICIES = ['PreserveUserEdits', 'AlwaysOverwrite'];
export const CONFLICT_RESOLUTIONS = ['KeepLocal', 'ReplaceWithSource', 'SaveConflictCopy'];
export const CONTENT_STATUSES = ['Unknown', 'Available', 'Empty', 'Processing', 'Failed'];
export const ERROR_CATEGORIES = [
    'None', 'Authentication', 'Permission', 'RateLimited', 'TransientApi', 'InvalidResponse', 'NotFound', 'TranscriptUnavailable',
    'Classification', 'Configuration', 'FilePermission', 'FileConflict', 'DiskSpace', 'Database', 'SyncConflict', 'Cancelled', 'Unknown',
];
export const EXPORT_KINDS = ['Notes', 'Transcript'];
export const EXPORT_LOCATIONS = ['Project', 'Unassigned', 'Shared'];
export const EXPORT_ROLES = ['Primary', 'Copy'];
export const EXPORT_STATUSES = ['Pending', 'Exported', 'Failed', 'Conflict', 'KeptLocal', 'LocallyModified', 'Missing', 'Superseded', 'Skipped'];
export const MANUAL_ASSIGNMENT_KINDS = ['None', 'Project', 'Shared', 'Excluded'];
export const MULTI_PROJECT_MODES = ['SingleProject', 'Shared', 'MultipleExports'];
export const PROJECT_STATUSES = ['Active', 'Archived'];
export const RULE_KINDS = ['Email', 'EmailDomain', 'ParticipantName', 'SpeakerName', 'GranolaFolder', 'TitleKeyword', 'Keyword'];
export const SOURCE_STATUSES = ['Active', 'Deleted', 'Inaccessible'];
export const SYNC_MODES = ['Incremental', 'Full', 'RetryFailed', 'ReExport', 'Reconcile'];
export const SYNC_RUN_STATUSES = ['Running', 'Succeeded', 'PartiallySucceeded', 'Failed', 'Cancelled', 'Interrupted'];
export const SYNC_TRIGGERS = ['Manual', 'Scheduled', 'Startup', 'Api'];
// ---------- Meetings ----------
export const MEETING_SOURCES = ['Granola', 'Fireflies', 'WhisperAi'];
/** Display names; the API uses the enum names above. */
export const MEETING_SOURCE_LABELS = { Granola: 'Granola', Fireflies: 'Fireflies', WhisperAi: 'WhisperAI' };
