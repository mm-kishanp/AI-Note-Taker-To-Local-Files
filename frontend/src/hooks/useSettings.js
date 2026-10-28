import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../services/api';
import { qk } from './queryKeys';
export function useSettings() {
    return useQuery({ queryKey: qk.settings, queryFn: api.settings.get });
}
export function useUpdateSettings() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (req) => api.settings.update(req),
        onSuccess: (s) => {
            qc.setQueryData(qk.settings, s);
            void qc.invalidateQueries({ queryKey: qk.dashboard });
            void qc.invalidateQueries({ queryKey: qk.projects });
            void qc.invalidateQueries({ queryKey: qk.exports });
        },
    });
}
export function useSourceCredential(source) {
    return useQuery({ queryKey: qk.credential(source), queryFn: () => api.settings.credential(source) });
}
export function useSetSourceCredential(source) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (req) => api.settings.setCredential(source, req),
        onSuccess: (s) => {
            qc.setQueryData(qk.credential(source), s);
            void qc.invalidateQueries({ queryKey: qk.health });
            void qc.invalidateQueries({ queryKey: qk.dashboard });
        },
    });
}
export function useDeleteSourceCredential(source) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: () => api.settings.deleteCredential(source),
        onSuccess: (s) => {
            qc.setQueryData(qk.credential(source), s);
            void qc.invalidateQueries({ queryKey: qk.health });
            void qc.invalidateQueries({ queryKey: qk.dashboard });
        },
    });
}
export function useTestConnection(source) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: () => api.sources.testConnection(source),
        onSettled: () => void qc.invalidateQueries({ queryKey: qk.credential(source) }),
    });
}
export function useAiCredential() {
    return useQuery({ queryKey: qk.aiCredential, queryFn: api.settings.aiCredential });
}
export function useSetAiCredential() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (req) => api.settings.setAiCredential(req),
        onSuccess: (s) => qc.setQueryData(qk.aiCredential, s),
    });
}
export function useDeleteAiCredential() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: () => api.settings.deleteAiCredential(),
        onSuccess: (s) => qc.setQueryData(qk.aiCredential, s),
    });
}
export function useMigrateRoot() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (req) => api.exports.migrateRoot(req),
        onSuccess: () => {
            void qc.invalidateQueries({ queryKey: qk.settings });
            void qc.invalidateQueries({ queryKey: qk.exports });
            void qc.invalidateQueries({ queryKey: qk.meetings });
            void qc.invalidateQueries({ queryKey: qk.projects });
        },
    });
}
export function useGranolaFolders(enabled) {
    return useQuery({ queryKey: qk.granolaFolders, queryFn: api.granola.folders, enabled, staleTime: 5 * 60_000, retry: false });
}
