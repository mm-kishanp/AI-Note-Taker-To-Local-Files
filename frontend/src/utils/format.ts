import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';

dayjs.extend(relativeTime);

export function formatDateTime(value: string | null | undefined, fallback = '—'): string {
  if (!value) return fallback;
  const d = dayjs(value);
  return d.isValid() ? d.format('YYYY-MM-DD HH:mm') : fallback;
}

export function formatDate(value: string | null | undefined, fallback = '—'): string {
  if (!value) return fallback;
  const d = dayjs(value);
  return d.isValid() ? d.format('YYYY-MM-DD') : fallback;
}

export function formatRelative(value: string | null | undefined, fallback = 'never'): string {
  if (!value) return fallback;
  const d = dayjs(value);
  return d.isValid() ? d.fromNow() : fallback;
}

export function formatTime(value: string | null | undefined): string {
  if (!value) return '';
  const d = dayjs(value);
  return d.isValid() ? d.format('HH:mm:ss') : '';
}

export function formatDuration(ms: number | null | undefined): string {
  if (ms === null || ms === undefined || ms < 0) return '—';
  if (ms < 1000) return `${ms} ms`;
  const s = Math.round(ms / 1000);
  if (s < 60) return `${s}s`;
  const m = Math.floor(s / 60);
  const rem = s % 60;
  if (m < 60) return `${m}m ${rem}s`;
  return `${Math.floor(m / 60)}h ${m % 60}m`;
}

export function formatBytes(bytes: number | null | undefined): string {
  if (bytes === null || bytes === undefined) return '—';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let v = bytes;
  let i = 0;
  while (v >= 1024 && i < units.length - 1) {
    v /= 1024;
    i++;
  }
  return `${v.toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
}

/** Adds spaces to PascalCase enum values for display: "NeedsReview" -> "Needs review". */
export function humanize(value: string | null | undefined): string {
  if (!value) return '';
  const spaced = value.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/([A-Z])([A-Z][a-z])/g, '$1 $2');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}

export function truncateList(items: string[], max: number): { shown: string; hidden: number } {
  const unique = Array.from(new Set(items.filter(Boolean)));
  return { shown: unique.slice(0, max).join(', '), hidden: Math.max(0, unique.length - max) };
}

/** ISO date (yyyy-mm-dd) -> start of day ISO string for the API. */
export function dateParamToIso(value: string | null | undefined, endOfDay = false): string | undefined {
  if (!value) return undefined;
  const d = dayjs(value);
  if (!d.isValid()) return undefined;
  return (endOfDay ? d.endOf('day') : d.startOf('day')).toISOString();
}

export function downloadJson(filename: string, data: unknown) {
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
