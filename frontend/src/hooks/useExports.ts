import { useMutation, useQuery } from '@tanstack/react-query';
import { api } from '../services/api';
import type { ExportRootKind, ValidatePathRequest } from '../types/api';
import { qk } from './queryKeys';

export function useExportTree(root: ExportRootKind) {
  return useQuery({ queryKey: qk.exportTree(root), queryFn: () => api.exports.tree(root) });
}

export function useExportFile(root: ExportRootKind, path: string | null) {
  return useQuery({
    queryKey: qk.exportFile(root, path ?? ''),
    queryFn: () => api.exports.file(root, path!),
    enabled: !!path,
    staleTime: 0,
  });
}

export function useOpenExportPath() {
  return useMutation({
    mutationFn: ({ root, path }: { root: ExportRootKind; path?: string | null }) => api.exports.open(root, path),
  });
}

export function useDirectories(path: string | null, enabled = true) {
  return useQuery({ queryKey: qk.directories(path), queryFn: () => api.exports.directories(path), enabled, staleTime: 0 });
}

export function useValidatePath() {
  return useMutation({ mutationFn: (req: ValidatePathRequest) => api.exports.validatePath(req) });
}
