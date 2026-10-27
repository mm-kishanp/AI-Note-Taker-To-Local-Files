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
  return (
    <Suspense fallback={<LoadingRows rows={8} />}>
      <Routes>
        <Route element={<AppLayout />}>
          <Route index element={<DashboardPage />} />
          <Route path="meetings" element={<MeetingsPage />} />
          <Route path="meetings/:id" element={<MeetingDetailPage />} />
          <Route path="projects" element={<ProjectsPage />} />
          <Route path="projects/:id" element={<ProjectDetailPage />} />
          <Route path="clients" element={<ProjectsPage initialTab="clients" />} />
          <Route path="unassigned" element={<UnassignedPage />} />
          <Route path="sync-history" element={<SyncHistoryPage />} />
          <Route path="exports" element={<ExportBrowserPage />} />
          <Route path="settings" element={<SettingsPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </Suspense>
  );
}
