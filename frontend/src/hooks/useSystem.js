import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../services/api';
import { qk } from './queryKeys';
export function useHealth() {
    return useQuery({ queryKey: qk.health, queryFn: api.health, refetchInterval: 30_000 });
}
export function useDashboard() {
    return useQuery({ queryKey: qk.dashboard, queryFn: api.dashboard, refetchInterval: 60_000 });
}
/** Live sync progress: polls every 1.5 s while running, every 10 s while idle. */
export function useSyncStatus() {
    return useQuery({
        queryKey: qk.syncStatus,
        queryFn: api.sync.status,
        refetchInterval: (query) => (query.state.data?.isRunning ? 1500 : 10_000),
        refetchIntervalInBackground: false,
    });
}
export function useSyncHistory(page, pageSize) {
    return useQuery({
        queryKey: qk.syncHistoryPage(page, pageSize),
        queryFn: () => api.sync.history(page, pageSize),
        placeholderData: keepPreviousData,
    });
}
export function useSyncRun(id) {
    return useQuery({ queryKey: qk.syncRun(id ?? ''), queryFn: () => api.sync.run(id), enabled: !!id });
}
export function useStartSync() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (req) => api.sync.start(req),
        onSettled: () => {
            void qc.invalidateQueries({ queryKey: qk.syncStatus });
            void qc.invalidateQueries({ queryKey: qk.syncHistory });
        },
    });
}
export function useCancelSync() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: () => api.sync.cancel(),
        onSettled: () => void qc.invalidateQueries({ queryKey: qk.syncStatus }),
    });
}
