import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { Alert, Button, Group, Modal, Paper, SegmentedControl, Stack, Text, Textarea } from '@mantine/core';
import { IconArrowRight } from '@tabler/icons-react';
import { useEffect, useState } from 'react';
import { useAssignMeeting, useDestinationPreview } from '../hooks/useMeetings';
import { useAssignUnassigned } from '../hooks/useUnassigned';
import { notifySuccess } from '../utils/notify';
import { PathText } from './PathText';
import { ProblemAlert } from './ProblemAlert';
import { ProjectSelect } from './ProjectSelect';
import { LoadingRows } from './QueryState';
const KIND_OPTIONS = [
    { value: 'Project', label: 'Project' },
    { value: 'Shared', label: 'Shared' },
    { value: 'Excluded', label: 'Excluded' },
    { value: 'None', label: 'Automatic' },
];
const KIND_HELP = {
    Project: 'Pin this meeting to a project. Rules will no longer move it.',
    Shared: 'File under the Shared folders (cross-project meetings).',
    Excluded: 'Do not export this meeting. Existing generated files are left on disk.',
    None: 'Remove the manual assignment and let the classification rules decide again.',
};
/** Reassign a meeting. The destination preview is fetched and shown before the assignment can be confirmed. */
export function ReassignModal({ meetingId, meetingTitle, opened, onClose, initialKind = 'Project', initialProjectId = null, fromReviewQueue, onAssigned, }) {
    const [kind, setKind] = useState(initialKind);
    const [projectId, setProjectId] = useState(initialProjectId);
    const [note, setNote] = useState('');
    useEffect(() => {
        if (opened) {
            setKind(initialKind);
            setProjectId(initialProjectId);
            setNote('');
        }
    }, [opened, initialKind, initialProjectId]);
    const ready = kind !== 'Project' || !!projectId;
    const preview = useDestinationPreview(meetingId, kind, projectId, opened && ready);
    const assignMeeting = useAssignMeeting();
    const assignReview = useAssignUnassigned();
    const mutation = fromReviewQueue ? assignReview : assignMeeting;
    const confirm = () => {
        mutation.mutate({ id: meetingId, req: { kind, projectId: kind === 'Project' ? projectId : null, note: note.trim() || null } }, {
            onSuccess: (detail) => {
                notifySuccess(kind === 'None' ? 'Manual assignment cleared.' : `Meeting assigned to ${kind === 'Project' ? detail.meeting.projectName ?? 'project' : kind}.`, 'Assignment saved');
                onAssigned?.(detail);
                onClose();
            },
        });
    };
    const canConfirm = ready && preview.isSuccess && !preview.isFetching && !mutation.isPending;
    return (_jsx(Modal, { opened: opened, onClose: onClose, title: meetingTitle ? `Reassign “${meetingTitle}”` : 'Reassign meeting', size: "lg", children: _jsxs(Stack, { gap: "md", children: [_jsxs("div", { children: [_jsx(Text, { size: "sm", fw: 500, mb: 4, id: "assign-kind-label", children: "Assignment" }), _jsx(SegmentedControl, { "aria-labelledby": "assign-kind-label", data: KIND_OPTIONS, value: kind, onChange: (v) => setKind(v), fullWidth: true }), _jsx(Text, { size: "xs", c: "dimmed", mt: 4, children: KIND_HELP[kind] })] }), kind === 'Project' && _jsx(ProjectSelect, { value: projectId, onChange: setProjectId, required: true }), _jsx(Textarea, { label: "Note (optional)", value: note, onChange: (e) => setNote(e.currentTarget.value), autosize: true, minRows: 1, maxRows: 4 }), _jsxs(Paper, { withBorder: true, p: "sm", radius: "sm", children: [_jsxs(Group, { gap: 6, mb: 6, children: [_jsx(IconArrowRight, { size: 16, "aria-hidden": true }), _jsx(Text, { size: "sm", fw: 600, children: "Destination preview" })] }), !ready ? (_jsx(Text, { size: "sm", c: "dimmed", children: "Choose a project to see where the files will be written." })) : preview.isLoading || preview.isFetching ? (_jsx(LoadingRows, { rows: 2, height: 18 })) : preview.error ? (_jsx(ProblemAlert, { error: preview.error, title: "Could not preview destination" })) : (preview.data?.length ?? 0) === 0 ? (_jsx(Text, { size: "sm", c: "dimmed", "data-testid": "destination-empty", children: "No files will be exported for this choice." })) : (_jsx(Stack, { gap: "xs", "data-testid": "destination-preview", children: preview.data.map((d, i) => (_jsxs("div", { children: [_jsxs(Text, { size: "sm", fw: 500, children: [d.location, d.projectName ? ` · ${d.projectName}` : ''] }), _jsx(Text, { size: "xs", c: "dimmed", children: "Notes" }), _jsx(PathText, { path: d.notesPath, size: "xs", placeholder: "Not exported" }), _jsx(Text, { size: "xs", c: "dimmed", children: "Transcript" }), _jsx(PathText, { path: d.transcriptPath, size: "xs", placeholder: "Not exported" })] }, i))) }))] }), mutation.error && _jsx(ProblemAlert, { error: mutation.error }), kind === 'Excluded' && (_jsx(Alert, { color: "yellow", variant: "light", children: "Excluded meetings are skipped by future syncs until you change the assignment." })), _jsxs(Group, { justify: "flex-end", children: [_jsx(Button, { variant: "default", onClick: onClose, children: "Cancel" }), _jsx(Button, { onClick: confirm, disabled: !canConfirm, loading: mutation.isPending, children: "Confirm assignment" })] })] }) }));
}
