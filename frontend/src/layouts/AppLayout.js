import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { ActionIcon, Alert, AppShell, Badge, Burger, Button, Group, NavLink, ScrollArea, Text, Tooltip, useComputedColorScheme, useMantineColorScheme, } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { IconAlertTriangle, IconFolders, IconHistory, IconInbox, IconLayoutDashboard, IconMoon, IconNotes, IconSettings, IconSun, IconTags, } from '@tabler/icons-react';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { Link, Outlet, useLocation } from 'react-router-dom';
import { ApiTokenModal } from '../components/ApiTokenModal';
import { SyncNowButton, SyncStatusChip } from '../components/SyncControls';
import { qk } from '../hooks/queryKeys';
import { useDashboard, useHealth, useSyncStatus } from '../hooks/useSystem';
const NAV = [
    { to: '/', label: 'Dashboard', icon: IconLayoutDashboard, exact: true },
    { to: '/meetings', label: 'Meetings', icon: IconNotes },
    { to: '/projects', label: 'Projects & clients', icon: IconTags },
    { to: '/unassigned', label: 'Unassigned', icon: IconInbox, badge: 'unassigned' },
    { to: '/sync-history', label: 'Sync history', icon: IconHistory },
    { to: '/exports', label: 'Export browser', icon: IconFolders },
    { to: '/settings', label: 'Settings', icon: IconSettings },
];
/** Refreshes data views when a background run finishes. */
function SyncWatcher() {
    const qc = useQueryClient();
    const { data } = useSyncStatus();
    const wasRunning = useRef(null);
    useEffect(() => {
        if (!data)
            return;
        if (wasRunning.current && !data.isRunning) {
            for (const key of [qk.meetings, qk.unassigned, qk.dashboard, qk.projects, qk.syncHistory, qk.exports, qk.health]) {
                void qc.invalidateQueries({ queryKey: key });
            }
        }
        wasRunning.current = data.isRunning;
    }, [data, qc]);
    return null;
}
function ColorSchemeToggle() {
    const { setColorScheme } = useMantineColorScheme();
    const computed = useComputedColorScheme('light', { getInitialValueInEffect: true });
    const next = computed === 'dark' ? 'light' : 'dark';
    return (_jsx(Tooltip, { label: `Switch to ${next} mode`, children: _jsx(ActionIcon, { variant: "default", size: "lg", onClick: () => setColorScheme(next), "aria-label": `Switch to ${next} color scheme`, children: computed === 'dark' ? _jsx(IconSun, { size: 18 }) : _jsx(IconMoon, { size: 18 }) }) }));
}
export function AppLayout() {
    const [opened, { toggle, close }] = useDisclosure();
    const location = useLocation();
    const health = useHealth();
    const dashboard = useDashboard();
    const unassigned = dashboard.data?.unassignedMeetings ?? 0;
    useEffect(() => close(), [location.pathname, close]);
    const isActive = (to, exact) => (exact ? location.pathname === to : location.pathname === to || location.pathname.startsWith(to + '/'));
    return (_jsxs(AppShell, { header: { height: 60 }, navbar: { width: 240, breakpoint: 'sm', collapsed: { mobile: !opened } }, padding: "md", children: [_jsx("a", { href: "#main-content", className: "gpi-skip-link", children: "Skip to content" }), _jsx(SyncWatcher, {}), _jsx(ApiTokenModal, {}), _jsx(AppShell.Header, { children: _jsxs(Group, { h: "100%", px: "md", justify: "space-between", wrap: "nowrap", children: [_jsxs(Group, { gap: "sm", wrap: "nowrap", children: [_jsx(Burger, { opened: opened, onClick: toggle, hiddenFrom: "sm", size: "sm", "aria-label": "Toggle navigation" }), _jsx(Text, { fw: 700, size: "lg", component: Link, to: "/", style: { textDecoration: 'none', color: 'inherit', whiteSpace: 'nowrap' }, children: "Granola PI Sync" })] }), _jsxs(Group, { gap: "sm", wrap: "nowrap", children: [_jsx(SyncStatusChip, {}), _jsx(SyncNowButton, {}), _jsx(ColorSchemeToggle, {})] })] }) }), _jsxs(AppShell.Navbar, { p: "xs", children: [_jsx(AppShell.Section, { grow: true, component: ScrollArea, children: _jsx("nav", { "aria-label": "Main navigation", children: NAV.map((item) => (_jsx(NavLink, { component: Link, to: item.to, label: item.label, leftSection: _jsx(item.icon, { size: 18, "aria-hidden": true }), active: isActive(item.to, item.exact), "aria-current": isActive(item.to, item.exact) ? 'page' : undefined, rightSection: item.badge === 'unassigned' && unassigned > 0 ? (_jsx(Badge, { size: "sm", color: "yellow", variant: "filled", "aria-label": `${unassigned} unassigned meetings`, children: unassigned })) : undefined }, item.to))) }) }), _jsx(AppShell.Section, { children: _jsx(Text, { size: "xs", c: "dimmed", px: "sm", py: 4, children: health.data ? `Backend v${health.data.version} · ${health.data.status}` : health.error ? 'Backend unreachable' : ' ' }) })] }), _jsxs(AppShell.Main, { id: "main-content", tabIndex: -1, children: [health.data && !health.data.credentialsConfigured && (_jsx(Alert, { color: "orange", icon: _jsx(IconAlertTriangle, { size: 18 }), title: "Granola API key not configured", mb: "md", role: "alert", children: _jsxs(Group, { justify: "space-between", wrap: "wrap", gap: "xs", children: [_jsx(Text, { size: "sm", children: "Synchronization cannot fetch meetings until you add your Granola API key." }), _jsx(Button, { component: Link, to: "/settings#granola", size: "xs", variant: "light", color: "orange", children: "Open Settings" })] }) })), health.error && (_jsx(Alert, { color: "red", icon: _jsx(IconAlertTriangle, { size: 18 }), title: "Cannot reach the backend", mb: "md", role: "alert", children: "The Granola Project Intelligence service is not responding. Data shown may be out of date." })), _jsx(Outlet, {})] })] }));
}
