import { jsx as _jsx, jsxs as _jsxs, Fragment as _Fragment } from "react/jsx-runtime";
import { Alert, Anchor, Badge, Button, Card, Checkbox, Group, NumberInput, Paper, SimpleGrid, Stack, Table, Text, Title, } from '@mantine/core';
import { modals } from '@mantine/modals';
import { IconArchive, IconArrowLeft, IconEdit, IconFlask, IconRestore } from '@tabler/icons-react';
import { useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { EmptyState } from '../components/EmptyState';
import { MeetingsTable } from '../components/MeetingsTable';
import { PageHeader } from '../components/PageHeader';
import { PathText } from '../components/PathText';
import { ProblemAlert } from '../components/ProblemAlert';
import { ProjectFormModal } from '../components/ProjectFormModal';
import { LoadingRows, QueryState } from '../components/QueryState';
import { cleanRules, RuleEditor, toEditableRules } from '../components/RuleEditor';
import { StatusBadge } from '../components/StatusBadge';
import { useMeetings } from '../hooks/useMeetings';
import { useApplyMapping, useProject, useSaveProject, useSetProjectArchived, useTestMapping } from '../hooks/useProjects';
import { ApiError } from '../services/api';
import { formatDate, formatDateTime } from '../utils/format';
import { notifyError, notifyInfo, notifySuccess } from '../utils/notify';
function sameRules(a, p) {
    const norm = (rs) => JSON.stringify(cleanRules(rs));
    return norm(a) === norm(toEditableRules(p.rules));
}
function MappingPanel({ project, rules, dirty }) {
    const test = useTestMapping();
    const apply = useApplyMapping();
    const [limit, setLimit] = useState(500);
    const [result, setResult] = useState(null);
    const [selected, setSelected] = useState(new Set());
    const [onlyChanges, setOnlyChanges] = useState(true);
    const run = () => test.mutate({ id: project.id, req: { rules: cleanRules(rules), limit } }, {
        onSuccess: (r) => {
            setResult(r);
            setSelected(new Set(r.items.filter((i) => i.changes && !i.isManual).map((i) => i.meetingId)));
        },
    });
    const rows = (result?.items ?? []).filter((i) => !onlyChanges || i.changes);
    const toggle = (id) => setSelected((s) => {
        const n = new Set(s);
        if (n.has(id))
            n.delete(id);
        else
            n.add(id);
        return n;
    });
    const doApply = () => modals.openConfirmModal({
        title: `Apply rules to ${selected.size} meeting(s)?`,
        children: (_jsxs(Stack, { gap: "xs", children: [_jsxs(Text, { size: "sm", children: ["The selected meetings are re-classified with the ", _jsx("b", { children: "saved" }), " rules of \u201C", project.name, "\u201D and their files re-exported in the background. Manual assignments are always kept."] }), project && _jsx(Text, { size: "sm", children: "Files of reassigned meetings may move to new folders (if \u201Cmove files on reassignment\u201D is on)." })] })),
        labels: { confirm: 'Apply', cancel: 'Cancel' },
        onConfirm: () => apply.mutate({ id: project.id, req: { meetingIds: Array.from(selected), confirm: true } }, {
            onSuccess: (r) => notifySuccess(r.message || 'Re-classification started.', 'Started'),
            onError: (e) => (e instanceof ApiError && e.status === 409 ? notifyInfo(e.detail ?? 'A sync is already running.') : notifyError(e)),
        }),
    });
    return (_jsxs(Card, { withBorder: true, radius: "md", children: [_jsx(Title, { order: 4, mb: 4, children: "Test mapping" }), _jsx(Text, { size: "sm", c: "dimmed", mb: "sm", children: "Dry run of the rules currently in the editor (including unsaved changes) against existing meetings. Nothing is changed." }), _jsxs(Group, { align: "flex-end", gap: "sm", mb: "sm", children: [_jsx(NumberInput, { label: "Meetings to evaluate", value: limit, onChange: (v) => setLimit(Number(v) || 100), min: 1, max: 5000, w: 170 }), _jsx(Button, { leftSection: _jsx(IconFlask, { size: 16 }), onClick: run, loading: test.isPending, children: "Test mapping" })] }), test.error && _jsx(ProblemAlert, { error: test.error }), result && (_jsxs(Stack, { gap: "sm", children: [_jsxs(Group, { gap: "md", children: [_jsxs(Text, { size: "sm", children: ["Evaluated: ", _jsx("b", { children: result.evaluated })] }), _jsxs(Text, { size: "sm", children: ["Would match: ", _jsx("b", { children: result.wouldMatch })] }), _jsxs(Text, { size: "sm", children: ["Would change: ", _jsx("b", { children: result.wouldChange })] }), _jsx(Checkbox, { label: "Only show changes", checked: onlyChanges, onChange: (e) => setOnlyChanges(e.currentTarget.checked) })] }), rows.length === 0 ? (_jsx(Text, { size: "sm", c: "dimmed", children: "No meetings would change." })) : (_jsx(Table.ScrollContainer, { minWidth: 900, children: _jsxs(Table, { verticalSpacing: 4, striped: true, children: [_jsx(Table.Thead, { children: _jsxs(Table.Tr, { children: [_jsx(Table.Th, { w: 40, children: _jsx(Checkbox, { "aria-label": "Select all changing meetings", checked: rows.filter((r) => r.changes && !r.isManual).every((r) => selected.has(r.meetingId)) && selected.size > 0, indeterminate: selected.size > 0 && !rows.filter((r) => r.changes && !r.isManual).every((r) => selected.has(r.meetingId)), onChange: (e) => setSelected(e.currentTarget.checked ? new Set(rows.filter((r) => r.changes && !r.isManual).map((r) => r.meetingId)) : new Set()) }) }), _jsx(Table.Th, { children: "Meeting" }), _jsx(Table.Th, { children: "Current" }), _jsx(Table.Th, { children: "Proposed" }), _jsx(Table.Th, { children: "Reason" })] }) }), _jsx(Table.Tbody, { children: rows.map((i) => (_jsxs(Table.Tr, { children: [_jsx(Table.Td, { children: _jsx(Checkbox, { "aria-label": `Select ${i.title}`, checked: selected.has(i.meetingId), disabled: !i.changes || i.isManual, onChange: () => toggle(i.meetingId) }) }), _jsxs(Table.Td, { children: [_jsx(Anchor, { component: Link, to: `/meetings/${i.meetingId}`, size: "sm", children: i.title }), _jsx(Text, { size: "xs", c: "dimmed", children: formatDate(i.meetingDate) }), i.isManual && (_jsx(Badge, { size: "xs", color: "orange", variant: "light", style: { textTransform: 'none' }, children: "Manual \u2014 will be kept" }))] }), _jsxs(Table.Td, { children: [_jsx(StatusBadge, { kind: "classification", value: i.currentStatus }), _jsx(Text, { size: "xs", children: i.currentProject ?? '—' })] }), _jsxs(Table.Td, { children: [_jsx(StatusBadge, { kind: "classification", value: i.proposedStatus }), _jsx(Text, { size: "xs", fw: i.changes ? 600 : undefined, children: i.proposedProject ?? '—' })] }), _jsx(Table.Td, { maw: 320, children: _jsx(Text, { size: "xs", children: i.reason }) })] }, i.meetingId))) })] }) })), dirty && (_jsx(Alert, { color: "yellow", variant: "light", children: "You have unsaved rule changes. Applying uses the saved rules \u2014 save the rules first to apply what you tested." })), apply.error && _jsx(ProblemAlert, { error: apply.error }), _jsx(Group, { justify: "flex-end", children: _jsxs(Button, { onClick: doApply, disabled: selected.size === 0 || dirty, loading: apply.isPending, children: ["Apply to selected meetings (", selected.size, ")"] }) })] }))] }));
}
export function ProjectDetailPage() {
    const { id = '' } = useParams();
    const project = useProject(id);
    const save = useSaveProject();
    const archive = useSetProjectArchived();
    const [editOpen, setEditOpen] = useState(false);
    const [rules, setRules] = useState([]);
    const recent = useMeetings({ projectId: id, page: 1, pageSize: 10, sort: 'date', desc: true });
    useEffect(() => {
        if (project.data)
            setRules(toEditableRules(project.data.rules));
    }, [project.data]);
    const dirty = useMemo(() => (project.data ? !sameRules(rules, project.data) : false), [rules, project.data]);
    if (project.isLoading)
        return _jsx(LoadingRows, { rows: 10 });
    if (project.error || !project.data)
        return (_jsxs(_Fragment, { children: [_jsx(PageHeader, { title: "Project" }), _jsx(ProblemAlert, { error: project.error, onRetry: () => void project.refetch() })] }));
    const p = project.data;
    const saveRules = () => save.mutate({
        id: p.id,
        input: {
            name: p.name, slug: p.slug, description: p.description, clientId: p.clientId, status: p.status,
            notesFolderPath: p.notesFolderPath, transcriptFolderPath: p.transcriptFolderPath,
            defaultClassificationPriority: p.defaultClassificationPriority, rules: cleanRules(rules),
        },
    }, { onSuccess: () => notifySuccess('Rules saved. Use “Test mapping” to see which existing meetings would change.') });
    const toggleArchive = () => {
        const archiving = p.status === 'Active';
        modals.openConfirmModal({
            title: archiving ? `Archive “${p.name}”?` : `Restore “${p.name}”?`,
            children: (_jsx(Text, { size: "sm", children: archiving
                    ? 'Archived projects no longer receive new meetings from rules. Existing meetings and files are kept.'
                    : 'The project becomes active again and its rules are used for classification.' })),
            labels: { confirm: archiving ? 'Archive' : 'Restore', cancel: 'Cancel' },
            confirmProps: { color: archiving ? 'orange' : 'green' },
            onConfirm: () => archive.mutate({ id: p.id, archived: archiving }, { onSuccess: () => notifySuccess(archiving ? 'Project archived.' : 'Project restored.'), onError: (e) => notifyError(e) }),
        });
    };
    const ruleErrors = save.error instanceof ApiError ? save.error.errors.filter((e) => e.field.startsWith('rules')).map((e) => `${e.field}: ${e.message}`).join(' ') : '';
    return (_jsxs(_Fragment, { children: [_jsx(Anchor, { component: Link, to: "/projects", size: "sm", children: _jsxs(Group, { gap: 4, children: [_jsx(IconArrowLeft, { size: 14 }), " Projects"] }) }), _jsx(PageHeader, { title: p.name, description: _jsxs(Group, { gap: "xs", component: "span", children: [_jsx(Badge, { color: p.status === 'Active' ? 'green' : 'gray', variant: "light", style: { textTransform: 'none' }, children: p.status }), p.clientName && _jsxs("span", { children: ["Client: ", p.clientName] }), _jsxs("span", { children: [p.meetingCount, " meetings"] })] }), actions: _jsxs(_Fragment, { children: [_jsx(Button, { leftSection: _jsx(IconEdit, { size: 16 }), onClick: () => setEditOpen(true), children: "Edit" }), _jsx(Button, { variant: "default", leftSection: p.status === 'Active' ? _jsx(IconArchive, { size: 16 }) : _jsx(IconRestore, { size: 16 }), onClick: toggleArchive, loading: archive.isPending, children: p.status === 'Active' ? 'Archive' : 'Restore' })] }) }), _jsxs(Stack, { gap: "md", children: [_jsxs(SimpleGrid, { cols: { base: 1, lg: 2 }, spacing: "md", children: [_jsxs(Card, { withBorder: true, radius: "md", children: [_jsx(Title, { order: 4, mb: "sm", children: "Details" }), _jsxs(Stack, { gap: "xs", children: [_jsxs("div", { children: [_jsx(Text, { size: "xs", c: "dimmed", tt: "uppercase", fw: 600, children: "Slug" }), _jsx(Text, { size: "sm", ff: "monospace", children: p.slug })] }), p.description && (_jsxs("div", { children: [_jsx(Text, { size: "xs", c: "dimmed", tt: "uppercase", fw: 600, children: "Description" }), _jsx(Text, { size: "sm", style: { whiteSpace: 'pre-wrap' }, children: p.description })] })), _jsxs("div", { children: [_jsx(Text, { size: "xs", c: "dimmed", tt: "uppercase", fw: 600, children: "Priority" }), _jsx(Text, { size: "sm", children: p.defaultClassificationPriority })] }), _jsxs("div", { children: [_jsx(Text, { size: "xs", c: "dimmed", tt: "uppercase", fw: 600, children: "Created / updated" }), _jsxs(Text, { size: "sm", children: [formatDateTime(p.createdAt), " / ", formatDateTime(p.updatedAt)] })] })] })] }), _jsxs(Card, { withBorder: true, radius: "md", children: [_jsx(Title, { order: 4, mb: "sm", children: "Export folders" }), _jsxs(Stack, { gap: "xs", children: [_jsxs("div", { children: [_jsx(Text, { size: "xs", c: "dimmed", tt: "uppercase", fw: 600, children: "Notes directory" }), _jsx(PathText, { path: p.resolvedNotesDirectory }), p.notesFolderPath && _jsxs(Text, { size: "xs", c: "dimmed", children: ["Override template: ", _jsx(Text, { span: true, ff: "monospace", size: "xs", children: p.notesFolderPath })] })] }), _jsxs("div", { children: [_jsx(Text, { size: "xs", c: "dimmed", tt: "uppercase", fw: 600, children: "Transcripts directory" }), _jsx(PathText, { path: p.resolvedTranscriptsDirectory }), p.transcriptFolderPath && _jsxs(Text, { size: "xs", c: "dimmed", children: ["Override template: ", _jsx(Text, { span: true, ff: "monospace", size: "xs", children: p.transcriptFolderPath })] })] })] })] })] }), _jsxs(Card, { withBorder: true, radius: "md", children: [_jsx(RuleEditor, { value: rules, onChange: setRules, label: "Mapping rules", description: "Edit the rules, test them below, then save. Changes are not applied to existing meetings until you apply them.", error: ruleErrors || undefined }), save.error && !ruleErrors && _jsx(ProblemAlert, { error: save.error }), _jsxs(Group, { justify: "flex-end", mt: "sm", children: [_jsx(Button, { variant: "default", disabled: !dirty, onClick: () => setRules(toEditableRules(p.rules)), children: "Discard changes" }), _jsx(Button, { disabled: !dirty, loading: save.isPending, onClick: saveRules, children: "Save rules" })] })] }), _jsx(MappingPanel, { project: p, rules: rules, dirty: dirty }), _jsxs(Card, { withBorder: true, radius: "md", children: [_jsxs(Group, { justify: "space-between", mb: "sm", children: [_jsx(Title, { order: 4, children: "Recent meetings" }), _jsx(Button, { component: Link, to: `/meetings?projectId=${p.id}`, variant: "subtle", size: "xs", children: "All meetings in this project" })] }), _jsx(QueryState, { isLoading: recent.isLoading, error: recent.error, children: recent.data && recent.data.items.length > 0 ? (_jsx(Paper, { children: _jsx(MeetingsTable, { items: recent.data.items, compact: true }) })) : (_jsx(EmptyState, { title: "No meetings in this project yet" })) })] })] }), _jsx(ProjectFormModal, { opened: editOpen, onClose: () => setEditOpen(false), project: p })] }));
}
