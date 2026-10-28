import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { Paper, Skeleton, Text, UnstyledButton } from '@mantine/core';
import { Link } from 'react-router-dom';
export function StatCard({ label, value, hint, to, color, loading }) {
    const body = (_jsxs(Paper, { withBorder: true, p: "md", radius: "md", h: "100%", children: [_jsx(Text, { size: "xs", c: "dimmed", tt: "uppercase", fw: 600, children: label }), loading ? (_jsx(Skeleton, { height: 28, mt: 6, width: "50%" })) : (_jsx(Text, { size: "xl", fw: 700, c: color, mt: 4, children: value })), hint && (_jsx(Text, { size: "xs", c: "dimmed", mt: 2, children: hint }))] }));
    if (!to)
        return body;
    return (_jsx(UnstyledButton, { component: Link, to: to, "aria-label": `${label}: view details`, style: { display: 'block', height: '100%' }, children: body }));
}
