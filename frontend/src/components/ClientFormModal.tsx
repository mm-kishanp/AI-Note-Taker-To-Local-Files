import { Alert, Button, Group, Modal, Stack, TextInput, Textarea } from '@mantine/core';
import { IconInfoCircle } from '@tabler/icons-react';
import { useEffect, useState } from 'react';
import { useSaveClient } from '../hooks/useProjects';
import { ApiError } from '../services/api';
import type { ClientDto } from '../types/api';
import { notifySuccess } from '../utils/notify';
import { ProblemAlert } from './ProblemAlert';
import { cleanRules, type EditableRule, RuleEditor, toEditableRules } from './RuleEditor';

export interface ClientFormModalProps {
  opened: boolean;
  onClose: () => void;
  client?: ClientDto | null;
}

export function ClientFormModal({ opened, onClose, client }: ClientFormModalProps) {
  const save = useSaveClient();
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [rules, setRules] = useState<EditableRule[]>([]);

  useEffect(() => {
    if (!opened) return;
    save.reset();
    setName(client?.name ?? '');
    setDescription(client?.description ?? '');
    setRules(toEditableRules(client?.rules));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [opened, client]);

  const err = save.error instanceof ApiError ? save.error : null;
  const ruleErrors = err?.errors.filter((e) => e.field.startsWith('rules')).map((e) => `${e.field}: ${e.message}`).join(' ');

  return (
    <Modal opened={opened} onClose={onClose} title={client ? `Edit client “${client.name}”` : 'New client'} size="xl">
      <form
        onSubmit={(e) => {
          e.preventDefault();
          save.mutate(
            { id: client?.id, input: { name: name.trim(), description: description.trim() || null, rules: cleanRules(rules) } },
            {
              onSuccess: (c) => {
                notifySuccess(`Client “${c.name}” saved.`);
                onClose();
              },
            },
          );
        }}
      >
        <Stack gap="sm">
          <TextInput label="Name" required value={name} onChange={(e) => setName(e.currentTarget.value)} error={err?.fieldError('name')} data-autofocus />
          <Textarea label="Description" value={description} onChange={(e) => setDescription(e.currentTarget.value)} autosize minRows={2} />
          <Alert variant="light" color="blue" icon={<IconInfoCircle size={16} />}>
            Client rules (e.g. the client’s email domain) identify the client, not a single project. If the client has only one
            project, a client match routes the meeting there. If the client has several projects, a client match alone is
            ambiguous: add project-level rules (participants, speakers, folders or title keywords) to disambiguate, otherwise
            the meeting goes to the review queue.
          </Alert>
          <RuleEditor value={rules} onChange={setRules} label="Client rules" error={ruleErrors || undefined} />
          {save.error && <ProblemAlert error={save.error} />}
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={save.isPending} disabled={!name.trim()}>
              {client ? 'Save changes' : 'Create client'}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
