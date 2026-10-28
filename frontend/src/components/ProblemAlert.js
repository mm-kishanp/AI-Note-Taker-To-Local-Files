import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { Alert, Button, Code, List, Stack, Text } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
import { ApiError } from '../services/api';
/** Renders an API problem (RFC 7807) or any error in an accessible alert. */
export function ProblemAlert({ error, title, onRetry, hideFieldErrors }) {
    if (!error)
        return null;
    const apiErr = error instanceof ApiError ? error : null;
    const heading = title ?? apiErr?.title ?? 'Something went wrong';
    const detail = apiErr ? apiErr.detail : error instanceof Error ? error.message : String(error);
    const fieldErrors = apiErr && !hideFieldErrors ? apiErr.errors : [];
    return (_jsx(Alert, { color: "red", variant: "light", title: heading, icon: _jsx(IconAlertTriangle, { size: 18 }), role: "alert", children: _jsxs(Stack, { gap: 6, children: [detail && detail !== heading && _jsx(Text, { size: "sm", children: detail }), fieldErrors.length > 0 && (_jsx(List, { size: "sm", spacing: 2, children: fieldErrors.map((e, i) => (_jsxs(List.Item, { children: [e.field && _jsx(Code, { children: e.field }), " ", e.message] }, i))) })), (apiErr?.category || apiErr?.correlationId) && (_jsxs(Text, { size: "xs", c: "dimmed", children: [apiErr?.status ? `HTTP ${apiErr.status}` : null, apiErr?.category ? ` · ${apiErr.category}` : null, apiErr?.correlationId ? ` · correlation id ${apiErr.correlationId}` : null] })), onRetry && (_jsx("div", { children: _jsx(Button, { size: "xs", variant: "light", color: "red", onClick: onRetry, children: "Try again" }) }))] }) }));
}
