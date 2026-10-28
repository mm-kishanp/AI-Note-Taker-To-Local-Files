import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../services/api';
import { qk } from './queryKeys';
import { useInvalidateMeetingData } from './useInvalidate';
export function useProjects(search = '', includeArchived = false) {
    return useQuery({
        queryKey: qk.projectList(search, includeArchived),
        queryFn: () => api.projects.list(search || undefined, includeArchived),
    });
}
export function useProject(id) {
    return useQuery({ queryKey: qk.project(id), queryFn: () => api.projects.get(id), enabled: !!id });
}
export function useSaveProject() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: ({ id, input }) => id ? api.projects.update(id, input) : api.projects.create(input),
        onSuccess: (p) => {
            qc.setQueryData(qk.project(p.id), p);
            void qc.invalidateQueries({ queryKey: qk.projects });
            void qc.invalidateQueries({ queryKey: qk.clients });
            void qc.invalidateQueries({ queryKey: qk.dashboard });
        },
    });
}
export function useSetProjectArchived() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: ({ id, archived }) => archived ? api.projects.archive(id) : api.projects.restore(id),
        onSuccess: (p) => {
            qc.setQueryData(qk.project(p.id), p);
            void qc.invalidateQueries({ queryKey: qk.projects });
            void qc.invalidateQueries({ queryKey: qk.dashboard });
        },
    });
}
export function useTestMapping() {
    return useMutation({
        mutationFn: ({ id, req }) => api.projects.testMapping(id, req),
    });
}
export function useApplyMapping() {
    const qc = useQueryClient();
    const invalidate = useInvalidateMeetingData();
    return useMutation({
        mutationFn: ({ id, req }) => api.projects.applyMapping(id, req),
        onSettled: () => {
            void qc.invalidateQueries({ queryKey: qk.syncStatus });
            invalidate();
        },
    });
}
export function useImportConfig() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (cfg) => api.projects.importConfig(cfg),
        onSuccess: () => {
            void qc.invalidateQueries({ queryKey: qk.projects });
            void qc.invalidateQueries({ queryKey: qk.clients });
        },
    });
}
export function useClients() {
    return useQuery({ queryKey: qk.clients, queryFn: api.clients.list });
}
export function useSaveClient() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: ({ id, input }) => id ? api.clients.update(id, input) : api.clients.create(input),
        onSuccess: () => {
            void qc.invalidateQueries({ queryKey: qk.clients });
            void qc.invalidateQueries({ queryKey: qk.projects });
        },
    });
}
export function useDeleteClient() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (id) => api.clients.remove(id),
        onSuccess: () => {
            void qc.invalidateQueries({ queryKey: qk.clients });
            void qc.invalidateQueries({ queryKey: qk.projects });
        },
    });
}
