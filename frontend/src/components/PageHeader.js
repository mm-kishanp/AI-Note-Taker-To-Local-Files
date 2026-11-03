import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { Group, Stack, Text, Title } from '@mantine/core';
import { useEffect } from 'react';
export function PageHeader({ title, description, actions, docTitle }) {
    useEffect(() => {
        const t = docTitle ?? (typeof title === 'string' ? title : undefined);
        if (t)
            document.title = `${t} · CHRONIQ`;
    }, [docTitle, title]);
    return (_jsxs(Group, { justify: "space-between", align: "flex-start", mb: "lg", wrap: "wrap", gap: "sm", children: [_jsxs(Stack, { gap: 4, style: { minWidth: 0, flex: 1 }, children: [_jsx(Title, { order: 2, children: title }), description && (_jsx(Text, { c: "dimmed", size: "sm", children: description }))] }), actions && (_jsx(Group, { gap: "xs", wrap: "wrap", children: actions }))] }));
}
