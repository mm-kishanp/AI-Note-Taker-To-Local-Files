import { useMutation, useQuery } from '@tanstack/react-query';
import { api } from '../services/api';
import { qk } from './queryKeys';
export function useExportTree(root) {
    return useQuery({ queryKey: qk.exportTree(root), queryFn: () => api.exports.tree(root) });
}
export function useExportFile(root, path) {
    return useQuery({
        queryKey: qk.exportFile(root, path ?? ''),
        queryFn: () => api.exports.file(root, path),
        enabled: !!path,
        staleTime: 0,
    });
}
export function useOpenExportPath() {
    return useMutation({
        mutationFn: ({ root, path }) => api.exports.open(root, path),
    });
}
export function useDirectories(path, enabled = true) {
    return useQuery({ queryKey: qk.directories(path), queryFn: () => api.exports.directories(path), enabled, staleTime: 0 });
}
export function useValidatePath() {
    return useMutation({ mutationFn: (req) => api.exports.validatePath(req) });
}
