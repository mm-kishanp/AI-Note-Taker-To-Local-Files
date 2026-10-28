/** Tone -> Mantine color. The badge text always carries the meaning; color is only a secondary signal. */
export const TONE_COLORS = {
    success: 'green',
    info: 'blue',
    warning: 'yellow',
    danger: 'red',
    neutral: 'gray',
    progress: 'cyan',
};
const classification = {
    Assigned: 'success',
    Shared: 'info',
    MultiProject: 'info',
    NeedsReview: 'warning',
    Unassigned: 'warning',
    Pending: 'neutral',
    Excluded: 'neutral',
};
const exportStatus = {
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
const content = {
    Available: 'success',
    Empty: 'neutral',
    Processing: 'progress',
    Failed: 'danger',
    Unknown: 'neutral',
};
const sync = {
    Running: 'progress',
    Succeeded: 'success',
    PartiallySucceeded: 'warning',
    Failed: 'danger',
    Cancelled: 'neutral',
    Interrupted: 'warning',
};
const certainty = {
    Certain: 'success',
    High: 'success',
    Medium: 'info',
    Low: 'warning',
    None: 'neutral',
};
const labels = {
    NeedsReview: 'Needs review',
    MultiProject: 'Multi-project',
    KeptLocal: 'Kept local',
    LocallyModified: 'Locally modified',
    PartiallySucceeded: 'Partial',
};
const maps = {
    classification, export: exportStatus, content, sync, certainty,
};
export function statusTone(kind, value) {
    if (!value)
        return 'neutral';
    return maps[kind][value] ?? 'neutral';
}
export function statusColor(kind, value) {
    return TONE_COLORS[statusTone(kind, value)];
}
export function statusLabel(value) {
    if (!value)
        return 'Unknown';
    return labels[value] ?? value;
}
