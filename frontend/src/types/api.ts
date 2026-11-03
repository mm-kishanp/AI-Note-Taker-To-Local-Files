// TypeScript types hand-written from docs/openapi.json (CHRONIQ API).
// Enums are serialized as JSON strings by the backend (JsonStringEnumConverter).
// Integer fields are declared as `integer | string` in the OpenAPI document because of .NET number
// handling, but the server always emits JSON numbers, so they are typed as `number` here.

// ---------- Enums ----------
export const CLASSIFICATION_STATUSES = ['Pending', 'Assigned', 'Shared', 'MultiProject', 'NeedsReview', 'Unassigned', 'Excluded'] as const;
export type ClassificationStatus = (typeof CLASSIFICATION_STATUSES)[number];

export const ASSIGNMENT_METHODS = ['None', 'Manual', 'Rule', 'Ai'] as const;
export type AssignmentMethod = (typeof ASSIGNMENT_METHODS)[number];

export const CERTAINTIES = ['None', 'Low', 'Medium', 'High', 'Certain'] as const;
export type Certainty = (typeof CERTAINTIES)[number];

export const CLASSIFICATION_TIERS = ['ParticipantEmail', 'ParticipantIdentity', 'SpeakerIdentity', 'Client', 'Metadata', 'Keyword', 'Ai'] as const;
export type ClassificationTier = (typeof CLASSIFICATION_TIERS)[number];

export const CONFLICT_POLICIES = ['PreserveUserEdits', 'AlwaysOverwrite'] as const;
export type ConflictPolicy = (typeof CONFLICT_POLICIES)[number];

export const CONFLICT_RESOLUTIONS = ['KeepLocal', 'ReplaceWithSource', 'SaveConflictCopy'] as const;
export type ConflictResolution = (typeof CONFLICT_RESOLUTIONS)[number];

export const CONTENT_STATUSES = ['Unknown', 'Available', 'Empty', 'Processing', 'Failed'] as const;
export type ContentStatus = (typeof CONTENT_STATUSES)[number];

export const ERROR_CATEGORIES = [
  'None', 'Authentication', 'Permission', 'RateLimited', 'TransientApi', 'InvalidResponse', 'NotFound', 'TranscriptUnavailable',
  'Classification', 'Configuration', 'FilePermission', 'FileConflict', 'DiskSpace', 'Database', 'SyncConflict', 'Cancelled', 'Unknown',
] as const;
export type ErrorCategory = (typeof ERROR_CATEGORIES)[number];

export const EXPORT_KINDS = ['Notes', 'Transcript'] as const;
export type ExportKind = (typeof EXPORT_KINDS)[number];

export const EXPORT_LOCATIONS = ['Project', 'Unassigned', 'Shared'] as const;
export type ExportLocation = (typeof EXPORT_LOCATIONS)[number];

export const EXPORT_ROLES = ['Primary', 'Copy'] as const;
export type ExportRole = (typeof EXPORT_ROLES)[number];

export const EXPORT_STATUSES = ['Pending', 'Exported', 'Failed', 'Conflict', 'KeptLocal', 'LocallyModified', 'Missing', 'Superseded', 'Skipped'] as const;
export type ExportStatus = (typeof EXPORT_STATUSES)[number];

export const MANUAL_ASSIGNMENT_KINDS = ['None', 'Project', 'Shared', 'Excluded'] as const;
export type ManualAssignmentKind = (typeof MANUAL_ASSIGNMENT_KINDS)[number];

export const MULTI_PROJECT_MODES = ['SingleProject', 'Shared', 'MultipleExports'] as const;
export type MultiProjectMode = (typeof MULTI_PROJECT_MODES)[number];

export const PROJECT_STATUSES = ['Active', 'Archived'] as const;
export type ProjectStatus = (typeof PROJECT_STATUSES)[number];

