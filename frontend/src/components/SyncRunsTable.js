import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { Table, Text, Tooltip } from '@mantine/core';
import { formatDateTime, formatDuration, humanize } from '../utils/format';
import { StatusBadge } from './StatusBadge';
export function SyncRunsTable({ runs, onSelect, compact }) {
    const onKey = (e, r) => {
        if (onSelect && (e.key === 'Enter' || e.key === ' ')) {
            e.preventDefault();
            onSelect(r);
        }
    };
    return (_jsx(Table.ScrollContainer, { minWidth: compact ? 560 : 1000, children: _jsxs(Table, { highlightOnHover: !!onSelect, verticalSpacing: "xs", striped: true, children: [_jsx(Table.Thead, { children: _jsxs(Table.Tr, { children: [_jsx(Table.Th, { children: "Started" }), _jsx(Table.Th, { children: "Mode" }), !compact && _jsx(Table.Th, { children: "Trigger" }), _jsx(Table.Th, { children: "Status" }), _jsx(Table.Th, { children: "Processed" }), !compact && _jsx(Table.Th, { children: "Discovered" }), !compact && _jsx(Table.Th, { children: "Notes / transcripts" }), !compact && _jsx(Table.Th, { children: "Files updated" }), _jsx(Table.Th, { children: "Failures" }), !compact && _jsx(Table.Th, { children: "Conflicts" }), _jsx(Table.Th, { children: "Duration" }), !compact && _jsx(Table.Th, { children: "Error" })] }) }), _jsx(Table.Tbody, { children: runs.map((r) => {
                        const failures = r.apiFailures + r.fileSystemFailures + r.classificationFailures;
                        return (_jsxs(Table.Tr, { className: onSelect ? 'gpi-clickable-row' : undefined, onClick: onSelect ? () => onSelect(r) : undefined, onKeyDown: (e) => onKey(e, r), tabIndex: onSelect ? 0 : undefined, "aria-label": onSelect ? `Open run started ${formatDateTime(r.startedAt)}` : undefined, children: [_jsx(Table.Td, { style: { whiteSpace: 'nowrap' }, children: formatDateTime(r.startedAt) }), _jsx(Table.Td, { children: humanize(r.mode) }), !compact && _jsx(Table.Td, { children: r.trigger }), _jsx(Table.Td, { children: _jsx(StatusBadge, { kind: "sync", value: r.status, srLabel: "Run status" }) }), _jsx(Table.Td, { children: r.meetingsProcessed }), !compact && _jsx(Table.Td, { children: r.notesDiscovered }), !compact && (_jsxs(Table.Td, { children: [r.notesExported, " / ", r.transcriptsExported] })), !compact && _jsx(Table.Td, { children: r.filesUpdated }), _jsx(Table.Td, { children: _jsx(Tooltip, { label: `API ${r.apiFailures} · File system ${r.fileSystemFailures} · Classification ${r.classificationFailures}`, withArrow: true, children: _jsx(Text, { size: "sm", c: failures > 0 ? 'red' : undefined, fw: failures > 0 ? 600 : undefined, children: failures }) }) }), !compact && _jsx(Table.Td, { children: r.conflicts }), _jsx(Table.Td, { style: { whiteSpace: 'nowrap' }, children: r.finishedAt ? formatDuration(r.durationMs) : 'running…' }), !compact && (_jsx(Table.Td, { maw: 260, children: _jsx(Text, { size: "xs", c: "red", lineClamp: 2, title: r.error ?? undefined, children: r.error }) }))] }, r.id));
                    }) })] }) }));
}
