import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { Anchor, Badge, Group, Table, Text, Tooltip, UnstyledButton } from '@mantine/core';
import { IconAlertCircle, IconArrowDown, IconArrowUp, IconSelector } from '@tabler/icons-react';
import { Link, useNavigate } from 'react-router-dom';
import { formatDateTime, formatRelative, truncateList } from '../utils/format';
import { StatusBadge } from './StatusBadge';
function SortHeader({ label, k, sort, desc, onSort }) {
    if (!onSort)
        return _jsx(Table.Th, { children: label });
    const active = sort === k;
    const Icon = active ? (desc ? IconArrowDown : IconArrowUp) : IconSelector;
    return (_jsx(Table.Th, { "aria-sort": active ? (desc ? 'descending' : 'ascending') : 'none', children: _jsx(UnstyledButton, { onClick: () => onSort(k), "aria-label": `Sort by ${label}`, children: _jsxs(Group, { gap: 4, wrap: "nowrap", children: [_jsx(Text, { fw: 700, size: "sm", children: label }), _jsx(Icon, { size: 14, "aria-hidden": true })] }) }) }));
}
export function MeetingsTable({ items, sort, desc, onSort, compact }) {
    const navigate = useNavigate();
    const open = (id) => navigate(`/meetings/${id}`);
    const onKey = (e, id) => {
        if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            open(id);
        }
    };
    return (_jsx(Table.ScrollContainer, { minWidth: compact ? 600 : 1100, children: _jsxs(Table, { highlightOnHover: true, verticalSpacing: "xs", striped: true, children: [_jsx(Table.Thead, { children: _jsxs(Table.Tr, { children: [_jsx(SortHeader, { label: "Title", k: "title", sort: sort, desc: desc, onSort: onSort }), _jsx(SortHeader, { label: "Date", k: "date", sort: sort, desc: desc, onSort: onSort }), !compact && _jsx(Table.Th, { children: "Participants" }), _jsx(SortHeader, { label: "Project", k: "project", sort: sort, desc: desc, onSort: onSort }), _jsx(SortHeader, { label: "Classification", k: "status", sort: sort, desc: desc, onSort: onSort }), !compact && _jsx(Table.Th, { children: "Notes" }), !compact && _jsx(Table.Th, { children: "Transcript" }), _jsx(Table.Th, { children: "Export" }), !compact && _jsx(SortHeader, { label: "Last synced", k: "synced", sort: sort, desc: desc, onSort: onSort })] }) }), _jsx(Table.Tbody, { children: items.map((m) => {
                        const parts = truncateList(m.participants, 3);
                        return (_jsxs(Table.Tr, { className: "gpi-clickable-row", onClick: () => open(m.id), onKeyDown: (e) => onKey(e, m.id), tabIndex: 0, "aria-label": `Open meeting ${m.title}`, children: [_jsxs(Table.Td, { maw: 320, children: [_jsxs(Group, { gap: 4, wrap: "nowrap", children: [_jsx(Anchor, { component: Link, to: `/meetings/${m.id}`, onClick: (e) => e.stopPropagation(), size: "sm", fw: 500, lineClamp: 2, tabIndex: -1, children: m.title || '(untitled)' }), m.source === 'Fireflies' && (_jsx(Badge, { size: "xs", variant: "light", color: "violet", style: { flexShrink: 0 }, children: "Fireflies" })), m.lastError && (_jsx(Tooltip, { label: m.lastError, multiline: true, w: 300, children: _jsx(IconAlertCircle, { size: 16, color: "var(--mantine-color-red-6)", "aria-label": `Error: ${m.lastError}` }) }))] }), m.sourceStatus !== 'Active' && (_jsxs(Text, { size: "xs", c: "red", children: ["Source ", m.sourceStatus.toLowerCase()] }))] }), _jsx(Table.Td, { style: { whiteSpace: 'nowrap' }, children: _jsx(Text, { size: "sm", children: formatDateTime(m.meetingDate) }) }), !compact && (_jsx(Table.Td, { maw: 240, children: _jsxs(Text, { size: "sm", lineClamp: 1, title: m.participants.join(', '), children: [parts.shown || '—', parts.hidden > 0 && _jsxs(Text, { span: true, c: "dimmed", children: [" +", parts.hidden] })] }) })), _jsxs(Table.Td, { children: [_jsx(Text, { size: "sm", children: m.projectName ?? _jsx(Text, { span: true, c: "dimmed", children: "\u2014" }) }), m.clientName && (_jsx(Text, { size: "xs", c: "dimmed", children: m.clientName }))] }), _jsx(Table.Td, { children: _jsx(StatusBadge, { kind: "classification", value: m.classificationStatus, srLabel: "Classification" }) }), !compact && (_jsx(Table.Td, { children: _jsx(StatusBadge, { kind: "content", value: m.notesStatus, srLabel: "Notes" }) })), !compact && (_jsx(Table.Td, { children: _jsx(StatusBadge, { kind: "content", value: m.transcriptStatus, srLabel: "Transcript" }) })), _jsx(Table.Td, { children: _jsx(StatusBadge, { kind: "export", value: m.exportStatus, srLabel: "Export" }) }), !compact && (_jsx(Table.Td, { style: { whiteSpace: 'nowrap' }, children: _jsx(Text, { size: "sm", title: formatDateTime(m.lastSyncedAt), children: formatRelative(m.lastSyncedAt) }) }))] }, m.id));
                    }) })] }) }));
}