export const RULE_KINDS = ['Email', 'EmailDomain', 'ParticipantName', 'SpeakerName', 'GranolaFolder', 'TitleKeyword', 'Keyword'] as const;
export type RuleKind = (typeof RULE_KINDS)[number];

export const SOURCE_STATUSES = ['Active', 'Deleted', 'Inaccessible'] as const;
export type SourceStatus = (typeof SOURCE_STATUSES)[number];

export const SYNC_MODES = ['Incremental', 'Full', 'RetryFailed', 'ReExport', 'Reconcile'] as const;
export type SyncMode = (typeof SYNC_MODES)[number];

export const SYNC_RUN_STATUSES = ['Running', 'Succeeded', 'PartiallySucceeded', 'Failed', 'Cancelled', 'Interrupted'] as const;
export type SyncRunStatus = (typeof SYNC_RUN_STATUSES)[number];

export const SYNC_TRIGGERS = ['Manual', 'Scheduled', 'Startup', 'Api'] as const;
export type SyncTrigger = (typeof SYNC_TRIGGERS)[number];

export type ExportRootKind = 'root' | 'notes' | 'transcripts';

// ---------- Common ----------
export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  detail?: string;
  status?: number;
  instance?: string;
  category?: ErrorCategory | string;
  correlationId?: string;
  traceId?: string;
  /** Application validation errors are a list; ASP.NET model-binding errors are a dictionary. */
  errors?: Array<{ field: string; message: string }> | Record<string, string[]>;
}

// ---------- Meetings ----------
export const MEETING_SOURCES = ['Granola', 'Fireflies', 'WhisperAi'] as const;
export type MeetingSource = (typeof MEETING_SOURCES)[number];
/** Display names; the API uses the enum names above. */
export const MEETING_SOURCE_LABELS: Record<MeetingSource, string> = { Granola: 'Granola', Fireflies: 'Fireflies', WhisperAi: 'WhisperAI' };

export interface MeetingListItemDto {
  id: string;
  granolaNoteId: string;
  title: string;
  meetingDate: string;
  participants: string[];
  projectId: string | null;
  projectName: string | null;
  clientName: string | null;
  classificationStatus: ClassificationStatus;
  assignmentMethod: AssignmentMethod;
  certainty: Certainty;
  needsReview: boolean;
  notesStatus: ContentStatus;
  transcriptStatus: ContentStatus;
  exportStatus: ExportStatus;
  sourceStatus: SourceStatus;
  lastSyncedAt: string | null;
  lastError: string | null;
  source: MeetingSource;
}

export interface MeetingQuery {
  search?: string;
  from?: string;
  to?: string;
  projectId?: string;
  clientId?: string;
  classification?: ClassificationStatus;
  exportStatus?: ExportStatus;
  transcriptStatus?: ContentStatus;
  needsReview?: boolean;
  source?: MeetingSource;
  sort?: 'date' | 'title' | 'project' | 'synced' | 'status';
  desc?: boolean;
  page?: number;
  pageSize?: number;
}

export interface ParticipantDto {
  name: string | null;
  email: string | null;
  isOwner: boolean;
  isAttendee: boolean;
  isInvitee: boolean;
  spokenSegments: number;
}

export interface SpeakerDto {
  name: string;
  segments: number;
}

export interface ProjectRefDto {
  id: string;
  name: string;
  slug: string;
}

export interface ClassificationExplanationDto {
  status: ClassificationStatus;
  method: AssignmentMethod;
  certainty: Certainty;
  tier: string | null;
  reason: string;
  evidence: string[];
  candidates: ProjectRefDto[];
  conflicts: string[];
  needsReview: boolean;
  suggestedNextAction: string | null;
  manualAssignment: ManualAssignmentKind;
  manualAssignedAt: string | null;
}

