import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../services/api';
import { qk } from './queryKeys';
import { useInvalidateMeetingData } from './useInvalidate';
export function useUnassigned(page, pageSize) {
    return useQuery({
        queryKey: qk.unassignedList(page, pageSize),
        queryFn: () => api.unassigned.list(page, pageSize),
        placeholderData: keepPreviousData,
    });
}
export function useAssignUnassigned() {
    const qc = useQueryClient();
    const invalidate = useInvalidateMeetingData();
    return useMutation({
        mutationFn: ({ id, req }) => api.unassigned.assign(id, req),
        onSuccess: (detail) => {
            if (detail?.meeting?.id)
                qc.setQueryData(qk.meeting(detail.meeting.id), detail);
            invalidate();
        },
    });
}
export function useSaveMapping() {
    const invalidate = useInvalidateMeetingData();
    return useMutation({
        mutationFn: ({ id, req }) => api.unassigned.saveMapping(id, req),
        onSuccess: () => invalidate(),
    });
}
