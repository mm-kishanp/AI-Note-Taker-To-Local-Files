import {
  ActionIcon, Alert, Anchor, Badge, Button, FileButton, Group, List, Paper, Stack, Switch, Table, Tabs, Text, TextInput,
  Tooltip,
} from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import { modals } from '@mantine/modals';
import { IconDownload, IconEdit, IconPlus, IconSearch, IconTrash, IconUpload } from '@tabler/icons-react';
import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { ClientFormModal } from '../components/ClientFormModal';
import { EmptyState } from '../components/EmptyState';
import { PageHeader } from '../components/PageHeader';
import { ProblemAlert } from '../components/ProblemAlert';
import { ProjectFormModal } from '../components/ProjectFormModal';
import { QueryState } from '../components/QueryState';
import { useClients, useDeleteClient, useImportConfig, useProjects } from '../hooks/useProjects';
import { api } from '../services/api';
import type { ClientDto, ImportResultDto, ProjectConfigExport } from '../types/api';
import { downloadJson, formatDate, formatRelative } from '../utils/format';
import { notifyError, notifySuccess } from '../utils/notify';

function ProjectsTab() {
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [debounced] = useDebouncedValue(search, 300);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const projects = useProjects(debounced.trim(), includeArchived);
  const importCfg = useImportConfig();
  const [importResult, setImportResult] = useState<ImportResultDto | null>(null);
  const [exporting, setExporting] = useState(false);

  const doExport = async () => {
    setExporting(true);
    try {
      const cfg = await api.projects.exportConfig();
      downloadJson(`granola-projects-${new Date().toISOString().slice(0, 10)}.json`, cfg);
    } catch (e) {
      notifyError(e, 'Export failed');
    } finally {
      setExporting(false);
    }
  };

  const doImport = async (file: File | null) => {
    if (!file) return;
    let cfg: ProjectConfigExport;
    try {
      cfg = JSON.parse(await file.text()) as ProjectConfigExport;
      if (!cfg || !Array.isArray(cfg.projects) || !Array.isArray(cfg.clients)) throw new Error('File does not look like a project configuration export.');
    } catch (e) {
      notifyError(e, 'Invalid file');
      return;
    }
    modals.openConfirmModal({
      title: 'Import project configuration?',
      children: (
        <Text size="sm">
          {cfg.projects.length} project(s) and {cfg.clients.length} client(s) will be created or updated (matched by name/slug).
          Meetings are not re-classified automatically.
        </Text>
      ),
      labels: { confirm: 'Import', cancel: 'Cancel' },
      onConfirm: () =>
        importCfg.mutate(cfg, {
          onSuccess: (r) => {
            setImportResult(r);
            notifySuccess('Configuration imported.');
          },
          onError: (e) => notifyError(e, 'Import failed'),
        }),
    });
  };

  return (
    <Stack gap="md">
      <Group justify="space-between" wrap="wrap">
        <Group gap="sm" wrap="wrap">
          <TextInput
            placeholder="Search projects"
            aria-label="Search projects"
            leftSection={<IconSearch size={14} />}
            value={search}
            onChange={(e) => setSearch(e.currentTarget.value)}
            w={260}
          />
          <Switch label="Include archived" checked={includeArchived} onChange={(e) => setIncludeArchived(e.currentTarget.checked)} />
        </Group>
        <Group gap="xs">
          <Button variant="default" leftSection={<IconDownload size={16} />} onClick={doExport} loading={exporting}>
            Export config
          </Button>
          <FileButton onChange={doImport} accept="application/json,.json">
            {(props) => (
              <Button variant="default" leftSection={<IconUpload size={16} />} loading={importCfg.isPending} {...props}>
                Import config
              </Button>
            )}
          </FileButton>
          <Button leftSection={<IconPlus size={16} />} onClick={() => setCreateOpen(true)}>
            New project
          </Button>
        </Group>
      </Group>

      {importResult && (
        <Alert color={importResult.warnings.length ? 'yellow' : 'green'} title="Import result" withCloseButton onClose={() => setImportResult(null)}>
          <Text size="sm">
            Clients: {importResult.clientsCreated} created, {importResult.clientsUpdated} updated. Projects: {importResult.projectsCreated} created,{' '}
            {importResult.projectsUpdated} updated.
          </Text>
          {importResult.warnings.length > 0 && (
            <List size="sm" mt={4}>
              {importResult.warnings.map((w, i) => (
                <List.Item key={i}>{w}</List.Item>
              ))}
            </List>
          )}
        </Alert>
      )}

      <QueryState isLoading={projects.isLoading} error={projects.error} onRetry={() => void projects.refetch()}>
        {projects.data && projects.data.length === 0 ? (
          <EmptyState
            title={debounced ? 'No projects match your search' : 'No projects yet'}
            description="Projects define where meeting notes are filed and which rules route meetings to them."
            action={<Button onClick={() => setCreateOpen(true)}>Create project</Button>}
          />
        ) : (
          <Paper withBorder radius="md">
            <Table.ScrollContainer minWidth={800}>
              <Table highlightOnHover striped verticalSpacing="xs">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Name</Table.Th>
                    <Table.Th>Client</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Meetings</Table.Th>
                    <Table.Th>Rules</Table.Th>
                    <Table.Th>Last meeting</Table.Th>
                    <Table.Th>Priority</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {projects.data?.map((p) => (
                    <Table.Tr
                      key={p.id}
                      className="gpi-clickable-row"
                      tabIndex={0}
                      onClick={() => navigate(`/projects/${p.id}`)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') navigate(`/projects/${p.id}`);
                      }}
                      aria-label={`Open project ${p.name}`}
                    >
                      <Table.Td>
                        <Anchor component={Link} to={`/projects/${p.id}`} fw={500} size="sm" tabIndex={-1} onClick={(e) => e.stopPropagation()}>
                          {p.name}
                        </Anchor>
                        <Text size="xs" c="dimmed" ff="monospace">
                          {p.slug}
                        </Text>
                      </Table.Td>
                      <Table.Td>{p.clientName ?? <Text c="dimmed" span>—</Text>}</Table.Td>
                      <Table.Td>
                        <Badge color={p.status === 'Active' ? 'green' : 'gray'} variant="light" style={{ textTransform: 'none' }}>
                          {p.status}
                        </Badge>
                      </Table.Td>
                      <Table.Td>{p.meetingCount}</Table.Td>
                      <Table.Td>
                        {p.rules.filter((r) => r.enabled).length}
                        {p.rules.some((r) => !r.enabled) && <Text span c="dimmed" size="xs"> (+{p.rules.filter((r) => !r.enabled).length} disabled)</Text>}
                      </Table.Td>
                      <Table.Td title={formatDate(p.lastMeetingAt)}>{formatRelative(p.lastMeetingAt, '—')}</Table.Td>
                      <Table.Td>{p.defaultClassificationPriority}</Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          </Paper>
        )}
      </QueryState>
      <ProjectFormModal opened={createOpen} onClose={() => setCreateOpen(false)} onSaved={(p) => navigate(`/projects/${p.id}`)} />
    </Stack>
  );
}

function ClientsTab() {
  const clients = useClients();
  const del = useDeleteClient();
  const [editing, setEditing] = useState<ClientDto | null>(null);
  const [formOpen, setFormOpen] = useState(false);

  const confirmDelete = (c: ClientDto) =>
    modals.openConfirmModal({
      title: `Delete client “${c.name}”?`,
      children: (
        <Text size="sm">
          Its client-level rules are removed. {c.projects.length > 0 ? `${c.projects.length} project(s) will no longer be linked to a client.` : ''} Meetings and
          files are not deleted.
        </Text>
      ),
      labels: { confirm: 'Delete client', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => del.mutate(c.id, { onSuccess: () => notifySuccess('Client deleted.'), onError: (e) => notifyError(e) }),
    });

  return (
    <Stack gap="md">
      <Alert variant="light" color="blue">
        Clients group projects and can carry their own rules (typically the client’s email domain). When a client has more than
        one project, a client-rule match cannot decide between them — add project rules (participants, speakers, folders, title
        keywords) to disambiguate, otherwise matching meetings go to the review queue.
      </Alert>
      <Group justify="flex-end">
        <Button
          leftSection={<IconPlus size={16} />}
          onClick={() => {
            setEditing(null);
            setFormOpen(true);
          }}
        >
          New client
        </Button>
      </Group>
      <QueryState isLoading={clients.isLoading} error={clients.error} onRetry={() => void clients.refetch()}>
        {clients.data && clients.data.length === 0 ? (
          <EmptyState title="No clients yet" description="Clients are optional. Use them to share rules between related projects." />
        ) : (
          <Paper withBorder radius="md">
            <Table.ScrollContainer minWidth={700}>
              <Table striped verticalSpacing="xs">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Name</Table.Th>
                    <Table.Th>Projects</Table.Th>
                    <Table.Th>Rules</Table.Th>
                    <Table.Th>Actions</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {clients.data?.map((c) => (
                    <Table.Tr key={c.id}>
                      <Table.Td>
                        <Text fw={500} size="sm">
                          {c.name}
                        </Text>
                        {c.description && (
                          <Text size="xs" c="dimmed" lineClamp={2}>
                            {c.description}
                          </Text>
                        )}
                      </Table.Td>
                      <Table.Td>
                        <Group gap={6}>
                          {c.projects.length === 0 && <Text size="sm" c="dimmed">None</Text>}
                          {c.projects.map((p) => (
                            <Anchor key={p.id} component={Link} to={`/projects/${p.id}`} size="sm">
                              {p.name}
                            </Anchor>
                          ))}
                        </Group>
                        {c.projects.length > 1 && (
                          <Text size="xs" c="yellow.8">
                            Multiple projects — project rules needed to disambiguate.
                          </Text>
                        )}
                      </Table.Td>
                      <Table.Td>
                        <Stack gap={2}>
                          {c.rules.length === 0 && <Text size="sm" c="dimmed">No rules</Text>}
                          {c.rules.map((r) => (
                            <Text key={r.id} size="xs" c={r.enabled ? undefined : 'dimmed'}>
                              {r.kind}: <Text span ff="monospace" size="xs">{r.value}</Text>
                              {!r.enabled && ' (disabled)'}
                            </Text>
                          ))}
                        </Stack>
                      </Table.Td>
                      <Table.Td>
                        <Group gap={4} wrap="nowrap">
                          <Tooltip label="Edit client">
                            <ActionIcon
                              variant="subtle"
                              onClick={() => {
                                setEditing(c);
                                setFormOpen(true);
                              }}
                              aria-label={`Edit client ${c.name}`}
                            >
                              <IconEdit size={16} />
                            </ActionIcon>
                          </Tooltip>
                          <Tooltip label="Delete client">
                            <ActionIcon variant="subtle" color="red" onClick={() => confirmDelete(c)} aria-label={`Delete client ${c.name}`}>
                              <IconTrash size={16} />
                            </ActionIcon>
                          </Tooltip>
                        </Group>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          </Paper>
        )}
      </QueryState>
      {del.error && <ProblemAlert error={del.error} />}
      <ClientFormModal opened={formOpen} onClose={() => setFormOpen(false)} client={editing} />
    </Stack>
  );
}

export function ProjectsPage({ initialTab = 'projects' }: { initialTab?: 'projects' | 'clients' }) {
  const [params, setParams] = useSearchParams();
  const tab = params.get('tab') ?? initialTab;
  return (
    <>
      <PageHeader title="Projects & clients" description="Projects decide where meetings are filed; rules decide which meetings belong to them." />
      <Tabs
        value={tab}
        onChange={(v) => {
          const next = new URLSearchParams(params);
          next.set('tab', v ?? 'projects');
          setParams(next, { replace: true });
        }}
      >
        <Tabs.List mb="md">
          <Tabs.Tab value="projects">Projects</Tabs.Tab>
          <Tabs.Tab value="clients">Clients</Tabs.Tab>
        </Tabs.List>
        <Tabs.Panel value="projects">
          <ProjectsTab />
        </Tabs.Panel>
        <Tabs.Panel value="clients">
          <ClientsTab />
        </Tabs.Panel>
      </Tabs>
    </>
  );
}
