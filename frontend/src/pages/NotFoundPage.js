import { jsx as _jsx, Fragment as _Fragment, jsxs as _jsxs } from "react/jsx-runtime";
import { Button } from '@mantine/core';
import { Link } from 'react-router-dom';
import { EmptyState } from '../components/EmptyState';
import { PageHeader } from '../components/PageHeader';
export function NotFoundPage() {
    return (_jsxs(_Fragment, { children: [_jsx(PageHeader, { title: "Page not found" }), _jsx(EmptyState, { title: "This page does not exist", description: "The link may be outdated.", action: _jsx(Button, { component: Link, to: "/", children: "Go to dashboard" }) })] }));
}
