import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../services/api';
import type {
  AssignMeetingRequest, ConflictResolution, ManualAssignmentKind, MeetingDetailDto, MeetingQuery, ReExportRequest,
} from '../types/api';
import { qk } from './queryKeys';
import { useInvalidateMeetingData } from './useInvalidate';

export function useMeetings(q: MeetingQuery) {
  return useQuery({
    queryKey: qk.meetingList(q),
    queryFn: ({ signal }) => api.meetings.list(q, signal),
    placeholderData: keepPreviousData,
  });
}

export function useMeeting(id: string) {
  return useQuery({ queryKey: qk.meeting(id), queryFn: () => api.meetings.get(id), enabled: !!id });
}

export function useTranscript(id: string, page: number, pageSize: number, enabled = true) {
  return useQuery({
    queryKey: qk.transcript(id, page, pageSize),
    queryFn: () => api.meetings.transcript(id, page, pageSize),
    enabled: enabled && !!id,
    placeholderData: keepPreviousData,
  });
}

export function useDestinationPreview(id: string, kind: ManualAssignmentKind, projectId: string | null, enabled: boolean) {
  return useQuery({
    queryKey: qk.destinationPreview(id, kind, projectId),
    queryFn: () => api.meetings.destinationPreview(id, kind, kind === 'Project' ? projectId : null),
    enabled: enabled && !!id && (kind !== 'Project' || !!projectId),
    staleTime: 0,
  });
}

export function useConflict(id: string, recordId: string | null) {
  return useQuery({
    queryKey: qk.conflict(id, recordId ?? ''),
    queryFn: () => api.meetings.conflict(id, recordId!),
    enabled: !!recordId,
    staleTime: 0,
  });
}

/** Shared onSuccess: write the returned detail into the cache and invalidate list views. */
function useDetailMutation<TVars>(fn: (vars: TVars) => Promise<MeetingDetailDto>) {
  const qc = useQueryClient();
  const invalidate = useInvalidateMeetingData();
  return useMutation({
    mutationFn: fn,
    onSuccess: (detail) => {
      if (detail?.meeting?.id) qc.setQueryData(qk.meeting(detail.meeting.id), detail);
      invalidate();
    },
  });
}

export function useAssignMeeting() {
  return useDetailMutation(({ id, req }: { id: string; req: AssignMeetingRequest }) => api.meetings.assign(id, req));
}

export function useReclassify() {
  return useDetailMutation((id: string) => api.meetings.reclassify(id));
}

export function useReExport() {
  return useDetailMutation(({ id, req }: { id: string; req: ReExportRequest }) => api.meetings.reExport(id, req));
}

export function useResolveConflict() {
  return useDetailMutation(({ id, recordId, resolution }: { id: string; recordId: string; resolution: ConflictResolution }) =>
    api.meetings.resolveConflict(id, recordId, resolution));
}

export function useRetryMeeting() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.meetings.retry(id),
    onSettled: () => void qc.invalidateQueries({ queryKey: qk.syncStatus }),
  });
}

export function useOpenExport() {
  return useMutation({
    mutationFn: ({ id, recordId, folder }: { id: string; recordId: string; folder: boolean }) =>
      api.meetings.openExport(id, recordId, folder),
  });
}
