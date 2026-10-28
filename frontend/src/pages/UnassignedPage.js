import { jsx as _jsx, jsxs as _jsxs, Fragment as _Fragment } from "react/jsx-runtime";
import { Alert, Anchor, Badge, Button, Card, Group, Menu, Pagination, Stack, Text, Tooltip, UnstyledButton } from '@mantine/core';
import { modals } from '@mantine/modals';
import { IconBan, IconBulb, IconChevronDown, IconFolderPlus, IconLink, IconRefresh, IconShare, IconSparkles, IconTag, } from '@tabler/icons-react';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { EmptyState } from '../components/EmptyState';
import { PageHeader } from '../components/PageHeader';
import { ProjectFormModal } from '../components/ProjectFormModal';
import { LoadingRows, QueryState } from '../components/QueryState';
import { ReassignModal } from '../components/ReassignModal';
import { SaveMappingModal } from '../components/SaveMappingModal';
import { StatusBadge } from '../components/StatusBadge';
import { useReclassify } from '../hooks/useMeetings';
import { useAssignUnassigned, useUnassigned } from '../hooks/useUnassigned';
import { formatDateTime } from '../utils/format';
import { notifyError, notifySuccess } from '../utils/notify';
function Chip({ label, onClick, title }) {
    return (_jsx(Tooltip, { label: title, withArrow: true, openDelay: 300, children: _jsx(UnstyledButton, { onClick: onClick, "aria-label": title, children: _jsx(Badge, { variant: "light", color: "gray", style: { textTransform: 'none', cursor: 'pointer' }, leftSection: _jsx(IconTag, { size: 10 }), children: label }) }) }));
}
function UnassignedCard({ item, onAssign, onMapping, onCreateProject, }) {
    const m = item.meeting;
    const reclassify = useReclassify();
    const assign = useAssignUnassigned();
    const external = item.participants.filter((p) => !p.isOwner);
    const exclude = () => modals.openConfirmModal({
        title: 'Exclude from export?',
        children: _jsxs(Text, { size: "sm", children: ["\u201C", m.title, "\u201D will not be exported and leaves the review queue."] }),
        labels: { confirm: 'Exclude', cancel: 'Cancel' },
        confirmProps: { color: 'red' },
        onConfirm: () => assign.mutate({ id: m.id, req: { kind: 'Excluded', projectId: null, note: null } }, { onSuccess: () => notifySuccess('Meeting excluded.'), onError: (e) => notifyError(e) }),
    });
    return (_jsx(Card, { withBorder: true, radius: "md", component: "article", "aria-labelledby": `un-${m.id}`, children: _jsxs(Stack, { gap: "sm", children: [_jsxs(Group, { justify: "space-between", align: "flex-start", wrap: "wrap", children: [_jsxs("div", { style: { minWidth: 0 }, children: [_jsx(Anchor, { component: Link, to: `/meetings/${m.id}`, fw: 600, id: `un-${m.id}`, children: m.title || '(untitled)' }), _jsxs(Group, { gap: "xs", children: [_jsx(Text, { size: "sm", c: "dimmed", children: formatDateTime(m.meetingDate) }), _jsx(StatusBadge, { kind: "classification", value: m.classificationStatus, srLabel: "Classification" })] })] }), _jsxs(Group, { gap: "xs", children: [_jsx(Button, { size: "xs", onClick: () => onAssign({ item, kind: 'Project', projectId: null }), children: "Assign to project" }), _jsxs(Menu, { position: "bottom-end", children: [_jsx(Menu.Target, { children: _jsx(Button, { size: "xs", variant: "default", rightSection: _jsx(IconChevronDown, { size: 14 }), children: "More actions" }) }), _jsxs(Menu.Dropdown, { children: [_jsx(Menu.Item, { leftSection: _jsx(IconShare, { size: 16 }), onClick: () => onAssign({ item, kind: 'Shared', projectId: null }), children: "Assign to Shared" }), _jsx(Menu.Item, { leftSection: _jsx(IconLink, { size: 16 }), onClick: () => onMapping({ item, kind: 'Email', value: external.find((p) => p.email)?.email ?? '' }), children: "Save mapping\u2026" }), _jsx(Menu.Item, { leftSection: _jsx(IconFolderPlus, { size: 16 }), onClick: () => onCreateProject(item), children: "Create new project\u2026" }), _jsx(Menu.Item, { leftSection: _jsx(IconRefresh, { size: 16 }), onClick: () => reclassify.mutate(m.id, {
                                                        onSuccess: (d) => notifySuccess(`${d.classification.status}: ${d.classification.reason}`, 'Re-classified'),
                                                        onError: (e) => notifyError(e),
                                                    }), children: "Retry classification" }), _jsx(Menu.Divider, {}), _jsx(Menu.Item, { leftSection: _jsx(IconBan, { size: 16 }), color: "red", onClick: exclude, children: "Exclude" })] })] })] })] }), _jsxs(Text, { size: "sm", children: [_jsx(Text, { span: true, fw: 600, children: "Why: " }), item.reason] }), item.suggestedNextAction && (_jsxs(Text, { size: "sm", c: "dimmed", children: [_jsx(IconBulb, { size: 14, style: { verticalAlign: 'middle' }, "aria-hidden": true }), " ", item.suggestedNextAction] })), item.aiSuggestion && (_jsx(Alert, { color: "violet", variant: "light", icon: _jsx(IconSparkles, { size: 16 }), title: "AI suggestion", p: "xs", children: _jsx(Text, { size: "sm", children: item.aiSuggestion }) })), item.candidates.length > 0 && (_jsxs("div", { children: [_jsx(Text, { size: "xs", c: "dimmed", tt: "uppercase", fw: 600, mb: 4, children: "Candidate projects" }), _jsx(Group, { gap: 6, children: item.candidates.map((c) => (_jsxs(Button, { size: "compact-xs", variant: "light", onClick: () => onAssign({ item, kind: 'Project', projectId: c.id }), children: ["Assign to ", c.name] }, c.id))) })] })), _jsxs("div", { children: [_jsx(Text, { size: "xs", c: "dimmed", tt: "uppercase", fw: 600, mb: 4, children: "Participants \u2014 click to create a mapping" }), _jsxs(Group, { gap: 6, children: [external.length === 0 && _jsx(Text, { size: "sm", c: "dimmed", children: "No participants besides the owner." }), external.map((p, i) => {
                                    const label = p.email ? `${p.name ?? p.email} <${p.email}>` : p.name ?? '?';
                                    return (_jsx(Chip, { label: p.name && p.email ? `${p.name} · ${p.email}` : label, title: `Map ${p.email ?? p.name} to a project`, onClick: () => onMapping({ item, kind: p.email ? 'Email' : 'ParticipantName', value: p.email ?? p.name ?? '' }) }, i));
                                })] })] }), item.identifiedSpeakers.length > 0 && (_jsxs("div", { children: [_jsx(Text, { size: "xs", c: "dimmed", tt: "uppercase", fw: 600, mb: 4, children: "Identified speakers" }), _jsx(Group, { gap: 6, children: item.identifiedSpeakers.map((s) => (_jsx(Chip, { label: `${s.name} (${s.segments})`, title: `Map speaker ${s.name} to a project`, onClick: () => onMapping({ item, kind: 'SpeakerName', value: s.name }) }, s.name))) })] }))] }) }));
}
export function UnassignedPage() {
    const [page, setPage] = useState(1);
    const pageSize = 20;
    const q = useUnassigned(page, pageSize);
    const [assign, setAssign] = useState(null);
    const [mapping, setMapping] = useState(null);
    const [createFor, setCreateFor] = useState(null);
    const pages = Math.max(1, Math.ceil((q.data?.total ?? 0) / pageSize));
    return (_jsxs(_Fragment, { children: [_jsx(PageHeader, { title: "Unassigned", description: "Meetings the rules could not place with confidence. Assign them, or save a mapping so similar meetings are routed automatically." }), _jsx(QueryState, { isLoading: q.isLoading, error: q.error, onRetry: () => void q.refetch(), skeleton: _jsx(LoadingRows, { rows: 4, height: 140 }), children: q.data && q.data.items.length === 0 ? (_jsx(EmptyState, { title: "Nothing to review", description: "All meetings are assigned, shared or excluded." })) : (_jsxs(Stack, { gap: "md", style: { opacity: q.isPlaceholderData ? 0.6 : 1 }, children: [_jsxs(Text, { size: "sm", c: "dimmed", children: [q.data?.total, " meeting(s) need attention"] }), q.data?.items.map((item) => (_jsx(UnassignedCard, { item: item, onAssign: setAssign, onMapping: setMapping, onCreateProject: setCreateFor }, item.meeting.id))), pages > 1 && _jsx(Pagination, { total: pages, value: page, onChange: setPage })] })) }), assign && (_jsx(ReassignModal, { opened: true, meetingId: assign.item.meeting.id, meetingTitle: assign.item.meeting.title, initialKind: assign.kind, initialProjectId: assign.projectId, fromReviewQueue: true, onClose: () => setAssign(null) })), mapping && (_jsx(SaveMappingModal, { opened: true, meetingId: mapping.item.meeting.id, meetingTitle: mapping.item.meeting.title, initialKind: mapping.kind, initialValue: mapping.value, initialProjectId: mapping.projectId ?? null, onClose: () => setMapping(null) })), _jsx(ProjectFormModal, { opened: !!createFor, onClose: () => setCreateFor(null), initialName: createFor?.meeting.title, onSaved: (p) => {
                    const item = createFor;
                    setCreateFor(null);
                    if (item)
                        setAssign({ item, kind: 'Project', projectId: p.id });
                } })] }));
}
