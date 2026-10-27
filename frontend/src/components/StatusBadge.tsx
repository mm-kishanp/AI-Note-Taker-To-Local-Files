import { Badge, type BadgeProps } from '@mantine/core';
import { type StatusKind, statusColor, statusLabel, statusTone } from '../utils/status';

export interface StatusBadgeProps extends Omit<BadgeProps, 'color' | 'children'> {
  kind: StatusKind;
  value: string | null | undefined;
  /** Optional prefix for screen readers, e.g. "Export status". */
  srLabel?: string;
}

/** Consistent status badge. The text always carries the meaning; color is a secondary cue. */
export function StatusBadge({ kind, value, srLabel, variant = 'light', size = 'sm', ...rest }: StatusBadgeProps) {
  const label = statusLabel(value);
  return (
    <Badge
      color={statusColor(kind, value)}
      variant={variant}
      size={size}
      data-tone={statusTone(kind, value)}
      aria-label={srLabel ? `${srLabel}: ${label}` : undefined}
      style={{ textTransform: 'none' }}
      {...rest}
    >
      {label}
    </Badge>
  );
}
