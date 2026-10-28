import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { Paper, Stack, Text, ThemeIcon } from '@mantine/core';
import { IconInbox } from '@tabler/icons-react';
export function EmptyState({ title, description, icon, action }) {
    return (_jsx(Paper, { withBorder: true, p: "xl", radius: "md", role: "status", children: _jsxs(Stack, { align: "center", gap: "xs", children: [_jsx(ThemeIcon, { size: 44, radius: "xl", variant: "light", color: "gray", "aria-hidden": true, children: icon ?? _jsx(IconInbox, { size: 24 }) }), _jsx(Text, { fw: 600, children: title }), description && (_jsx(Text, { c: "dimmed", size: "sm", ta: "center", maw: 520, children: description })), action] }) }));
}
