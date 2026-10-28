import { useQueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';
import { qk } from './queryKeys';
/** Invalidates everything that depends on meeting classification / export state. */
export function useInvalidateMeetingData() {
    const qc = useQueryClient();
    return useCallback(() => {
        void qc.invalidateQueries({ queryKey: qk.meetings });
        void qc.invalidateQueries({ queryKey: qk.unassigned });
        void qc.invalidateQueries({ queryKey: qk.dashboard });
        void qc.invalidateQueries({ queryKey: qk.projects });
        void qc.invalidateQueries({ queryKey: qk.exports });
    }, [qc]);
}
