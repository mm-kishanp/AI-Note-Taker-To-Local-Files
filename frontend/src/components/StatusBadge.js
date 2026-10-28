import { jsx as _jsx } from "react/jsx-runtime";
import { Badge } from '@mantine/core';
import { statusColor, statusLabel, statusTone } from '../utils/status';
/** Consistent status badge. The text always carries the meaning; color is a secondary cue. */
export function StatusBadge({ kind, value, srLabel, variant = 'light', size = 'sm', ...rest }) {
    const label = statusLabel(value);
    return (_jsx(Badge, { color: statusColor(kind, value), variant: variant, size: size, "data-tone": statusTone(kind, value), "aria-label": srLabel ? `${srLabel}: ${label}` : undefined, style: { textTransform: 'none' }, ...rest, children: label }));
}
