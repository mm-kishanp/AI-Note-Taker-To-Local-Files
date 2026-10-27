import type {
  Certainty, ClassificationStatus, ContentStatus, ExportStatus, SyncRunStatus,
} from '../types/api';

export type StatusKind = 'classification' | 'export' | 'content' | 'sync' | 'certainty';

export type StatusTone = 'success' | 'info' | 'warning' | 'danger' | 'neutral' | 'progress';

/** Tone -> Mantine color. The badge text always carries the meaning; color is only a secondary signal. */
export const TONE_COLORS: Record<StatusTone, string> = {
  success: 'green',
  info: 'blue',
  warning: 'yellow',
  danger: 'red',
  neutral: 'gray',
  progress: 'cyan',
};

const classification: Record<ClassificationStatus, StatusTone> = {
  Assigned: 'success',
  Shared: 'info',
  MultiProject: 'info',
  NeedsReview: 'warning',
  Unassigned: 'warning',
  Pending: 'neutral',
  Excluded: 'neutral',
};

const exportStatus: Record<ExportStatus, StatusTone> = {
  Exported: 'success',
  Pending: 'neutral',
  Failed: 'danger',
  Conflict: 'danger',
  KeptLocal: 'info',
  LocallyModified: 'warning',
  Missing: 'warning',
  Superseded: 'neutral',
  Skipped: 'neutral',
};

const content: Record<ContentStatus, StatusTone> = {
  Available: 'success',
  Empty: 'neutral',
  Processing: 'progress',
  Failed: 'danger',
  Unknown: 'neutral',
};

const sync: Record<SyncRunStatus, StatusTone> = {
  Running: 'progress',
  Succeeded: 'success',
  PartiallySucceeded: 'warning',
  Failed: 'danger',
  Cancelled: 'neutral',
  Interrupted: 'warning',
};

const certainty: Record<Certainty, StatusTone> = {
  Certain: 'success',
  High: 'success',
  Medium: 'info',
  Low: 'warning',
  None: 'neutral',
};

const labels: Partial<Record<string, string>> = {
  NeedsReview: 'Needs review',
  MultiProject: 'Multi-project',
  KeptLocal: 'Kept local',
  LocallyModified: 'Locally modified',
  PartiallySucceeded: 'Partial',
};

const maps: Record<StatusKind, Record<string, StatusTone>> = {
  classification, export: exportStatus, content, sync, certainty,
};

export function statusTone(kind: StatusKind, value: string | null | undefined): StatusTone {
  if (!value) return 'neutral';
  return maps[kind][value] ?? 'neutral';
}

export function statusColor(kind: StatusKind, value: string | null | undefined): string {
  return TONE_COLORS[statusTone(kind, value)];
}

export function statusLabel(value: string | null | undefined): string {
  if (!value) return 'Unknown';
  return labels[value] ?? value;
}