export interface ExportRecordDto {
  id: string;
  kind: ExportKind;
  location: ExportLocation;
  projectId: string | null;
  projectName: string | null;
  role: ExportRole;
  relativePath: string;
  absolutePath: string;
  fileExists: boolean;
  status: ExportStatus;
  lastExportedAt: string | null;
  conflictDetectedAt: string | null;
  lastError: string | null;
}

export interface SyncRunItemDto {
  id: string;
  syncRunId: string;
  meetingId: string | null;
  granolaNoteId: string | null;
  operation: string;
  outcome: string;
  errorCategory: ErrorCategory;
  retryable: boolean;
  message: string | null;
  timestamp: string;
}

export interface MeetingDetailDto {
  meeting: MeetingListItemDto;
  createdAt: string;
  updatedAt: string;
  meetingStart: string | null;
  meetingEnd: string | null;
  ownerName: string | null;
  ownerEmail: string | null;
  sourceUrl: string | null;
  calendarEventTitle: string | null;
  granolaFolders: string[];
  participants: ParticipantDto[];
  identifiedSpeakers: SpeakerDto[];
  summary: string | null;
  notesContent: string | null;
  transcriptSegmentCount: number;
  classification: ClassificationExplanationDto;
  exports: ExportRecordDto[];
  history: SyncRunItemDto[];
  failureCount: number;
  nextRetryAt: string | null;
  lastErrorCategory: ErrorCategory;
}

export interface TranscriptSegmentDto {
  speaker: string;
  text: string;
  startTime: string | null;
  endTime: string | null;
}

export interface AssignMeetingRequest {
  kind: ManualAssignmentKind;
  projectId: string | null;
  note: string | null;
}

export interface DestinationPreviewDto {
  location: string;
  projectName: string | null;
  notesPath: string | null;
  transcriptPath: string | null;
}

export interface ReExportRequest {
  notes?: boolean;
  transcript?: boolean;
  force?: boolean;
}

export interface ConflictReviewDto {
  exportRecordId: string;
  relativePath: string;
  localContent: string | null;
  sourceContent: string;
}

export interface ResolveConflictRequest {
  resolution: ConflictResolution;
}

// ---------- Unassigned ----------
export interface UnassignedItemDto {
  meeting: MeetingListItemDto;
  participants: ParticipantDto[];
  identifiedSpeakers: SpeakerDto[];
  candidates: ProjectRefDto[];
  reason: string;
  suggestedNextAction: string | null;
  aiSuggestion: string | null;
}

export interface SaveMappingFromReviewRequest {
  projectId: string;
  kind: RuleKind;
  value: string;
  assignThisMeeting?: boolean;
}

// ---------- Dashboard ----------
export interface DashboardDto {
  totalMeetings: number;
  meetingsExported: number;
  notesExported: number;
  transcriptsExported: number;
  projectsConfigured: number;
  unassignedMeetings: number;
  needsReview: number;
  failedExports: number;
  conflicts: number;
  lastSuccessfulSync: string | null;
  nextScheduledSync: string | null;
  autoSyncEnabled: boolean;
  credentialsConfigured: boolean;
  recentMeetings: MeetingListItemDto[];
  recentRuns: SyncRunDto[];
}

// ---------- Projects & clients ----------
export interface RuleDto {
  id: string;
  kind: RuleKind;
  value: string;
  enabled: boolean;
}

export interface RuleInput {
  kind: RuleKind;
  value: string;
  enabled?: boolean;
}

export interface ProjectDto {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  clientId: string | null;
  clientName: string | null;
  status: ProjectStatus;
  notesFolderPath: string | null;
  transcriptFolderPath: string | null;
  defaultClassificationPriority: number;
  rules: RuleDto[];
  meetingCount: number;
  lastMeetingAt: string | null;
  resolvedNotesDirectory: string;
  resolvedTranscriptsDirectory: string;
  createdAt: string;
  updatedAt: string;
}

