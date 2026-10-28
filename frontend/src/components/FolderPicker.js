import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { Alert, Breadcrumbs, Button, Checkbox, Group, List, Modal, NavLink, ScrollArea, Stack, Text, TextInput, } from '@mantine/core';
import { IconArrowUp, IconCheck, IconFolder, IconHome, IconX } from '@tabler/icons-react';
import { useEffect, useState } from 'react';
import { useDirectories, useValidatePath } from '../hooks/useExports';
import { formatBytes } from '../utils/format';
import { ProblemAlert } from './ProblemAlert';
import { LoadingRows } from './QueryState';
/** Folder picker backed by GET /api/exports/directories + POST /api/exports/validate-path. */
export function FolderPicker({ opened, onClose, onSelect, initialPath, title = 'Choose a folder' }) {
    const [current, setCurrent] = useState(initialPath || null);
    const [typed, setTyped] = useState(initialPath ?? '');
    const [createIfMissing, setCreateIfMissing] = useState(false);
    const [result, setResult] = useState(null);
    const listing = useDirectories(current, opened);
    const validate = useValidatePath();
    useEffect(() => {
        if (opened) {
            setCurrent(initialPath || null);
            setTyped(initialPath ?? '');
            setResult(null);
            validate.reset();
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [opened, initialPath]);
    const navigate = (path) => {
        setCurrent(path);
        setTyped(path ?? '');
        setResult(null);
    };
    const runValidate = () => {
        if (!typed.trim())
            return;
        validate.mutate({ path: typed.trim(), createIfMissing }, { onSuccess: setResult });
    };
    const segments = (listing.data?.path ?? '').split(/[\\/]/).filter(Boolean);
    return (_jsx(Modal, { opened: opened, onClose: onClose, title: title, size: "lg", children: _jsxs(Stack, { gap: "sm", children: [_jsx(Group, { gap: "xs", align: "flex-end", wrap: "nowrap", children: _jsx(TextInput, { label: "Folder path", value: typed, onChange: (e) => {
                            setTyped(e.currentTarget.value);
                            setResult(null);
                        }, onKeyDown: (e) => {
                            if (e.key === 'Enter') {
                                e.preventDefault();
                                navigate(typed.trim() || null);
                            }
                        }, style: { flex: 1 }, ff: "monospace", description: "Type a path and press Enter to browse it, or pick a sub-folder below." }) }), _jsxs(Group, { gap: "xs", children: [_jsx(Button, { size: "xs", variant: "default", leftSection: _jsx(IconHome, { size: 14 }), onClick: () => navigate(null), children: "Drives" }), _jsx(Button, { size: "xs", variant: "default", leftSection: _jsx(IconArrowUp, { size: 14 }), disabled: !listing.data?.path, onClick: () => navigate(listing.data?.parent ?? null), children: "Up" }), segments.length > 0 && (_jsx(Breadcrumbs, { separator: "\u203A", style: { flexWrap: 'wrap' }, children: segments.map((s, i) => (_jsx(Text, { size: "xs", ff: "monospace", children: s }, i))) }))] }), _jsx(ScrollArea, { h: 260, type: "auto", style: { border: '1px solid var(--mantine-color-default-border)', borderRadius: 4 }, children: listing.isLoading ? (_jsx(LoadingRows, { rows: 6, height: 22 })) : listing.error ? (_jsx(ProblemAlert, { error: listing.error })) : (listing.data?.directories.length ?? 0) === 0 ? (_jsx(Text, { size: "sm", c: "dimmed", p: "sm", children: "No sub-folders." })) : (_jsx("nav", { "aria-label": "Sub-folders", children: listing.data.directories.map((d) => (_jsx(NavLink, { label: d.name, leftSection: _jsx(IconFolder, { size: 16 }), onClick: () => navigate(d.fullPath), "aria-label": `Open folder ${d.name}` }, d.fullPath))) })) }), _jsx(Checkbox, { label: "Create the folder if it does not exist", checked: createIfMissing, onChange: (e) => setCreateIfMissing(e.currentTarget.checked) }), validate.error && _jsx(ProblemAlert, { error: validate.error }), result && (_jsxs(Alert, { color: result.valid ? 'green' : 'red', icon: result.valid ? _jsx(IconCheck, { size: 16 }) : _jsx(IconX, { size: 16 }), title: result.valid ? 'Folder is usable' : 'Folder cannot be used', role: "status", children: [_jsx(Text, { size: "sm", ff: "monospace", style: { wordBreak: 'break-all' }, children: result.fullPath }), _jsxs(List, { size: "sm", mt: 4, children: [_jsx(List.Item, { children: result.exists ? 'Exists' : 'Does not exist yet' }), _jsx(List.Item, { children: result.writable ? 'Writable' : 'Not writable' }), result.freeBytes !== null && _jsxs(List.Item, { children: ["Free space: ", formatBytes(result.freeBytes)] }), result.messages.map((m, i) => (_jsx(List.Item, { children: m }, i)))] })] })), _jsxs(Group, { justify: "flex-end", children: [_jsx(Button, { variant: "default", onClick: onClose, children: "Cancel" }), _jsx(Button, { variant: "light", onClick: runValidate, loading: validate.isPending, disabled: !typed.trim(), children: "Validate" }), _jsx(Button, { disabled: !result?.valid, onClick: () => {
                                if (result?.valid) {
                                    onSelect(result.fullPath);
                                    onClose();
                                }
                            }, children: "Use this folder" })] })] }) }));
}
