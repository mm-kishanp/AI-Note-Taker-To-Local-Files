import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { ActionIcon, CopyButton, Group, Text, Tooltip } from '@mantine/core';
import { IconCheck, IconCopy } from '@tabler/icons-react';
/** Monospace, wrap-anywhere path with a copy-to-clipboard button. */
export function PathText({ path, noCopy, placeholder = '—', size = 'sm', ...rest }) {
    if (!path) {
        return (_jsx(Text, { size: size, c: "dimmed", ...rest, children: placeholder }));
    }
    return (_jsxs(Group, { gap: 4, wrap: "nowrap", align: "flex-start", style: { minWidth: 0 }, children: [_jsx(Text, { ff: "monospace", size: size, style: { wordBreak: 'break-all', minWidth: 0 }, title: path, ...rest, children: path }), !noCopy && (_jsx(CopyButton, { value: path, timeout: 1500, children: ({ copied, copy }) => (_jsx(Tooltip, { label: copied ? 'Copied' : 'Copy path', withArrow: true, children: _jsx(ActionIcon, { variant: "subtle", color: copied ? 'teal' : 'gray', size: "sm", onClick: copy, "aria-label": copied ? 'Path copied' : `Copy path ${path}`, children: copied ? _jsx(IconCheck, { size: 14 }) : _jsx(IconCopy, { size: 14 }) }) })) }))] }));
}
