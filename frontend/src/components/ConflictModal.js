import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { Alert, Button, Group, Modal, Paper, ScrollArea, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { modals } from '@mantine/modals';
import { useConflict, useResolveConflict } from '../hooks/useMeetings';
import { notifySuccess } from '../utils/notify';
import { PathText } from './PathText';
import { ProblemAlert } from './ProblemAlert';
import { LoadingRows } from './QueryState';
const RESOLUTION_TEXT = {
    KeepLocal: {
        label: 'Keep local',
        confirm: 'Keep your edited file. Future syncs will not overwrite it until you choose otherwise.',
        color: 'blue',
    },
    ReplaceWithSource: {
        label: 'Replace with source',
        confirm: 'Overwrite the local file with the latest content from Granola. Your local edits in this file will be lost.',
        color: 'red',
    },
    SaveConflictCopy: {
        label: 'Save conflict copy',
        confirm: 'Write the Granola version as the main file and keep your edited version as a separate “.conflict” copy next to it.',
        color: 'green',
    },
};
/** Side-by-side review of a locally edited export vs. the regenerated source content. */
export function ConflictModal({ meetingId, recordId, onClose }) {
    const conflict = useConflict(meetingId, recordId);
    const resolve = useResolveConflict();
    const choose = (resolution) => {
        const t = RESOLUTION_TEXT[resolution];
        modals.openConfirmModal({
            title: t.label,
            children: _jsx(Text, { size: "sm", children: t.confirm }),
            labels: { confirm: t.label, cancel: 'Back' },
            confirmProps: { color: t.color },
            onConfirm: () => resolve.mutate({ id: meetingId, recordId: recordId, resolution }, {
                onSuccess: () => {
                    notifySuccess('Conflict resolved.');
                    onClose();
                },
            }),
        });
    };
    return (_jsx(Modal, { opened: !!recordId, onClose: onClose, title: "Review conflict", size: "90%", children: conflict.isLoading ? (_jsx(LoadingRows, { rows: 8 })) : conflict.error ? (_jsx(ProblemAlert, { error: conflict.error })) : conflict.data ? (_jsxs(Stack, { gap: "sm", children: [_jsx(Alert, { color: "yellow", variant: "light", children: "This file was edited outside the app after it was exported. Choose which version to keep." }), _jsx(PathText, { path: conflict.data.relativePath }), _jsxs(SimpleGrid, { cols: { base: 1, md: 2 }, spacing: "sm", children: [_jsxs(Paper, { withBorder: true, p: "xs", children: [_jsx(Title, { order: 5, mb: 4, children: "Local file (your edits)" }), _jsx(ScrollArea, { h: 420, type: "auto", children: conflict.data.localContent === null ? (_jsx(Text, { c: "dimmed", size: "sm", children: "The local file no longer exists." })) : (_jsx("pre", { className: "gpi-raw", "aria-label": "Local content", children: conflict.data.localContent })) })] }), _jsxs(Paper, { withBorder: true, p: "xs", children: [_jsx(Title, { order: 5, mb: 4, children: "Source (regenerated from Granola)" }), _jsx(ScrollArea, { h: 420, type: "auto", children: _jsx("pre", { className: "gpi-raw", "aria-label": "Source content", children: conflict.data.sourceContent }) })] })] }), resolve.error && _jsx(ProblemAlert, { error: resolve.error }), _jsxs(Group, { justify: "flex-end", children: [_jsx(Button, { variant: "default", onClick: onClose, children: "Close" }), Object.keys(RESOLUTION_TEXT).map((r) => (_jsx(Button, { color: RESOLUTION_TEXT[r].color, variant: r === 'ReplaceWithSource' ? 'outline' : 'filled', onClick: () => choose(r), loading: resolve.isPending, children: RESOLUTION_TEXT[r].label }, r)))] })] })) : null }));
}
