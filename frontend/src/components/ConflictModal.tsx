import { Alert, Button, Group, Modal, Paper, ScrollArea, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { modals } from '@mantine/modals';
import { useConflict, useResolveConflict } from '../hooks/useMeetings';
import type { ConflictResolution } from '../types/api';
import { notifySuccess } from '../utils/notify';
import { PathText } from './PathText';
import { ProblemAlert } from './ProblemAlert';
import { LoadingRows } from './QueryState';

export interface ConflictModalProps {
  meetingId: string;
  recordId: string | null;
  onClose: () => void;
}

const RESOLUTION_TEXT: Record<ConflictResolution, { label: string; confirm: string; color: string }> = {
  KeepLocal: {
    label: 'Keep local',
    confirm: 'Keep your edited file. Future syncs will not overwrite it until you choose otherwise.',
    color: 'blue',
  },
  ReplaceWithSource: {
    label: 'Replace with source',
    confirm: 'Overwrite the local file with the latest content from Granola. Your local edits in this file will be lost.',
    color: 'red',
  },
  SaveConflictCopy: {
    label: 'Save conflict copy',
    confirm: 'Write the Granola version as the main file and keep your edited version as a separate “.conflict” copy next to it.',
    color: 'green',
  },
};

/** Side-by-side review of a locally edited export vs. the regenerated source content. */
export function ConflictModal({ meetingId, recordId, onClose }: ConflictModalProps) {
  const conflict = useConflict(meetingId, recordId);
  const resolve = useResolveConflict();

  const choose = (resolution: ConflictResolution) => {
    const t = RESOLUTION_TEXT[resolution];
    modals.openConfirmModal({
      title: t.label,
      children: <Text size="sm">{t.confirm}</Text>,
      labels: { confirm: t.label, cancel: 'Back' },
      confirmProps: { color: t.color },
      onConfirm: () =>
        resolve.mutate(
          { id: meetingId, recordId: recordId!, resolution },
          {
            onSuccess: () => {
              notifySuccess('Conflict resolved.');
              onClose();
            },
          },
        ),
    });
  };

  return (
    <Modal opened={!!recordId} onClose={onClose} title="Review conflict" size="90%">
      {conflict.isLoading ? (
        <LoadingRows rows={8} />
      ) : conflict.error ? (
        <ProblemAlert error={conflict.error} />
      ) : conflict.data ? (
        <Stack gap="sm">
          <Alert color="yellow" variant="light">
            This file was edited outside the app after it was exported. Choose which version to keep.
          </Alert>
          <PathText path={conflict.data.relativePath} />
          <SimpleGrid cols={{ base: 1, md: 2 }} spacing="sm">
            <Paper withBorder p="xs">
              <Title order={5} mb={4}>
                Local file (your edits)
              </Title>
              <ScrollArea h={420} type="auto">
                {conflict.data.localContent === null ? (
                  <Text c="dimmed" size="sm">
                    The local file no longer exists.
                  </Text>
                ) : (
                  <pre className="gpi-raw" aria-label="Local content">
                    {conflict.data.localContent}
                  </pre>
                )}
              </ScrollArea>
            </Paper>
            <Paper withBorder p="xs">
              <Title order={5} mb={4}>
                Source (regenerated from Granola)
              </Title>
              <ScrollArea h={420} type="auto">
                <pre className="gpi-raw" aria-label="Source content">
                  {conflict.data.sourceContent}
                </pre>
              </ScrollArea>
            </Paper>
          </SimpleGrid>
          {resolve.error && <ProblemAlert error={resolve.error} />}
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              Close
            </Button>
            {(Object.keys(RESOLUTION_TEXT) as ConflictResolution[]).map((r) => (
              <Button key={r} color={RESOLUTION_TEXT[r].color} variant={r === 'ReplaceWithSource' ? 'outline' : 'filled'} onClick={() => choose(r)} loading={resolve.isPending}>
                {RESOLUTION_TEXT[r].label}
              </Button>
            ))}
          </Group>
        </Stack>
      ) : null}
    </Modal>
  );
}