export interface ProjectInput {
  name: string;
  slug: string | null;
  description: string | null;
  clientId: string | null;
  status: ProjectStatus;
  notesFolderPath: string | null;
  transcriptFolderPath: string | null;
  defaultClassificationPriority: number;
  rules: RuleInput[];
}

export interface ClientDto {
  id: string;
  name: string;
  description: string | null;
  rules: RuleDto[];
  projects: ProjectRefDto[];
}

export interface ClientInput {
  name: string;
  description: string | null;
  rules: RuleInput[];
}

export interface TestMappingRequest {
  rules: RuleInput[] | null;
  limit?: number;
}

export interface TestMappingItemDto {
  meetingId: string;
  title: string;
  meetingDate: string;
  currentStatus: ClassificationStatus;
  currentProject: string | null;
  proposedStatus: ClassificationStatus;
  proposedProject: string | null;
  changes: boolean;
  isManual: boolean;
  reason: string;
}

export interface TestMappingResultDto {
  evaluated: number;
  wouldMatch: number;
  wouldChange: number;
  items: TestMappingItemDto[];
}

export interface ApplyRulesRequest {
  meetingIds: string[];
  confirm: boolean;
}

export interface ClientConfig {
  name: string;
  description: string | null;
  rules: RuleInput[];
}

export interface ProjectConfig {
  id: string | null;
  name: string;
  slug: string;
  description: string | null;
  clientName: string | null;
  status: ProjectStatus;
  notesFolder: string | null;
  transcriptsFolder: string | null;
  priority: number;
  rules: RuleInput[];
}

export interface ProjectConfigExport {
  version: number;
  clients: ClientConfig[];
  projects: ProjectConfig[];
}

export interface ImportResultDto {
  clientsCreated: number;
  clientsUpdated: number;
  projectsCreated: number;
  projectsUpdated: number;
  warnings: string[];
}

// ---------- Sync ----------
export interface StartSyncRequest {
  mode?: SyncMode;
  meetingIds?: string[] | null;
  reclassify?: boolean;
  force?: boolean;
}

export interface SyncStartResult {
  started: boolean;
  runId: string | null;
  message: string;
}

export interface SyncProgressSnapshot {
  isRunning: boolean;
  runId: string | null;
  mode: SyncMode | null;
  trigger: SyncTrigger | null;
  phase: string;
  discovered: number;
  queued: number;
  processed: number;
  failed: number;
  currentItem: string | null;
  startedAt: string | null;
  lastRunId: string | null;
  lastRunStatus: SyncRunStatus | null;
  lastRunFinishedAt: string | null;
}

export interface SyncRunDto {
  id: string;
  startedAt: string;
  finishedAt: string | null;
  trigger: SyncTrigger;
  mode: SyncMode;
  status: SyncRunStatus;
  notesDiscovered: number;
  meetingsProcessed: number;
  notesExported: number;
  transcriptsExported: number;
  filesUpdated: number;
  meetingsSkipped: number;
  classificationFailures: number;
  apiFailures: number;
  fileSystemFailures: number;
  conflicts: number;
  durationMs: number;
  error: string | null;
}

/** GET /api/sync/history/{id} — untyped in OpenAPI (returns an anonymous object). */
export interface SyncRunDetailDto {
  run: SyncRunDto;
  items: SyncRunItemDto[];
}

export interface HealthDto {
  status: string;
  version: string;
  database: boolean;
  credentialsConfigured: boolean;
  syncRunning: boolean;
  time: string;
}

// ---------- Settings ----------
export interface ExportSettings {
  rootDirectory: string;
  notesRootDirectory: string | null;
  transcriptsRootDirectory: string | null;
  projectDirectoryTemplate: string;
  notesDirectoryTemplate: string;
  transcriptsDirectoryTemplate: string;
  unassignedNotesDirectory: string;
  unassignedTranscriptsDirectory: string;
  sharedNotesDirectory: string;
  sharedTranscriptsDirectory: string;
  filenameTemplate: string;
  exportNotes: boolean;
  exportTranscripts: boolean;
  includeAttachmentLinks: boolean;
  conflictPolicy: ConflictPolicy;
  moveFilesOnReassignment: boolean;
  generateProjectContext: boolean;
  generateReviewQueue: boolean;
}

