import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { Alert, Button, Checkbox, Group, Modal, Stack, Text } from '@mantine/core';
import { modals } from '@mantine/modals';
import { useEffect, useState } from 'react';
import { useReExport } from '../hooks/useMeetings';
import { notifySuccess } from '../utils/notify';
import { ProblemAlert } from './ProblemAlert';
/** Regenerate files from stored data. "Force" overwrites local edits and needs an explicit second confirmation. */
export function ReExportModal({ meetingId, opened, onClose }) {
    const [notes, setNotes] = useState(true);
    const [transcript, setTranscript] = useState(true);
    const [force, setForce] = useState(false);
    const reExport = useReExport();
    useEffect(() => {
        if (opened) {
            setNotes(true);
            setTranscript(true);
            setForce(false);
            reExport.reset();
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [opened]);
    const run = () => reExport.mutate({ id: meetingId, req: { notes, transcript, force } }, {
        onSuccess: () => {
            notifySuccess('Files regenerated.');
            onClose();
        },
    });
    const submit = () => {
        if (!force)
            return run();
        modals.openConfirmModal({
            title: 'Overwrite local edits?',
            children: (_jsxs(Text, { size: "sm", children: ["Any changes you made to these exported Markdown files will be ", _jsx("b", { children: "permanently overwritten" }), " with content regenerated from Granola. This cannot be undone."] })),
            labels: { confirm: 'Yes, overwrite my edits', cancel: 'Cancel' },
            confirmProps: { color: 'red' },
            onConfirm: run,
        });
    };
    return (_jsx(Modal, { opened: opened, onClose: onClose, title: "Re-export meeting", children: _jsxs(Stack, { gap: "sm", children: [_jsx(Text, { size: "sm", children: "Regenerate Markdown files from the data stored locally (no Granola request)." }), _jsx(Checkbox, { label: "Notes file", checked: notes, onChange: (e) => setNotes(e.currentTarget.checked) }), _jsx(Checkbox, { label: "Transcript file", checked: transcript, onChange: (e) => setTranscript(e.currentTarget.checked) }), _jsx(Checkbox, { label: "Force overwrite local edits", description: "By default, files you edited are preserved (conflict policy). Tick to overwrite them.", color: "red", checked: force, onChange: (e) => setForce(e.currentTarget.checked) }), force && (_jsx(Alert, { color: "red", variant: "light", children: "Local edits will be lost. You will be asked to confirm." })), reExport.error && _jsx(ProblemAlert, { error: reExport.error }), _jsxs(Group, { justify: "flex-end", children: [_jsx(Button, { variant: "default", onClick: onClose, children: "Cancel" }), _jsx(Button, { onClick: submit, loading: reExport.isPending, disabled: !notes && !transcript, color: force ? 'red' : undefined, children: "Re-export" })] })] }) }));
}
