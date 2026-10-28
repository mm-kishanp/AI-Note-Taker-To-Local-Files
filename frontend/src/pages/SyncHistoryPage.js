import { jsx as _jsx, jsxs as _jsxs, Fragment as _Fragment } from "react/jsx-runtime";
import { Badge, Drawer, Group, Pagination, Paper, SimpleGrid, Stack, Table, Text } from '@mantine/core';
import { IconHistory } from '@tabler/icons-react';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { EmptyState } from '../components/EmptyState';
import { PageHeader } from '../components/PageHeader';
import { LoadingRows, QueryState } from '../components/QueryState';
import { StatusBadge } from '../components/StatusBadge';
import { SyncNowButton } from '../components/SyncControls';
import { SyncRunsTable } from '../components/SyncRunsTable';
import { useSyncHistory, useSyncRun } from '../hooks/useSystem';
import { formatDateTime, formatDuration, formatTime, humanize } from '../utils/format';
const PAGE_SIZE = 20;
export function SyncHistoryPage() {
    const [page, setPage] = useState(1);
    const [selected, setSelected] = useState(null);
    const history = useSyncHistory(page, PAGE_SIZE);
    const totalPages = history.data ? Math.max(1, Math.ceil(history.data.total / PAGE_SIZE)) : 1;
    return (_jsxs(_Fragment, { children: [_jsx(PageHeader, { title: "Synchronization history", description: "Every sync run with its counters. Select a run to see per-meeting outcomes and errors.", actions: _jsx(SyncNowButton, {}) }), _jsx(QueryState, { isLoading: history.isLoading, error: history.error, onRetry: () => void history.refetch(), children: history.data && history.data.items.length === 0 ? (_jsx(EmptyState, { icon: _jsx(IconHistory, { size: 24 }), title: "No sync runs yet", description: "Start a sync to import meetings from Granola." })) : (_jsxs(Stack, { gap: "md", children: [_jsx(Paper, { withBorder: true, radius: "md", p: "xs", children: _jsx(SyncRunsTable, { runs: history.data?.items ?? [], onSelect: setSelected }) }), totalPages > 1 && (_jsx(Group, { justify: "center", children: _jsx(Pagination, { value: page, onChange: setPage, total: totalPages, "aria-label": "Sync history pages" }) }))] })) }), _jsx(RunDrawer, { run: selected, onClose: () => setSelected(null) })] }));
}
function Counter({ label, value, danger }) {
    return (_jsxs(Stack, { gap: 0, children: [_jsx(Text, { size: "xs", c: "dimmed", children: label }), _jsx(Text, { fw: 600, c: danger ? 'red' : undefined, children: value })] }));
}
function RunDrawer({ run, onClose }) {
    const detail = useSyncRun(run?.id ?? null);
    const r = detail.data?.run ?? run;
    return (_jsx(Drawer, { opened: !!run, onClose: onClose, position: "right", size: "xl", title: "Sync run details", children: r && (_jsxs(Stack, { gap: "md", children: [_jsxs(Group, { gap: "xs", children: [_jsx(StatusBadge, { kind: "sync", value: r.status, size: "md", srLabel: "Run status" }), _jsx(Badge, { variant: "outline", color: "gray", style: { textTransform: 'none' }, children: humanize(r.mode) }), _jsx(Badge, { variant: "outline", color: "gray", style: { textTransform: 'none' }, children: r.trigger })] }), _jsxs(Text, { size: "sm", children: ["Started ", formatDateTime(r.startedAt), " \u00B7 ", r.finishedAt ? `finished ${formatDateTime(r.finishedAt)} · ${formatDuration(r.durationMs)}` : 'still running'] }), r.error && (_jsx(Text, { size: "sm", c: "red", role: "alert", children: r.error })), _jsxs(SimpleGrid, { cols: { base: 2, sm: 4 }, children: [_jsx(Counter, { label: "Discovered", value: r.notesDiscovered }), _jsx(Counter, { label: "Processed", value: r.meetingsProcessed }), _jsx(Counter, { label: "Skipped (unchanged)", value: r.meetingsSkipped }), _jsx(Counter, { label: "Files updated", value: r.filesUpdated }), _jsx(Counter, { label: "Notes exported", value: r.notesExported }), _jsx(Counter, { label: "Transcripts exported", value: r.transcriptsExported }), _jsx(Counter, { label: "Conflicts", value: r.conflicts, danger: r.conflicts > 0 }), _jsx(Counter, { label: "API failures", value: r.apiFailures, danger: r.apiFailures > 0 }), _jsx(Counter, { label: "File-system failures", value: r.fileSystemFailures, danger: r.fileSystemFailures > 0 }), _jsx(Counter, { label: "Classification failures", value: r.classificationFailures, danger: r.classificationFailures > 0 })] }), _jsx(Text, { fw: 600, children: "Meeting outcomes" }), _jsx(Text, { size: "xs", c: "dimmed", children: "Unchanged meetings are not listed." }), detail.isLoading ? (_jsx(LoadingRows, { rows: 4 })) : (detail.data?.items.length ?? 0) === 0 ? (_jsx(Text, { size: "sm", c: "dimmed", children: "No per-meeting changes recorded for this run." })) : (_jsx(Table.ScrollContainer, { minWidth: 600, children: _jsxs(Table, { verticalSpacing: "xs", striped: true, children: [_jsx(Table.Thead, { children: _jsxs(Table.Tr, { children: [_jsx(Table.Th, { children: "Time" }), _jsx(Table.Th, { children: "Note" }), _jsx(Table.Th, { children: "Outcome" }), _jsx(Table.Th, { children: "Category" }), _jsx(Table.Th, { children: "Message" })] }) }), _jsx(Table.Tbody, { children: detail.data.items.map((i) => (_jsxs(Table.Tr, { children: [_jsx(Table.Td, { style: { whiteSpace: 'nowrap' }, children: formatTime(i.timestamp) }), _jsx(Table.Td, { children: i.meetingId ? (_jsx(Link, { to: `/meetings/${i.meetingId}`, onClick: onClose, children: i.granolaNoteId ?? 'meeting' })) : (_jsx(Text, { size: "sm", ff: "monospace", children: i.granolaNoteId })) }), _jsx(Table.Td, { children: humanize(i.outcome) }), _jsx(Table.Td, { children: i.errorCategory !== 'None' && (_jsxs(Text, { size: "sm", c: "red", children: [humanize(i.errorCategory), i.retryable ? ' (will retry)' : ''] })) }), _jsx(Table.Td, { maw: 320, children: _jsx(Text, { size: "xs", lineClamp: 3, title: i.message ?? undefined, children: i.message }) })] }, i.id))) })] }) }))] })) }));
}
