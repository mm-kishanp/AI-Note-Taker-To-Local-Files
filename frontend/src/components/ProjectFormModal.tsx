import { Button, Code, Group, Modal, NumberInput, Select, Stack, Text, TextInput, Textarea } from '@mantine/core';
import { useEffect, useState } from 'react';
import { useClients, useSaveProject } from '../hooks/useProjects';
import { ApiError } from '../services/api';
import type { ProjectDto, ProjectInput } from '../types/api';
import { notifySuccess } from '../utils/notify';
import { TEMPLATE_VARIABLES } from '../utils/rules';
import { ProblemAlert } from './ProblemAlert';
import { cleanRules, type EditableRule, RuleEditor, toEditableRules } from './RuleEditor';

export interface ProjectFormModalProps {
  opened: boolean;
  onClose: () => void;
  project?: ProjectDto | null;
  onSaved?: (p: ProjectDto) => void;
  /** Prefill (e.g. "Create new project" from the review queue). */
  initialRules?: EditableRule[];
  initialName?: string;
}

export function TemplateHelp() {
  return (
    <Text size="xs" c="dimmed">
      Relative to the export root. Leave empty to use the global template. Variables:{' '}
      {TEMPLATE_VARIABLES.map((v, i) => (
        <span key={v}>
          <Code>{v}</Code>
          {i < TEMPLATE_VARIABLES.length - 1 ? ' ' : ''}
        </span>
      ))}
    </Text>
  );
}

function errorsWithPrefix(err: unknown, prefix: string): string | undefined {
  if (!(err instanceof ApiError)) return undefined;
  const msgs = err.errors.filter((e) => e.field.toLowerCase().startsWith(prefix)).map((e) => `${e.field}: ${e.message}`);
  return msgs.length ? msgs.join(' ') : undefined;
}

export function ProjectFormModal({ opened, onClose, project, onSaved, initialRules, initialName }: ProjectFormModalProps) {
  const clients = useClients();
  const save = useSaveProject();
  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [description, setDescription] = useState('');
  const [clientId, setClientId] = useState<string | null>(null);
  const [priority, setPriority] = useState<number>(100);
  const [notesFolder, setNotesFolder] = useState('');
  const [transcriptFolder, setTranscriptFolder] = useState('');
  const [rules, setRules] = useState<EditableRule[]>([]);

  useEffect(() => {
    if (!opened) return;
    save.reset();
    setName(project?.name ?? initialName ?? '');
    setSlug(project?.slug ?? '');
    setDescription(project?.description ?? '');
    setClientId(project?.clientId ?? null);
    setPriority(project?.defaultClassificationPriority ?? 100);
    setNotesFolder(project?.notesFolderPath ?? '');
    setTranscriptFolder(project?.transcriptFolderPath ?? '');
    setRules(project ? toEditableRules(project.rules) : initialRules ?? []);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [opened, project]);

  const err = save.error instanceof ApiError ? save.error : null;

  const submit = () => {
    const input: ProjectInput = {
      name: name.trim(),
      slug: slug.trim() || null,
      description: description.trim() || null,
      clientId,
      status: project?.status ?? 'Active',
      notesFolderPath: notesFolder.trim() || null,
      transcriptFolderPath: transcriptFolder.trim() || null,
      defaultClassificationPriority: priority,
      rules: cleanRules(rules),
    };
    save.mutate(
      { id: project?.id, input },
      {
        onSuccess: (p) => {
          notifySuccess(`Project “${p.name}” saved.`);
          onSaved?.(p);
          onClose();
        },
      },
    );
  };

  return (
    <Modal opened={opened} onClose={onClose} title={project ? `Edit project “${project.name}”` : 'New project'} size="xl">
      <form
        onSubmit={(e) => {
          e.preventDefault();
          submit();
        }}
      >
        <Stack gap="sm">
          <Group grow align="flex-start">
            <TextInput label="Name" required value={name} onChange={(e) => setName(e.currentTarget.value)} error={err?.fieldError('name')} data-autofocus />
            <TextInput
              label="Slug"
              description="Optional — generated from the name. Used in folder names ({ProjectSlug})."
              value={slug}
              onChange={(e) => setSlug(e.currentTarget.value)}
              error={err?.fieldError('slug')}
              ff="monospace"
            />
          </Group>
          <Textarea label="Description" value={description} onChange={(e) => setDescription(e.currentTarget.value)} autosize minRows={2} maxRows={5} />
          <Group grow align="flex-start">
            <Select
              label="Client"
              placeholder="No client"
              data={(clients.data ?? []).map((c) => ({ value: c.id, label: c.name }))}
              value={clientId}
              onChange={setClientId}
              clearable
              searchable
              error={err?.fieldError('clientId')}
            />
            <NumberInput
              label="Priority"
              description="When several projects match, lower numbers are ranked first (e.g. primary copy in multi-export mode)."
              value={priority}
              onChange={(v) => setPriority(typeof v === 'number' ? v : Number(v) || 0)}
              min={0}
              max={10000}
            />
          </Group>
          <TextInput
            label="Notes folder override"
            placeholder="e.g. Clients/{ClientName}/{ProjectSlug}/Notes"
            value={notesFolder}
            onChange={(e) => setNotesFolder(e.currentTarget.value)}
            error={err?.fieldError('notesFolderPath')}
            ff="monospace"
          />
          <TextInput
            label="Transcripts folder override"
            placeholder="e.g. Clients/{ClientName}/{ProjectSlug}/Transcripts/{Year}"
            value={transcriptFolder}
            onChange={(e) => setTranscriptFolder(e.currentTarget.value)}
            error={err?.fieldError('transcriptFolderPath')}
            ff="monospace"
          />
          <TemplateHelp />
          <RuleEditor
            value={rules}
            onChange={setRules}
            label="Mapping rules"
            description="A meeting is routed to this project when any enabled rule matches (evaluated in the configured tier order)."
            error={errorsWithPrefix(save.error, 'rules')}
          />
          {save.error && <ProblemAlert error={save.error} />}
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={save.isPending} disabled={!name.trim()}>
              {project ? 'Save changes' : 'Create project'}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
