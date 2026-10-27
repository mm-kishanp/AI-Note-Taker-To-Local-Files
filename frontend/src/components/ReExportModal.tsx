import { Alert, Button, Checkbox, Group, Modal, Stack, Text } from '@mantine/core';
import { modals } from '@mantine/modals';
import { useEffect, useState } from 'react';
import { useReExport } from '../hooks/useMeetings';
import { notifySuccess } from '../utils/notify';
import { ProblemAlert } from './ProblemAlert';

export interface ReExportModalProps {
  meetingId: string;
  opened: boolean;
  onClose: () => void;
}

/** Regenerate files from stored data. "Force" overwrites local edits and needs an explicit second confirmation. */
export function ReExportModal({ meetingId, opened, onClose }: ReExportModalProps) {
  const [notes, setNotes] = useState(true);
  const [transcript, setTranscript] = useState(true);
  const [force, setForce] = useState(false);
  const reExport = useReExport();

  useEffect(() => {
    if (opened) {
      setNotes(true);
      setTranscript(true);
      setForce(false);
      reExport.reset();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [opened]);

  const run = () =>
    reExport.mutate(
      { id: meetingId, req: { notes, transcript, force } },
      {
        onSuccess: () => {
          notifySuccess('Files regenerated.');
          onClose();
        },
      },
    );

  const submit = () => {
    if (!force) return run();
    modals.openConfirmModal({
      title: 'Overwrite local edits?',
      children: (
        <Text size="sm">
          Any changes you made to these exported Markdown files will be <b>permanently overwritten</b> with content regenerated
          from Granola. This cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Yes, overwrite my edits', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: run,
    });
  };

  return (
    <Modal opened={opened} onClose={onClose} title="Re-export meeting">
      <Stack gap="sm">
        <Text size="sm">Regenerate Markdown files from the data stored locally (no Granola request).</Text>
        <Checkbox label="Notes file" checked={notes} onChange={(e) => setNotes(e.currentTarget.checked)} />
        <Checkbox label="Transcript file" checked={transcript} onChange={(e) => setTranscript(e.currentTarget.checked)} />
        <Checkbox
          label="Force overwrite local edits"
          description="By default, files you edited are preserved (conflict policy). Tick to overwrite them."
          color="red"
          checked={force}
          onChange={(e) => setForce(e.currentTarget.checked)}
        />
        {force && (
          <Alert color="red" variant="light">
            Local edits will be lost. You will be asked to confirm.
          </Alert>
        )}
        {reExport.error && <ProblemAlert error={reExport.error} />}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={submit} loading={reExport.isPending} disabled={!notes && !transcript} color={force ? 'red' : undefined}>
            Re-export
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
