import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../services/api';
import { qk } from './queryKeys';
import { useInvalidateMeetingData } from './useInvalidate';
export function useMeetings(q) {
    return useQuery({
        queryKey: qk.meetingList(q),
        queryFn: ({ signal }) => api.meetings.list(q, signal),
        placeholderData: keepPreviousData,
    });
}
export function useMeeting(id) {
    return useQuery({ queryKey: qk.meeting(id), queryFn: () => api.meetings.get(id), enabled: !!id });
}
export function useTranscript(id, page, pageSize, enabled = true) {
    return useQuery({
        queryKey: qk.transcript(id, page, pageSize),
        queryFn: () => api.meetings.transcript(id, page, pageSize),
        enabled: enabled && !!id,
        placeholderData: keepPreviousData,
    });
}
export function useDestinationPreview(id, kind, projectId, enabled) {
    return useQuery({
        queryKey: qk.destinationPreview(id, kind, projectId),
        queryFn: () => api.meetings.destinationPreview(id, kind, kind === 'Project' ? projectId : null),
        enabled: enabled && !!id && (kind !== 'Project' || !!projectId),
        staleTime: 0,
    });
}
export function useConflict(id, recordId) {
    return useQuery({
        queryKey: qk.conflict(id, recordId ?? ''),
        queryFn: () => api.meetings.conflict(id, recordId),
        enabled: !!recordId,
        staleTime: 0,
    });
}
/** Shared onSuccess: write the returned detail into the cache and invalidate list views. */
function useDetailMutation(fn) {
    const qc = useQueryClient();
    const invalidate = useInvalidateMeetingData();
    return useMutation({
        mutationFn: fn,
        onSuccess: (detail) => {
            if (detail?.meeting?.id)
                qc.setQueryData(qk.meeting(detail.meeting.id), detail);
            invalidate();
        },
    });
}
export function useAssignMeeting() {
    return useDetailMutation(({ id, req }) => api.meetings.assign(id, req));
}
export function useReclassify() {
    return useDetailMutation((id) => api.meetings.reclassify(id));
}
export function useReExport() {
    return useDetailMutation(({ id, req }) => api.meetings.reExport(id, req));
}
export function useResolveConflict() {
    return useDetailMutation(({ id, recordId, resolution }) => api.meetings.resolveConflict(id, recordId, resolution));
}
export function useRetryMeeting() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (id) => api.meetings.retry(id),
        onSettled: () => void qc.invalidateQueries({ queryKey: qk.syncStatus }),
    });
}
export function useOpenExport() {
    return useMutation({
        mutationFn: ({ id, recordId, folder }) => api.meetings.openExport(id, recordId, folder),
    });
}
