import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { lazy, Suspense } from 'react';
import { Route, Routes } from 'react-router-dom';
import { LoadingRows } from './components/QueryState';
import { AppLayout } from './layouts/AppLayout';
import { DashboardPage } from './pages/DashboardPage';
import { NotFoundPage } from './pages/NotFoundPage';
const MeetingsPage = lazy(() => import('./pages/MeetingsPage').then((m) => ({ default: m.MeetingsPage })));
const MeetingDetailPage = lazy(() => import('./pages/MeetingDetailPage').then((m) => ({ default: m.MeetingDetailPage })));
const ProjectsPage = lazy(() => import('./pages/ProjectsPage').then((m) => ({ default: m.ProjectsPage })));
const ProjectDetailPage = lazy(() => import('./pages/ProjectDetailPage').then((m) => ({ default: m.ProjectDetailPage })));
const UnassignedPage = lazy(() => import('./pages/UnassignedPage').then((m) => ({ default: m.UnassignedPage })));
const SyncHistoryPage = lazy(() => import('./pages/SyncHistoryPage').then((m) => ({ default: m.SyncHistoryPage })));
const ExportBrowserPage = lazy(() => import('./pages/ExportBrowserPage').then((m) => ({ default: m.ExportBrowserPage })));
const SettingsPage = lazy(() => import('./pages/SettingsPage').then((m) => ({ default: m.SettingsPage })));
export function AppRoutes() {
    return (_jsx(Suspense, { fallback: _jsx(LoadingRows, { rows: 8 }), children: _jsx(Routes, { children: _jsxs(Route, { element: _jsx(AppLayout, {}), children: [_jsx(Route, { index: true, element: _jsx(DashboardPage, {}) }), _jsx(Route, { path: "meetings", element: _jsx(MeetingsPage, {}) }), _jsx(Route, { path: "meetings/:id", element: _jsx(MeetingDetailPage, {}) }), _jsx(Route, { path: "projects", element: _jsx(ProjectsPage, {}) }), _jsx(Route, { path: "projects/:id", element: _jsx(ProjectDetailPage, {}) }), _jsx(Route, { path: "clients", element: _jsx(ProjectsPage, { initialTab: "clients" }) }), _jsx(Route, { path: "unassigned", element: _jsx(UnassignedPage, {}) }), _jsx(Route, { path: "sync-history", element: _jsx(SyncHistoryPage, {}) }), _jsx(Route, { path: "exports", element: _jsx(ExportBrowserPage, {}) }), _jsx(Route, { path: "settings", element: _jsx(SettingsPage, {}) }), _jsx(Route, { path: "*", element: _jsx(NotFoundPage, {}) })] }) }) }));
}