export interface SyncSettings {
  autoSyncEnabled: boolean;
  intervalMinutes: number;
  initialImportFrom: string | null;
  maxConcurrency: number;
  maxRequestAttempts: number;
  maxMeetingRetries: number;
  incrementalOverlapMinutes: number;
  firefliesLookbackDays: number;
}

export interface AiSettings {
  enabled: boolean;
  provider: string;
  model: string;
  autoAssign: boolean;
  includeSummary: boolean;
}

export interface ClassificationSettings {
  tierOrder: ClassificationTier[];
  disabledTiers: ClassificationTier[];
  multiProjectMode: MultiProjectMode;
  ignoreOwner: boolean;
  useCalendarInvitees: boolean;
  minimumSpeakerSegments: number;
  ai: AiSettings;
}

export interface LoggingSettings {
  minimumLevel: string;
  logDirectory: string;
  retentionDays: number;
}

export interface ResolvedPathsDto {
  root: string;
  notesRoot: string;
  transcriptsRoot: string;
  baseDirectory: string;
}

export interface SettingsDto {
  export: ExportSettings;
  sync: SyncSettings;
  classification: ClassificationSettings;
  logging: LoggingSettings;
  resolvedPaths: ResolvedPathsDto;
}

export interface UpdateSettingsRequest {
  export: ExportSettings | null;
  sync: SyncSettings | null;
  classification: ClassificationSettings | null;
  logging: LoggingSettings | null;
}

export interface CredentialStatusDto {
  configured: boolean;
  source: string | null;
  maskedKey: string | null;
  canStore: boolean;
  storeName: string;
  lastCheckSucceeded: boolean | null;
  lastCheckedAt: string | null;
  lastSuccessAt: string | null;
  lastError: string | null;
  lastErrorCategory: ErrorCategory;
}

export interface AiStatusDto {
  keyConfigured: boolean;
  maskedKey: string | null;
  canStore: boolean;
}

export interface SetCredentialRequest {
  apiKey: string;
  validate?: boolean;
}

export interface ConnectionTestDto {
  success: boolean;
  errorCategory: ErrorCategory;
  message: string;
  credentialSource: string | null;
}

export interface GranolaFolder {
  id?: string;
  name?: string;
  parent_folder_id?: string | null;
  space_id?: string | null;
}

// ---------- Exports / file system ----------
export interface ExportTreeNodeDto {
  name: string;
  relativePath: string;
  isDirectory: boolean;
  fileCount: number;
  lastModified: string | null;
  size: number | null;
  children: ExportTreeNodeDto[];
}

export interface ExportTreeDto {
  root: string;
  rootKind: string;
  exists: boolean;
  warning: string | null;
  tree: ExportTreeNodeDto | null;
}

export interface FileContentDto {
  relativePath: string;
  content: string;
  size: number;
  lastModified: string;
}

export interface ValidatePathRequest {
  path: string;
  createIfMissing?: boolean;
}

export interface ValidatePathResultDto {
  valid: boolean;
  fullPath: string;
  exists: boolean;
  writable: boolean;
  freeBytes: number | null;
  messages: string[];
}

export interface MigrateRootRequest {
  newRoot: string;
  confirm: boolean;
}

export interface MigrateRootResultDto {
  from: string;
  to: string;
  filesCopied: number;
  filesSkipped: number;
  warnings: string[];
  settingsUpdated: boolean;
}

export interface DirectoryEntryDto {
  name: string;
  fullPath: string;
}

export interface DirectoryListingDto {
  path: string | null;
  parent: string | null;
  directories: DirectoryEntryDto[];
}
