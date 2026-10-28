import { jsxs as _jsxs, jsx as _jsx, Fragment as _Fragment } from "react/jsx-runtime";
import { Alert, Anchor, Button, Checkbox, Group, Modal, NativeSelect, Stack, Table, Text, TextInput } from '@mantine/core';
import { modals } from '@mantine/modals';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useApplyMapping, useProjects } from '../hooks/useProjects';
import { useSaveMapping } from '../hooks/useUnassigned';
import { ApiError } from '../services/api';
import { RULE_KINDS } from '../types/api';
import { formatDate } from '../utils/format';
import { notifyInfo, notifySuccess, notifyError } from '../utils/notify';
import { RULE_KIND_INFO } from '../utils/rules';
import { ProblemAlert } from './ProblemAlert';
import { StatusBadge } from './StatusBadge';
/**
 * Saves a rule (participant email/name/speaker → project) from the review queue, then shows which other unassigned
 * meetings would now match and offers a bulk apply that requires explicit confirmation.
 */
export function SaveMappingModal({ meetingId, meetingTitle, opened, onClose, initialKind = 'Email', initialValue = '', initialProjectId = null, }) {
    const projects = useProjects('', false);
    const save = useSaveMapping();
    const apply = useApplyMapping();
    const [projectId, setProjectId] = useState(initialProjectId ?? '');
    const [kind, setKind] = useState(initialKind);
    const [value, setValue] = useState(initialValue);
    const [assignThis, setAssignThis] = useState(true);
    const [preview, setPreview] = useState(null);
    const [selected, setSelected] = useState(new Set());
    useEffect(() => {
        if (!opened)
            return;
        setProjectId(initialProjectId ?? '');
        setKind(initialKind);
        setValue(initialValue);
        setAssignThis(true);
        setPreview(null);
        setSelected(new Set());
        save.reset();
        apply.reset();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [opened, initialKind, initialValue, initialProjectId]);
    const others = (preview?.items ?? []).filter((i) => i.meetingId !== meetingId && i.changes);
    const err = save.error instanceof ApiError ? save.error : null;
    const submit = () => save.mutate({ id: meetingId, req: { projectId, kind, value: value.trim(), assignThisMeeting: assignThis } }, {
        onSuccess: (r) => {
            setPreview(r);
            setSelected(new Set(r.items.filter((i) => i.meetingId !== meetingId && i.changes && !i.isManual).map((i) => i.meetingId)));
            notifySuccess(assignThis ? 'Rule saved and this meeting assigned.' : 'Rule saved.');
        },
    });
    const projectName = projects.data?.find((p) => p.id === projectId)?.name ?? 'the project';
    const confirmApply = () => modals.openConfirmModal({
        title: `Apply to ${selected.size} other meeting(s)?`,
        children: (_jsxs(Text, { size: "sm", children: ["The selected meetings will be re-classified with the saved rules of \u201C", projectName, "\u201D and re-exported in the background. Manual assignments are kept."] })),
        labels: { confirm: 'Apply mapping', cancel: 'Cancel' },
        onConfirm: () => apply.mutate({ id: projectId, req: { meetingIds: Array.from(selected), confirm: true } }, {
            onSuccess: (r) => {
                notifySuccess(r.message || 'Re-classification started.', 'Started');
                onClose();
            },
            onError: (e) => (e instanceof ApiError && e.status === 409 ? notifyInfo(e.detail ?? 'A sync is already running.') : notifyError(e)),
        }),
    });
    return (_jsx(Modal, { opened: opened, onClose: onClose, title: `Save mapping from “${meetingTitle}”`, size: "lg", children: !preview ? (_jsx("form", { onSubmit: (e) => {
                e.preventDefault();
                submit();
            }, children: _jsxs(Stack, { gap: "sm", children: [_jsx(NativeSelect, { label: "Project", required: true, value: projectId, onChange: (e) => setProjectId(e.currentTarget.value), data: [{ value: '', label: projects.isLoading ? 'Loading…' : 'Choose a project' }, ...(projects.data ?? []).map((p) => ({ value: p.id, label: p.name }))], error: err?.fieldError('projectId') }), _jsx(NativeSelect, { label: "Rule kind", value: kind, onChange: (e) => setKind(e.currentTarget.value), data: RULE_KINDS.map((k) => ({ value: k, label: RULE_KIND_INFO[k].label })), description: RULE_KIND_INFO[kind].help }), _jsx(TextInput, { label: "Value", required: true, value: value, onChange: (e) => setValue(e.currentTarget.value), placeholder: RULE_KIND_INFO[kind].placeholder, error: err?.fieldError('rule') ?? err?.fieldError('value'), ff: "monospace" }), _jsx(Checkbox, { label: "Also assign this meeting to the project", checked: assignThis, onChange: (e) => setAssignThis(e.currentTarget.checked) }), save.error && _jsx(ProblemAlert, { error: save.error }), _jsxs(Group, { justify: "flex-end", children: [_jsx(Button, { variant: "default", onClick: onClose, children: "Cancel" }), _jsx(Button, { type: "submit", loading: save.isPending, disabled: !projectId || !value.trim(), children: "Save mapping" })] })] }) })) : (_jsxs(Stack, { gap: "sm", children: [_jsxs(Alert, { color: "green", variant: "light", title: "Mapping saved", children: [RULE_KIND_INFO[kind].label, " ", _jsx("b", { children: value }), " \u2192 ", projectName, ". Evaluated ", preview.evaluated, " meeting(s); ", preview.wouldMatch, " would match."] }), others.length === 0 ? (_jsx(Text, { size: "sm", c: "dimmed", "data-testid": "no-other-matches", children: "No other unassigned meetings would change with this mapping." })) : (_jsxs(_Fragment, { children: [_jsxs(Text, { size: "sm", children: [others.length, " other meeting(s) would now be assigned. Select the ones to update:"] }), _jsxs(Table, { verticalSpacing: 4, "data-testid": "mapping-preview", children: [_jsx(Table.Thead, { children: _jsxs(Table.Tr, { children: [_jsx(Table.Th, { w: 40 }), _jsx(Table.Th, { children: "Meeting" }), _jsx(Table.Th, { children: "Current" }), _jsx(Table.Th, { children: "Proposed" })] }) }), _jsx(Table.Tbody, { children: others.map((i) => (_jsxs(Table.Tr, { children: [_jsx(Table.Td, { children: _jsx(Checkbox, { "aria-label": `Select ${i.title}`, checked: selected.has(i.meetingId), disabled: i.isManual, onChange: () => setSelected((s) => {
                                                        const n = new Set(s);
                                                        if (n.has(i.meetingId))
                                                            n.delete(i.meetingId);
                                                        else
                                                            n.add(i.meetingId);
                                                        return n;
                                                    }) }) }), _jsxs(Table.Td, { children: [_jsx(Anchor, { component: Link, to: `/meetings/${i.meetingId}`, size: "sm", onClick: onClose, children: i.title }), _jsxs(Text, { size: "xs", c: "dimmed", children: [formatDate(i.meetingDate), i.isManual ? ' · manual assignment (kept)' : ''] })] }), _jsx(Table.Td, { children: _jsx(StatusBadge, { kind: "classification", value: i.currentStatus }) }), _jsx(Table.Td, { children: _jsx(Text, { size: "sm", children: i.proposedProject ?? i.proposedStatus }) })] }, i.meetingId))) })] })] })), apply.error && _jsx(ProblemAlert, { error: apply.error }), _jsxs(Group, { justify: "flex-end", children: [_jsx(Button, { variant: "default", onClick: onClose, children: "Done" }), others.length > 0 && (_jsxs(Button, { onClick: confirmApply, disabled: selected.size === 0, loading: apply.isPending, children: ["Apply to ", selected.size, " meeting(s)"] }))] })] })) }));
}
