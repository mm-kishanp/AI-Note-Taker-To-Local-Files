import type { ExportRootKind, MeetingQuery } from '../types/api';

/** Central query keys so invalidation stays consistent across hooks. */
export const qk = {
  health: ['health'] as const,
  dashboard: ['dashboard'] as const,
  meetings: ['meetings'] as const,
  meetingList: (q: MeetingQuery) => ['meetings', 'list', q] as const,
  meeting: (id: string) => ['meetings', 'detail', id] as const,
  transcript: (id: string, page: number, pageSize: number) => ['meetings', 'transcript', id, page, pageSize] as const,
  destinationPreview: (id: string, kind: string, projectId: string | null | undefined) =>
    ['meetings', 'destination', id, kind, projectId ?? null] as const,
  conflict: (id: string, recordId: string) => ['meetings', 'conflict', id, recordId] as const,
  unassigned: ['unassigned'] as const,
  unassignedList: (page: number, pageSize: number) => ['unassigned', page, pageSize] as const,
  projects: ['projects'] as const,
  projectList: (search: string, includeArchived: boolean) => ['projects', 'list', search, includeArchived] as const,
  project: (id: string) => ['projects', 'detail', id] as const,
  clients: ['clients'] as const,
  syncStatus: ['sync', 'status'] as const,
  syncHistory: ['sync', 'history'] as const,
  syncHistoryPage: (page: number, pageSize: number) => ['sync', 'history', page, pageSize] as const,
  syncRun: (id: string) => ['sync', 'run', id] as const,
  settings: ['settings'] as const,
  granolaCredential: ['credentials', 'granola'] as const,
  aiCredential: ['credentials', 'ai'] as const,
  granolaFolders: ['granola', 'folders'] as const,
  exports: ['exports'] as const,
  exportTree: (root: ExportRootKind) => ['exports', 'tree', root] as const,
  exportFile: (root: ExportRootKind, path: string) => ['exports', 'file', root, path] as const,
  directories: (path: string | null) => ['fs', 'directories', path] as const,
};
