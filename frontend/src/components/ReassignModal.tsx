import { Alert, Button, Group, Modal, Paper, SegmentedControl, Stack, Text, Textarea } from '@mantine/core';
import { IconArrowRight } from '@tabler/icons-react';
import { useEffect, useState } from 'react';
import { useAssignMeeting, useDestinationPreview } from '../hooks/useMeetings';
import { useAssignUnassigned } from '../hooks/useUnassigned';
import type { ManualAssignmentKind, MeetingDetailDto } from '../types/api';
import { notifySuccess } from '../utils/notify';
import { PathText } from './PathText';
import { ProblemAlert } from './ProblemAlert';
import { ProjectSelect } from './ProjectSelect';
import { LoadingRows } from './QueryState';

export interface ReassignModalProps {
  meetingId: string;
  meetingTitle?: string;
  opened: boolean;
  onClose: () => void;
  initialKind?: ManualAssignmentKind;
  initialProjectId?: string | null;
  /** Use POST /api/unassigned/{id}/assign (review queue) instead of /api/meetings/{id}/assign-project. */
  fromReviewQueue?: boolean;
  onAssigned?: (detail: MeetingDetailDto) => void;
}

const KIND_OPTIONS: { value: ManualAssignmentKind; label: string }[] = [
  { value: 'Project', label: 'Project' },
  { value: 'Shared', label: 'Shared' },
  { value: 'Excluded', label: 'Excluded' },
  { value: 'None', label: 'Automatic' },
];

const KIND_HELP: Record<ManualAssignmentKind, string> = {
  Project: 'Pin this meeting to a project. Rules will no longer move it.',
  Shared: 'File under the Shared folders (cross-project meetings).',
  Excluded: 'Do not export this meeting. Existing generated files are left on disk.',
  None: 'Remove the manual assignment and let the classification rules decide again.',
};

/** Reassign a meeting. The destination preview is fetched and shown before the assignment can be confirmed. */
export function ReassignModal({
  meetingId, meetingTitle, opened, onClose, initialKind = 'Project', initialProjectId = null, fromReviewQueue, onAssigned,
}: ReassignModalProps) {
  const [kind, setKind] = useState<ManualAssignmentKind>(initialKind);
  const [projectId, setProjectId] = useState<string | null>(initialProjectId);
  const [note, setNote] = useState('');

  useEffect(() => {
    if (opened) {
      setKind(initialKind);
      setProjectId(initialProjectId);
      setNote('');
    }
  }, [opened, initialKind, initialProjectId]);

  const ready = kind !== 'Project' || !!projectId;
  const preview = useDestinationPreview(meetingId, kind, projectId, opened && ready);
  const assignMeeting = useAssignMeeting();
  const assignReview = useAssignUnassigned();
  const mutation = fromReviewQueue ? assignReview : assignMeeting;

  const confirm = () => {
    mutation.mutate(
      { id: meetingId, req: { kind, projectId: kind === 'Project' ? projectId : null, note: note.trim() || null } },
      {
        onSuccess: (detail) => {
          notifySuccess(
            kind === 'None' ? 'Manual assignment cleared.' : `Meeting assigned to ${kind === 'Project' ? detail.meeting.projectName ?? 'project' : kind}.`,
            'Assignment saved',
          );
          onAssigned?.(detail);
          onClose();
        },
      },
    );
  };

  const canConfirm = ready && preview.isSuccess && !preview.isFetching && !mutation.isPending;

  return (
    <Modal opened={opened} onClose={onClose} title={meetingTitle ? `Reassign “${meetingTitle}”` : 'Reassign meeting'} size="lg">
      <Stack gap="md">
        <div>
          <Text size="sm" fw={500} mb={4} id="assign-kind-label">
            Assignment
          </Text>
          <SegmentedControl
            aria-labelledby="assign-kind-label"
            data={KIND_OPTIONS}
            value={kind}
            onChange={(v) => setKind(v as ManualAssignmentKind)}
            fullWidth
          />
          <Text size="xs" c="dimmed" mt={4}>
            {KIND_HELP[kind]}
          </Text>
        </div>
        {kind === 'Project' && <ProjectSelect value={projectId} onChange={setProjectId} required />}
        <Textarea label="Note (optional)" value={note} onChange={(e) => setNote(e.currentTarget.value)} autosize minRows={1} maxRows={4} />

        <Paper withBorder p="sm" radius="sm">
          <Group gap={6} mb={6}>
            <IconArrowRight size={16} aria-hidden />
            <Text size="sm" fw={600}>
              Destination preview
            </Text>
          </Group>
          {!ready ? (
            <Text size="sm" c="dimmed">
              Choose a project to see where the files will be written.
            </Text>
          ) : preview.isLoading || preview.isFetching ? (
            <LoadingRows rows={2} height={18} />
          ) : preview.error ? (
            <ProblemAlert error={preview.error} title="Could not preview destination" />
          ) : (preview.data?.length ?? 0) === 0 ? (
            <Text size="sm" c="dimmed" data-testid="destination-empty">
              No files will be exported for this choice.
            </Text>
          ) : (
            <Stack gap="xs" data-testid="destination-preview">
              {preview.data!.map((d, i) => (
                <div key={i}>
                  <Text size="sm" fw={500}>
                    {d.location}
                    {d.projectName ? ` · ${d.projectName}` : ''}
                  </Text>
                  <Text size="xs" c="dimmed">
                    Notes
                  </Text>
                  <PathText path={d.notesPath} size="xs" placeholder="Not exported" />
                  <Text size="xs" c="dimmed">
                    Transcript
                  </Text>
                  <PathText path={d.transcriptPath} size="xs" placeholder="Not exported" />
                </div>
              ))}
            </Stack>
          )}
        </Paper>

        {mutation.error && <ProblemAlert error={mutation.error} />}
        {kind === 'Excluded' && (
          <Alert color="yellow" variant="light">
            Excluded meetings are skipped by future syncs until you change the assignment.
          </Alert>
        )}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={confirm} disabled={!canConfirm} loading={mutation.isPending}>
            Confirm assignment
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
