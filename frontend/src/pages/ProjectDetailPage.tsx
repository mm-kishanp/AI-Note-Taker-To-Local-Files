import {
  Alert, Anchor, Badge, Button, Card, Checkbox, Group, NumberInput, Paper, SimpleGrid, Stack, Table, Text, Title,
} from '@mantine/core';
import { modals } from '@mantine/modals';
import { IconArchive, IconArrowLeft, IconEdit, IconFlask, IconRestore } from '@tabler/icons-react';
import { useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { EmptyState } from '../components/EmptyState';
import { MeetingsTable } from '../components/MeetingsTable';
import { PageHeader } from '../components/PageHeader';
import { PathText } from '../components/PathText';
import { ProblemAlert } from '../components/ProblemAlert';
import { ProjectFormModal } from '../components/ProjectFormModal';
import { LoadingRows, QueryState } from '../components/QueryState';
import { cleanRules, type EditableRule, RuleEditor, toEditableRules } from '../components/RuleEditor';
import { StatusBadge } from '../components/StatusBadge';
import { useMeetings } from '../hooks/useMeetings';
import { useApplyMapping, useProject, useSaveProject, useSetProjectArchived, useTestMapping } from '../hooks/useProjects';
import { ApiError } from '../services/api';
import type { ProjectDto, TestMappingResultDto } from '../types/api';
import { formatDate, formatDateTime } from '../utils/format';
import { notifyError, notifyInfo, notifySuccess } from '../utils/notify';

function sameRules(a: EditableRule[], p: ProjectDto) {
  const norm = (rs: EditableRule[]) => JSON.stringify(cleanRules(rs));
  return norm(a) === norm(toEditableRules(p.rules));
}

function MappingPanel({ project, rules, dirty }: { project: ProjectDto; rules: EditableRule[]; dirty: boolean }) {
  const test = useTestMapping();
  const apply = useApplyMapping();
  const [limit, setLimit] = useState<number>(500);
  const [result, setResult] = useState<TestMappingResultDto | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [onlyChanges, setOnlyChanges] = useState(true);

  const run = () =>
    test.mutate(
      { id: project.id, req: { rules: cleanRules(rules), limit } },
      {
        onSuccess: (r) => {
          setResult(r);
          setSelected(new Set(r.items.filter((i) => i.changes && !i.isManual).map((i) => i.meetingId)));
        },
      },
    );

  const rows = (result?.items ?? []).filter((i) => !onlyChanges || i.changes);
  const toggle = (id: string) =>
    setSelected((s) => {
      const n = new Set(s);
      if (n.has(id)) n.delete(id);
      else n.add(id);
      return n;
    });

  const doApply = () =>
    modals.openConfirmModal({
      title: `Apply rules to ${selected.size} meeting(s)?`,
      children: (
        <Stack gap="xs">
          <Text size="sm">
            The selected meetings are re-classified with the <b>saved</b> rules of “{project.name}” and their files re-exported in
            the background. Manual assignments are always kept.
          </Text>
          {project && <Text size="sm">Files of reassigned meetings may move to new folders (if “move files on reassignment” is on).</Text>}
        </Stack>
      ),
      labels: { confirm: 'Apply', cancel: 'Cancel' },
      onConfirm: () =>
        apply.mutate(
          { id: project.id, req: { meetingIds: Array.from(selected), confirm: true } },
          {
            onSuccess: (r) => notifySuccess(r.message || 'Re-classification started.', 'Started'),
            onError: (e) => (e instanceof ApiError && e.status === 409 ? notifyInfo(e.detail ?? 'A sync is already running.') : notifyError(e)),
          },
        ),
    });

  return (
    <Card withBorder radius="md">
      <Title order={4} mb={4}>
        Test mapping
      </Title>
      <Text size="sm" c="dimmed" mb="sm">
        Dry run of the rules currently in the editor (including unsaved changes) against existing meetings. Nothing is changed.
      </Text>
      <Group align="flex-end" gap="sm" mb="sm">
        <NumberInput label="Meetings to evaluate" value={limit} onChange={(v) => setLimit(Number(v) || 100)} min={1} max={5000} w={170} />
        <Button leftSection={<IconFlask size={16} />} onClick={run} loading={test.isPending}>
          Test mapping
        </Button>
      </Group>
      {test.error && <ProblemAlert error={test.error} />}
      {result && (
        <Stack gap="sm">
          <Group gap="md">
            <Text size="sm">Evaluated: <b>{result.evaluated}</b></Text>
            <Text size="sm">Would match: <b>{result.wouldMatch}</b></Text>
            <Text size="sm">Would change: <b>{result.wouldChange}</b></Text>
            <Checkbox label="Only show changes" checked={onlyChanges} onChange={(e) => setOnlyChanges(e.currentTarget.checked)} />
          </Group>
          {rows.length === 0 ? (
            <Text size="sm" c="dimmed">
              No meetings would change.
            </Text>
          ) : (
            <Table.ScrollContainer minWidth={900}>
              <Table verticalSpacing={4} striped>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th w={40}>
                      <Checkbox
                        aria-label="Select all changing meetings"
                        checked={rows.filter((r) => r.changes && !r.isManual).every((r) => selected.has(r.meetingId)) && selected.size > 0}
                        indeterminate={selected.size > 0 && !rows.filter((r) => r.changes && !r.isManual).every((r) => selected.has(r.meetingId))}
                        onChange={(e) =>
                          setSelected(e.currentTarget.checked ? new Set(rows.filter((r) => r.changes && !r.isManual).map((r) => r.meetingId)) : new Set())
                        }
                      />
                    </Table.Th>
                    <Table.Th>Meeting</Table.Th>
                    <Table.Th>Current</Table.Th>
                    <Table.Th>Proposed</Table.Th>
                    <Table.Th>Reason</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {rows.map((i) => (
                    <Table.Tr key={i.meetingId}>
                      <Table.Td>
                        <Checkbox
                          aria-label={`Select ${i.title}`}
                          checked={selected.has(i.meetingId)}
                          disabled={!i.changes || i.isManual}
                          onChange={() => toggle(i.meetingId)}
                        />
                      </Table.Td>
                      <Table.Td>
                        <Anchor component={Link} to={`/meetings/${i.meetingId}`} size="sm">
                          {i.title}
                        </Anchor>
                        <Text size="xs" c="dimmed">
                          {formatDate(i.meetingDate)}
                        </Text>
                        {i.isManual && (
                          <Badge size="xs" color="orange" variant="light" style={{ textTransform: 'none' }}>
                            Manual — will be kept
                          </Badge>
                        )}
                      </Table.Td>
                      <Table.Td>
                        <StatusBadge kind="classification" value={i.currentStatus} />
                        <Text size="xs">{i.currentProject ?? '—'}</Text>
                      </Table.Td>
                      <Table.Td>
                        <StatusBadge kind="classification" value={i.proposedStatus} />
                        <Text size="xs" fw={i.changes ? 600 : undefined}>
                          {i.proposedProject ?? '—'}
                        </Text>
                      </Table.Td>
                      <Table.Td maw={320}>
                        <Text size="xs">{i.reason}</Text>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          )}
          {dirty && (
            <Alert color="yellow" variant="light">
              You have unsaved rule changes. Applying uses the saved rules — save the rules first to apply what you tested.
            </Alert>
          )}
          {apply.error && <ProblemAlert error={apply.error} />}
          <Group justify="flex-end">
            <Button onClick={doApply} disabled={selected.size === 0 || dirty} loading={apply.isPending}>
              Apply to selected meetings ({selected.size})
            </Button>
          </Group>
        </Stack>
      )}
    </Card>
  );
}

export function ProjectDetailPage() {
  const { id = '' } = useParams();
  const project = useProject(id);
  const save = useSaveProject();
  const archive = useSetProjectArchived();
  const [editOpen, setEditOpen] = useState(false);
  const [rules, setRules] = useState<EditableRule[]>([]);
  const recent = useMeetings({ projectId: id, page: 1, pageSize: 10, sort: 'date', desc: true });

  useEffect(() => {
    if (project.data) setRules(toEditableRules(project.data.rules));
  }, [project.data]);

  const dirty = useMemo(() => (project.data ? !sameRules(rules, project.data) : false), [rules, project.data]);

  if (project.isLoading) return <LoadingRows rows={10} />;
  if (project.error || !project.data)
    return (
      <>
        <PageHeader title="Project" />
        <ProblemAlert error={project.error} onRetry={() => void project.refetch()} />
      </>
    );
  const p = project.data;

  const saveRules = () =>
    save.mutate(
      {
        id: p.id,
        input: {
          name: p.name, slug: p.slug, description: p.description, clientId: p.clientId, status: p.status,
          notesFolderPath: p.notesFolderPath, transcriptFolderPath: p.transcriptFolderPath,
          defaultClassificationPriority: p.defaultClassificationPriority, rules: cleanRules(rules),
        },
      },
      { onSuccess: () => notifySuccess('Rules saved. Use “Test mapping” to see which existing meetings would change.') },
    );

  const toggleArchive = () => {
    const archiving = p.status === 'Active';
    modals.openConfirmModal({
      title: archiving ? `Archive “${p.name}”?` : `Restore “${p.name}”?`,
      children: (
        <Text size="sm">
          {archiving
            ? 'Archived projects no longer receive new meetings from rules. Existing meetings and files are kept.'
            : 'The project becomes active again and its rules are used for classification.'}
        </Text>
      ),
      labels: { confirm: archiving ? 'Archive' : 'Restore', cancel: 'Cancel' },
      confirmProps: { color: archiving ? 'orange' : 'green' },
      onConfirm: () =>
        archive.mutate({ id: p.id, archived: archiving }, { onSuccess: () => notifySuccess(archiving ? 'Project archived.' : 'Project restored.'), onError: (e) => notifyError(e) }),
    });
  };

  const ruleErrors =
    save.error instanceof ApiError ? save.error.errors.filter((e) => e.field.startsWith('rules')).map((e) => `${e.field}: ${e.message}`).join(' ') : '';

  return (
    <>
      <Anchor component={Link} to="/projects" size="sm">
        <Group gap={4}>
          <IconArrowLeft size={14} /> Projects
        </Group>
      </Anchor>
      <PageHeader
        title={p.name}
        description={
          <Group gap="xs" component="span">
            <Badge color={p.status === 'Active' ? 'green' : 'gray'} variant="light" style={{ textTransform: 'none' }}>
              {p.status}
            </Badge>
            {p.clientName && <span>Client: {p.clientName}</span>}
            <span>{p.meetingCount} meetings</span>
          </Group>
        }
        actions={
          <>
            <Button leftSection={<IconEdit size={16} />} onClick={() => setEditOpen(true)}>
              Edit
            </Button>
            <Button
              variant="default"
              leftSection={p.status === 'Active' ? <IconArchive size={16} /> : <IconRestore size={16} />}
              onClick={toggleArchive}
              loading={archive.isPending}
            >
              {p.status === 'Active' ? 'Archive' : 'Restore'}
            </Button>
          </>
        }
      />
      <Stack gap="md">
        <SimpleGrid cols={{ base: 1, lg: 2 }} spacing="md">
          <Card withBorder radius="md">
            <Title order={4} mb="sm">
              Details
            </Title>
            <Stack gap="xs">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>Slug</Text>
                <Text size="sm" ff="monospace">{p.slug}</Text>
              </div>
              {p.description && (
                <div>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={600}>Description</Text>
                  <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>{p.description}</Text>
                </div>
              )}
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>Priority</Text>
                <Text size="sm">{p.defaultClassificationPriority}</Text>
              </div>
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>Created / updated</Text>
                <Text size="sm">{formatDateTime(p.createdAt)} / {formatDateTime(p.updatedAt)}</Text>
              </div>
            </Stack>
          </Card>
          <Card withBorder radius="md">
            <Title order={4} mb="sm">
              Export folders
            </Title>
            <Stack gap="xs">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>Notes directory</Text>
                <PathText path={p.resolvedNotesDirectory} />
                {p.notesFolderPath && <Text size="xs" c="dimmed">Override template: <Text span ff="monospace" size="xs">{p.notesFolderPath}</Text></Text>}
              </div>
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>Transcripts directory</Text>
                <PathText path={p.resolvedTranscriptsDirectory} />
                {p.transcriptFolderPath && <Text size="xs" c="dimmed">Override template: <Text span ff="monospace" size="xs">{p.transcriptFolderPath}</Text></Text>}
              </div>
            </Stack>
          </Card>
        </SimpleGrid>

        <Card withBorder radius="md">
          <RuleEditor
            value={rules}
            onChange={setRules}
            label="Mapping rules"
            description="Edit the rules, test them below, then save. Changes are not applied to existing meetings until you apply them."
            error={ruleErrors || undefined}
          />
          {save.error && !ruleErrors && <ProblemAlert error={save.error} />}
          <Group justify="flex-end" mt="sm">
            <Button variant="default" disabled={!dirty} onClick={() => setRules(toEditableRules(p.rules))}>
              Discard changes
            </Button>
            <Button disabled={!dirty} loading={save.isPending} onClick={saveRules}>
              Save rules
            </Button>
          </Group>
        </Card>

        <MappingPanel project={p} rules={rules} dirty={dirty} />

        <Card withBorder radius="md">
          <Group justify="space-between" mb="sm">
            <Title order={4}>Recent meetings</Title>
            <Button component={Link} to={`/meetings?projectId=${p.id}`} variant="subtle" size="xs">
              All meetings in this project
            </Button>
          </Group>
          <QueryState isLoading={recent.isLoading} error={recent.error}>
            {recent.data && recent.data.items.length > 0 ? (
              <Paper>
                <MeetingsTable items={recent.data.items} compact />
              </Paper>
            ) : (
              <EmptyState title="No meetings in this project yet" />
            )}
          </QueryState>
        </Card>
      </Stack>
      <ProjectFormModal opened={editOpen} onClose={() => setEditOpen(false)} project={p} />
    </>
  );
}
