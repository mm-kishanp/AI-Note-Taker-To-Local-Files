import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { Badge, Button, Group, Loader, Menu, Progress, Text, Tooltip } from '@mantine/core';
import { IconChevronDown, IconPlayerStop, IconRefresh } from '@tabler/icons-react';
import { useCancelSync, useStartSync, useSyncStatus } from '../hooks/useSystem';
import { ApiError } from '../services/api';
import { formatRelative } from '../utils/format';
import { notifyError, notifyInfo, notifySuccess } from '../utils/notify';
import { statusColor, statusLabel } from '../utils/status';
export const SYNC_MODE_OPTIONS = [
    { mode: 'Incremental', label: 'Incremental sync', description: 'Fetch new and changed meetings since the last checkpoint.' },
    { mode: 'Full', label: 'Full sync', description: 'Re-list every meeting from Granola and refresh changed ones.' },
    { mode: 'RetryFailed', label: 'Retry failed', description: 'Retry meetings whose last fetch or export failed.' },
    { mode: 'Reconcile', label: 'Reconcile files', description: 'Check exported files on disk for missing or edited files.' },
];
/** Compact live status chip shown in the header. */
export function SyncStatusChip() {
    const { data, error } = useSyncStatus();
    if (error) {
        return (_jsx(Badge, { color: "red", variant: "light", style: { textTransform: 'none' }, role: "status", children: "Backend unreachable" }));
    }
    if (!data)
        return _jsx(Loader, { size: "xs", "aria-label": "Loading sync status" });
    if (data.isRunning) {
        const total = Math.max(data.queued, data.discovered, data.processed, 1);
        const pct = Math.min(100, Math.round((data.processed / total) * 100));
        return (_jsx(Tooltip, { label: data.currentItem ? `Current: ${data.currentItem}` : data.phase, withArrow: true, children: _jsxs(Group, { gap: 6, wrap: "nowrap", role: "status", "aria-live": "polite", "aria-label": `Sync running: ${data.phase}, ${data.processed} of ${total} processed`, children: [_jsx(Loader, { size: 14 }), _jsxs(Text, { size: "sm", fw: 500, children: ["Syncing \u00B7 ", data.phase] }), _jsx(Progress, { value: pct, w: 80, size: "sm", "aria-hidden": true }), _jsxs(Text, { size: "xs", c: "dimmed", children: [data.processed, "/", total, data.failed > 0 ? ` · ${data.failed} failed` : ''] })] }) }));
    }
    return (_jsxs(Group, { gap: 6, wrap: "nowrap", role: "status", "aria-live": "polite", children: [_jsx(Badge, { variant: "dot", color: "gray", style: { textTransform: 'none' }, children: "Idle" }), data.lastRunStatus && (_jsxs(Text, { size: "xs", c: "dimmed", visibleFrom: "md", children: ["Last run", ' ', _jsx(Text, { span: true, c: statusColor('sync', data.lastRunStatus), fw: 600, size: "xs", children: statusLabel(data.lastRunStatus) }), ' ', formatRelative(data.lastRunFinishedAt)] }))] }));
}
/** "Sync now" split button with a mode menu; Incremental is the default. Shows Cancel while running. */
export function SyncNowButton({ size = 'sm' }) {
    const status = useSyncStatus();
    const start = useStartSync();
    const cancel = useCancelSync();
    const running = status.data?.isRunning ?? false;
    const run = (mode) => start.mutate({ mode }, {
        onSuccess: (r) => notifySuccess(r.message || `${mode} sync started`, 'Sync started'),
        onError: (e) => {
            if (e instanceof ApiError && e.status === 409)
                notifyInfo(e.detail ?? 'A synchronization is already running.', 'Already running');
            else
                notifyError(e, 'Could not start sync');
        },
    });
    if (running) {
        return (_jsx(Button, { size: size, color: "red", variant: "light", leftSection: _jsx(IconPlayerStop, { size: 16 }), loading: cancel.isPending, onClick: () => cancel.mutate(undefined, {
                onSuccess: () => notifyInfo('Cancellation requested.'),
                onError: (e) => notifyError(e),
            }), children: "Cancel sync" }));
    }
    return (_jsxs(Group, { gap: 0, wrap: "nowrap", children: [_jsx(Button, { size: size, leftSection: _jsx(IconRefresh, { size: 16 }), loading: start.isPending, onClick: () => run('Incremental'), style: { borderTopRightRadius: 0, borderBottomRightRadius: 0 }, children: "Sync now" }), _jsxs(Menu, { position: "bottom-end", withinPortal: true, children: [_jsx(Menu.Target, { children: _jsx(Button, { size: size, px: 8, "aria-label": "Choose sync mode", style: { borderTopLeftRadius: 0, borderBottomLeftRadius: 0, borderLeft: '1px solid rgba(255,255,255,0.35)' }, disabled: start.isPending, children: _jsx(IconChevronDown, { size: 16 }) }) }), _jsxs(Menu.Dropdown, { children: [_jsx(Menu.Label, { children: "Sync mode" }), SYNC_MODE_OPTIONS.map((o) => (_jsxs(Menu.Item, { onClick: () => run(o.mode), children: [_jsx(Text, { size: "sm", fw: 500, children: o.label }), _jsx(Text, { size: "xs", c: "dimmed", maw: 260, children: o.description })] }, o.mode)))] })] })] }));
}
