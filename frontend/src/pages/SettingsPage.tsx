import {
  ActionIcon, Alert, Anchor, Badge, Button, Checkbox, Code, Divider, Group, List, Modal, NumberInput, Paper, PasswordInput,
  Select, SimpleGrid, Stack, Switch, Tabs, Text, TextInput, Title, Tooltip,
} from '@mantine/core';
import {
  IconArrowDown, IconArrowUp, IconCheck, IconFolderSearch, IconKey, IconPlugConnected, IconTrash, IconX,
} from '@tabler/icons-react';
import { useEffect, useState, type ReactNode } from 'react';
import { FolderPicker } from '../components/FolderPicker';
import { PageHeader } from '../components/PageHeader';
import { PathText } from '../components/PathText';
import { ProblemAlert } from '../components/ProblemAlert';
import { LoadingRows, QueryState } from '../components/QueryState';
import {
  useAiCredential, useDeleteAiCredential, useDeleteSourceCredential, useMigrateRoot, useSetAiCredential,
  useSetSourceCredential, useSettings, useSourceCredential, useTestConnection, useUpdateSettings,
} from '../hooks/useSettings';
import { ApiError } from '../services/api';
import {
  CLASSIFICATION_TIERS, CONFLICT_POLICIES, MULTI_PROJECT_MODES,
  type ClassificationSettings, type ClassificationTier, type ConnectionTestDto, type ExportSettings, type LoggingSettings,
  MEETING_SOURCE_LABELS, type MeetingSource, type MigrateRootResultDto, type SettingsDto, type SyncSettings, type UpdateSettingsRequest,
} from '../types/api';
import { formatDateTime, formatRelative, humanize } from '../utils/format';
import { notifyError, notifySuccess } from '../utils/notify';

const TIER_HELP: Record<ClassificationTier, string> = {
  ParticipantEmail: 'Exact participant / invitee email (Email rules)',
  ParticipantIdentity: 'Attendee name or email domain (ParticipantName, EmailDomain rules)',
  SpeakerIdentity: 'Speakers Granola or Fireflies identified by name in the transcript',
  Client: 'Client rules → that client’s projects',
  Metadata: 'Granola folder membership (GranolaFolder rules)',
  Keyword: 'Title / summary keywords',
  Ai: 'Optional AI suggestion (only when enabled below)',
};

const LOG_LEVELS = ['Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical'];

/** Field errors from a 400 problem whose field starts with the given prefix. */
function fieldError(error: unknown, field: string): string | undefined {
  if (!(error instanceof ApiError)) return undefined;
  return error.errors.find((e) => e.field?.toLowerCase() === field.toLowerCase())?.message;
}

function Section({ title, description, children }: { title: string; description?: ReactNode; children: ReactNode }) {
  return (
    <Paper withBorder radius="md" p="lg">
      <Stack gap="md">
        <div>
          <Title order={4}>{title}</Title>
          {description && (
            <Text size="sm" c="dimmed" mt={4}>
              {description}
            </Text>
          )}
        </div>
        {children}
      </Stack>
    </Paper>
  );
}

/** Local editable copy of one settings section with a Save/Reset bar. */
function useSectionForm<T>(server: T | undefined) {
  const [value, setValue] = useState<T | undefined>(server);
  useEffect(() => setValue(server), [server]);
  const dirty = JSON.stringify(value) !== JSON.stringify(server);
  const patch = (p: Partial<T>) => setValue((v) => (v ? { ...v, ...p } : v));
  return { value, setValue, patch, dirty, reset: () => setValue(server) };
}

function SaveBar({ dirty, saving, onSave, onReset, error }: { dirty: boolean; saving: boolean; onSave: () => void; onReset: () => void; error: unknown }) {
  return (
    <Stack gap="xs">
      {error ? <ProblemAlert error={error} title="Settings were not saved" /> : null}
      <Group justify="flex-end">
        <Button variant="default" disabled={!dirty || saving} onClick={onReset}>
          Reset
        </Button>
        <Button disabled={!dirty} loading={saving} onClick={onSave}>
          Save
        </Button>
      </Group>
    </Stack>
  );
}

function useSaveSection() {
  const update = useUpdateSettings();
  const save = (req: Partial<UpdateSettingsRequest>, label: string) =>
    update.mutate(
      { export: null, sync: null, classification: null, logging: null, ...req },
      { onSuccess: () => notifySuccess(`${label} settings saved.`) },
    );
  return { save, saving: update.isPending, error: update.error, reset: update.reset };
}

// ---------------------------------------------------------------- Source connections

const SOURCE_INFO: Record<MeetingSource, { title: string; where: ReactNode; placeholder: string; envVar: string }> = {
  Granola: {
    title: 'Granola connection',
    where: <>Create a key in the Granola desktop app under <b>Settings → Connectors → API keys</b>.</>,
    placeholder: 'grn_…',
    envVar: 'GRANOLA_API_KEY',
  },
  Fireflies: {
    title: 'Fireflies connection',
    where: (
      <>
        Optional. Every call Fireflies.ai recorded is imported with its AI notes and transcript. Copy the key from{' '}
        <Anchor href="https://app.fireflies.ai/integrations" target="_blank" rel="noopener noreferrer">app.fireflies.ai → Integrations → Fireflies API</Anchor>.
      </>
    ),
    placeholder: 'Fireflies API key',
    envVar: 'FIREFLIES_API_KEY',
  },
  WhisperAi: {
    title: 'WhisperAI connection',
    where: (
      <>
        Optional. Every completed WhisperAI transcript on the account is imported (WhisperAI provides transcripts only, with
        anonymous speaker labels). Create a key in the{' '}
        <Anchor href="https://whisperai.com/developer" target="_blank" rel="noopener noreferrer">WhisperAI developer portal</Anchor>.
      </>
    ),
    placeholder: 'wai_…',
    envVar: 'WHISPERAI_API_KEY',
  },
};

function SourceConnection({ source }: { source: MeetingSource }) {
  const info = SOURCE_INFO[source];
  const status = useSourceCredential(source);
  const setKey = useSetSourceCredential(source);
  const del = useDeleteSourceCredential(source);
  const test = useTestConnection(source);
  const [key, setKeyValue] = useState('');
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [lastTest, setLastTest] = useState<ConnectionTestDto | null>(null);
  const s = status.data;

  return (
    <Section
      title={info.title}
      description={
        <>
          {info.where} The key is validated, then stored in the operating system's credential store; it is never shown again in full and
          never sent to this browser.
        </>
      }
    >
      <QueryState isLoading={status.isLoading} error={status.error} skeleton={<LoadingRows rows={3} />}>
        {s && (
          <Stack gap="md">
            <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="sm">
              <div>
                <Text size="xs" c="dimmed">Status</Text>
                <Group gap="xs">
                  <Badge color={s.configured ? 'green' : 'gray'} variant="light" style={{ textTransform: 'none' }}>
                    {s.configured ? 'Key configured' : 'No key'}
                  </Badge>
                  {s.lastCheckSucceeded !== null && (
                    <Badge color={s.lastCheckSucceeded ? 'green' : 'red'} variant="outline" style={{ textTransform: 'none' }}>
                      {s.lastCheckSucceeded ? 'Last check OK' : 'Last check failed'}
                    </Badge>
                  )}
                </Group>
              </div>
              <div>
                <Text size="xs" c="dimmed">Key</Text>
                <Text ff="monospace" size="sm">{s.maskedKey ?? '—'}</Text>
              </div>
              <div>
                <Text size="xs" c="dimmed">Stored in</Text>
                <Text size="sm">{s.source ?? s.storeName}</Text>
              </div>
              <div>
                <Text size="xs" c="dimmed">Last successful request</Text>
                <Text size="sm">{s.lastSuccessAt ? `${formatDateTime(s.lastSuccessAt)} (${formatRelative(s.lastSuccessAt)})` : 'never'}</Text>
              </div>
            </SimpleGrid>
            {s.lastError && s.lastCheckSucceeded === false && (
              <Alert color="red" title={humanize(s.lastErrorCategory)} role="alert">
                {s.lastError}
              </Alert>
            )}

            {s.canStore ? (
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  setKey.mutate(
                    { apiKey: key.trim(), validate: true },
                    {
                      onSuccess: () => {
                        setKeyValue('');
                        notifySuccess(`API key saved and verified with ${MEETING_SOURCE_LABELS[source]}.`);
                      },
                    },
                  );
                }}
              >
                <Group align="flex-end" wrap="wrap">
                  <PasswordInput
                    label={s.configured ? 'Replace API key' : 'API key'}
                    placeholder={info.placeholder}
                    value={key}
                    onChange={(e) => setKeyValue(e.currentTarget.value)}
                    autoComplete="off"
                    style={{ flex: 1, minWidth: 260 }}
                    error={fieldError(setKey.error, 'apiKey')}
                  />
                  <Button type="submit" leftSection={<IconKey size={16} />} loading={setKey.isPending} disabled={key.trim().length < 10}>
                    Save &amp; verify
                  </Button>
                </Group>
              </form>
            ) : (
              <Alert color="blue">
                {s.storeName} is read-only on this host. Provide the key with the <Code>{info.envVar}</Code> environment variable.
              </Alert>
            )}
            {setKey.error && !fieldError(setKey.error, 'apiKey') ? <ProblemAlert error={setKey.error} /> : null}

            <Group>
              <Button
                variant="light"
                leftSection={<IconPlugConnected size={16} />}
                loading={test.isPending}
                disabled={!s.configured}
                onClick={() => test.mutate(undefined, { onSuccess: setLastTest, onError: (e) => notifyError(e) })}
              >
                Test connection
              </Button>
              {s.configured && s.canStore && (
                <Button variant="subtle" color="red" leftSection={<IconTrash size={16} />} onClick={() => setConfirmDelete(true)}>
                  Remove key
                </Button>
              )}
            </Group>
            {lastTest && (
              <Alert
                color={lastTest.success ? 'green' : 'red'}
                icon={lastTest.success ? <IconCheck size={16} /> : <IconX size={16} />}
                title={lastTest.success ? 'Connected' : `Connection failed (${humanize(lastTest.errorCategory)})`}
                role="status"
              >
                {lastTest.message}
              </Alert>
            )}
          </Stack>
        )}
      </QueryState>
      <Modal opened={confirmDelete} onClose={() => setConfirmDelete(false)} title={`Remove the ${source} API key?`}>
        <Stack>
          <Text size="sm">
            {source} meetings stop synchronizing until a new key is added. Exported files and meeting history are not affected. To revoke the key itself,
            also delete it in {source}.
          </Text>
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setConfirmDelete(false)}>Cancel</Button>
            <Button
              color="red"
              loading={del.isPending}
              onClick={() =>
                del.mutate(undefined, {
                  onSuccess: () => {
                    setConfirmDelete(false);
                    notifySuccess('API key removed.');
                  },
                  onError: (e) => notifyError(e),
                })
              }
            >
              Remove key
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Section>
  );
}

// ---------------------------------------------------------------- Synchronization

function SyncSection({ settings }: { settings: SettingsDto }) {
  const form = useSectionForm<SyncSettings>(settings.sync);
  const { save, saving, error } = useSaveSection();
  const v = form.value!;
  if (!v) return null;
  const num = (field: keyof SyncSettings) => (val: string | number) => form.patch({ [field]: typeof val === 'number' ? val : Number(val) } as Partial<SyncSettings>);

  return (
    <Section title="Synchronization" description="Incremental syncs only fetch meetings changed since the last successful run. Failed meetings are retried with backoff.">
      <Switch
        label="Automatic synchronization"
        description="Runs an incremental sync on the interval below while the app is running."
        checked={v.autoSyncEnabled}
        onChange={(e) => form.patch({ autoSyncEnabled: e.currentTarget.checked })}
      />
      <SimpleGrid cols={{ base: 1, sm: 2 }}>
        <NumberInput label="Interval (minutes)" min={5} max={1440} value={v.intervalMinutes} onChange={num('intervalMinutes')} error={fieldError(error, 'sync.intervalMinutes')} />
        <TextInput
          type="date"
          label="Initial import from"
          description="Only import meetings created on or after this date. Empty = all history."
          value={v.initialImportFrom ? v.initialImportFrom.slice(0, 10) : ''}
          onChange={(e) => form.patch({ initialImportFrom: e.currentTarget.value ? `${e.currentTarget.value}T00:00:00Z` : null })}
        />
        <NumberInput label="Concurrent meetings" description="1–8. Requests are throttled to Granola's rate limit regardless." min={1} max={8} value={v.maxConcurrency} onChange={num('maxConcurrency')} error={fieldError(error, 'sync.maxConcurrency')} />
        <NumberInput label="Attempts per request" description="Retries for transient Granola errors (1–10)." min={1} max={10} value={v.maxRequestAttempts} onChange={num('maxRequestAttempts')} error={fieldError(error, 'sync.maxRequestAttempts')} />
        <NumberInput label="Retries per failed meeting" description="Scheduled retries before only manual retry applies." min={0} max={50} value={v.maxMeetingRetries} onChange={num('maxMeetingRetries')} error={fieldError(error, 'sync.maxMeetingRetries')} />
        <NumberInput label="Incremental overlap (minutes)" description="Tolerates clock skew between this computer and Granola." min={0} max={1440} value={v.incrementalOverlapMinutes} onChange={num('incrementalOverlapMinutes')} error={fieldError(error, 'sync.incrementalOverlapMinutes')} />
        <NumberInput label="Fireflies look-back (days)" description="Fireflies finishes transcripts and AI notes after the call; recent calls are re-checked until both arrive." min={1} max={60} value={v.firefliesLookbackDays} onChange={num('firefliesLookbackDays')} error={fieldError(error, 'sync.firefliesLookbackDays')} />
      </SimpleGrid>
      <SaveBar dirty={form.dirty} saving={saving} onReset={form.reset} error={error} onSave={() => save({ sync: v }, 'Synchronization')} />
    </Section>
  );
}

// ---------------------------------------------------------------- Export

function PathField({ label, description, value, onChange, optional }: {
  label: string; description?: string; value: string | null; onChange: (v: string | null) => void; optional?: boolean;
}) {
  const [picker, setPicker] = useState(false);
  return (
    <>
      <TextInput
        label={label}
        description={description}
        value={value ?? ''}
        placeholder={optional ? 'Same as export root' : undefined}
        onChange={(e) => onChange(e.currentTarget.value || (optional ? null : ''))}
        ff="monospace"
        rightSection={
          <Tooltip label="Browse…">
            <ActionIcon variant="subtle" onClick={() => setPicker(true)} aria-label={`Browse for ${label}`}>
              <IconFolderSearch size={16} />
            </ActionIcon>
          </Tooltip>
        }
      />
      <FolderPicker opened={picker} onClose={() => setPicker(false)} initialPath={value} onSelect={(p) => onChange(p)} title={label} />
    </>
  );
}

function ExportSection({ settings }: { settings: SettingsDto }) {
  const form = useSectionForm<ExportSettings>(settings.export);
  const { save, saving, error } = useSaveSection();
  const [migrateOpen, setMigrateOpen] = useState(false);
  const v = form.value!;
  if (!v) return null;
  const text = (field: keyof ExportSettings) => (e: React.ChangeEvent<HTMLInputElement>) => form.patch({ [field]: e.currentTarget.value } as Partial<ExportSettings>);
  const flag = (field: keyof ExportSettings) => (e: React.ChangeEvent<HTMLInputElement>) => form.patch({ [field]: e.currentTarget.checked } as Partial<ExportSettings>);

  return (
    <Section
      title="Export"
      description={
        <>
          Templates are relative to their root. Variables: <Code>{'{ProjectSlug}'}</Code> <Code>{'{ProjectName}'}</Code> <Code>{'{ClientName}'}</Code>{' '}
          <Code>{'{ProjectDirectory}'}</Code> <Code>{'{MeetingDate}'}</Code> <Code>{'{Year}'}</Code> <Code>{'{Month}'}</Code> <Code>{'{Day}'}</Code>{' '}
          <Code>{'{MeetingId}'}</Code> <Code>{'{MeetingTitle}'}</Code>. Existing files keep their paths; changes apply to newly exported meetings.
        </>
      }
    >
      <Stack gap={4}>
        <Text size="xs" c="dimmed">Currently resolved</Text>
        <Group gap="xs"><Text size="sm" w={110}>Root</Text><PathText path={settings.resolvedPaths.root} /></Group>
        <Group gap="xs"><Text size="sm" w={110}>Notes</Text><PathText path={settings.resolvedPaths.notesRoot} /></Group>
        <Group gap="xs"><Text size="sm" w={110}>Transcripts</Text><PathText path={settings.resolvedPaths.transcriptsRoot} /></Group>
      </Stack>
      <PathField label="Export root" description="Absolute, or relative to the app's base directory." value={v.rootDirectory} onChange={(p) => form.patch({ rootDirectory: p ?? '' })} />
      <SimpleGrid cols={{ base: 1, sm: 2 }}>
        <PathField label="Separate notes root (optional)" value={v.notesRootDirectory} optional onChange={(p) => form.patch({ notesRootDirectory: p })} />
        <PathField label="Separate transcripts root (optional)" value={v.transcriptsRootDirectory} optional onChange={(p) => form.patch({ transcriptsRootDirectory: p })} />
        <TextInput label="Project directory" value={v.projectDirectoryTemplate} onChange={text('projectDirectoryTemplate')} ff="monospace" />
        <TextInput label="File name" description="Without .md" value={v.filenameTemplate} onChange={text('filenameTemplate')} ff="monospace" />
        <TextInput label="Notes directory" value={v.notesDirectoryTemplate} onChange={text('notesDirectoryTemplate')} ff="monospace" />
        <TextInput label="Transcripts directory" value={v.transcriptsDirectoryTemplate} onChange={text('transcriptsDirectoryTemplate')} ff="monospace" />
        <TextInput label="Unassigned notes" value={v.unassignedNotesDirectory} onChange={text('unassignedNotesDirectory')} ff="monospace" />
        <TextInput label="Unassigned transcripts" value={v.unassignedTranscriptsDirectory} onChange={text('unassignedTranscriptsDirectory')} ff="monospace" />
        <TextInput label="Shared notes" value={v.sharedNotesDirectory} onChange={text('sharedNotesDirectory')} ff="monospace" />
        <TextInput label="Shared transcripts" value={v.sharedTranscriptsDirectory} onChange={text('sharedTranscriptsDirectory')} ff="monospace" />
      </SimpleGrid>
      <SimpleGrid cols={{ base: 1, sm: 2 }}>
        <Checkbox label="Export notes" checked={v.exportNotes} onChange={flag('exportNotes')} />
        <Checkbox label="Export transcripts" checked={v.exportTranscripts} onChange={flag('exportTranscripts')} />
        <Checkbox label="Move unmodified files when a meeting is reassigned" checked={v.moveFilesOnReassignment} onChange={flag('moveFilesOnReassignment')} />
        <Checkbox label="Link Granola photo attachments (signed URLs)" checked={v.includeAttachmentLinks} onChange={flag('includeAttachmentLinks')} />
        <Checkbox label="Maintain README.md and PROJECT_CONTEXT.md per project" checked={v.generateProjectContext} onChange={flag('generateProjectContext')} />
        <Checkbox label="Maintain Unassigned/ReviewQueue.md" checked={v.generateReviewQueue} onChange={flag('generateReviewQueue')} />
      </SimpleGrid>
      <Select
        label="When an exported file was edited locally"
        data={CONFLICT_POLICIES.map((p) => ({
          value: p,
          label: p === 'PreserveUserEdits' ? 'Preserve my edits (mark conflicts for review) — recommended' : 'Always overwrite with the source version',
        }))}
        value={v.conflictPolicy}
        onChange={(val) => val && form.patch({ conflictPolicy: val as ExportSettings['conflictPolicy'] })}
        allowDeselect={false}
      />
      <SaveBar dirty={form.dirty} saving={saving} onReset={form.reset} error={error} onSave={() => save({ export: v }, 'Export')} />
      <Divider />
      <Group justify="space-between" wrap="wrap">
        <Text size="sm" c="dimmed" maw={600}>
          Move the whole repository to another folder or drive. Files are copied; nothing is overwritten or deleted.
        </Text>
        <Button variant="light" onClick={() => setMigrateOpen(true)}>
          Migrate export root…
        </Button>
      </Group>
      <MigrateRootModal opened={migrateOpen} onClose={() => setMigrateOpen(false)} current={settings.resolvedPaths.root} />
    </Section>
  );
}

function MigrateRootModal({ opened, onClose, current }: { opened: boolean; onClose: () => void; current: string }) {
  const migrate = useMigrateRoot();
  const [target, setTarget] = useState<string | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [result, setResult] = useState<MigrateRootResultDto | null>(null);
  useEffect(() => {
    if (opened) {
      setTarget(null);
      setConfirmed(false);
      setResult(null);
      migrate.reset();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [opened]);

  return (
    <Modal opened={opened} onClose={onClose} title="Migrate export root" size="lg">
      <Stack>
        <Text size="sm">
          Copies every file from <Code>{current}</Code> to the new folder and then points the settings at it. Files already at the destination
          are never overwritten, and the current folder is left in place — delete it yourself once you have checked the copy.
        </Text>
        <PathField label="New export root" value={target} onChange={setTarget} />
        <Checkbox label="I understand the old folder is kept and must be removed manually." checked={confirmed} onChange={(e) => setConfirmed(e.currentTarget.checked)} />
        {migrate.error ? <ProblemAlert error={migrate.error} /> : null}
        {result && (
          <Alert color="green" title="Migration complete" role="status">
            <Text size="sm">
              Copied {result.filesCopied} file(s), skipped {result.filesSkipped} identical file(s).
            </Text>
            {result.warnings.length > 0 && (
              <List size="sm" mt={4}>
                {result.warnings.map((w, i) => (
                  <List.Item key={i}>{w}</List.Item>
                ))}
              </List>
            )}
          </Alert>
        )}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>{result ? 'Close' : 'Cancel'}</Button>
          {!result && (
            <Button
              disabled={!target || !confirmed}
              loading={migrate.isPending}
              onClick={() => target && migrate.mutate({ newRoot: target, confirm: true }, { onSuccess: setResult })}
            >
              Copy and switch
            </Button>
          )}
        </Group>
      </Stack>
    </Modal>
  );
}

// ---------------------------------------------------------------- Classification

function ClassificationSection({ settings }: { settings: SettingsDto }) {
  const form = useSectionForm<ClassificationSettings>(settings.classification);
  const { save, saving, error } = useSaveSection();
  const ai = useAiCredential();
  const setAi = useSetAiCredential();
  const delAi = useDeleteAiCredential();
  const [aiKey, setAiKey] = useState('');
  const v = form.value!;
  if (!v) return null;

  const order = [...v.tierOrder, ...CLASSIFICATION_TIERS.filter((t) => !v.tierOrder.includes(t))];
  const move = (i: number, d: -1 | 1) => {
    const next = [...order];
    [next[i], next[i + d]] = [next[i + d], next[i]];
    form.patch({ tierOrder: next });
  };
  const toggleTier = (t: ClassificationTier, enabled: boolean) =>
    form.patch({ disabledTiers: enabled ? v.disabledTiers.filter((x) => x !== t) : [...v.disabledTiers, t] });

  return (
    <Section
      title="Classification"
      description="Tiers are evaluated top to bottom; the first tier with evidence decides. A manual assignment always wins and is never changed by rules."
    >
      <Stack gap={6}>
        <Text size="sm" fw={500}>Rule priority</Text>
        {order.map((t, i) => (
          <Paper key={t} withBorder p="xs" radius="sm">
            <Group justify="space-between" wrap="nowrap">
              <Group gap="sm" wrap="nowrap" style={{ minWidth: 0 }}>
                <Text fw={600} w={20} ta="right">{i + 1}.</Text>
                <Switch
                  aria-label={`Enable ${humanize(t)}`}
                  checked={!v.disabledTiers.includes(t)}
                  onChange={(e) => toggleTier(t, e.currentTarget.checked)}
                />
                <div style={{ minWidth: 0 }}>
                  <Text size="sm" fw={500}>{t === 'Ai' ? 'AI' : humanize(t)}</Text>
                  <Text size="xs" c="dimmed">{TIER_HELP[t]}</Text>
                </div>
              </Group>
              <Group gap={2} wrap="nowrap">
                <ActionIcon variant="subtle" disabled={i === 0} onClick={() => move(i, -1)} aria-label={`Move ${humanize(t)} up`}>
                  <IconArrowUp size={16} />
                </ActionIcon>
                <ActionIcon variant="subtle" disabled={i === order.length - 1} onClick={() => move(i, 1)} aria-label={`Move ${humanize(t)} down`}>
                  <IconArrowDown size={16} />
                </ActionIcon>
              </Group>
            </Group>
          </Paper>
        ))}
        {fieldError(error, 'classification.tierOrder') && <Text size="sm" c="red">{fieldError(error, 'classification.tierOrder')}</Text>}
      </Stack>
      <Select
        label="When one tier matches several projects"
        data={MULTI_PROJECT_MODES.map((m) => ({
          value: m,
          label:
            m === 'SingleProject'
              ? 'Send to review (Unassigned) — recommended'
              : m === 'Shared'
                ? 'Save in the Shared folder'
                : 'Export to every matching project (one canonical file + copies)',
        }))}
        value={v.multiProjectMode}
        onChange={(val) => val && form.patch({ multiProjectMode: val as ClassificationSettings['multiProjectMode'] })}
        allowDeselect={false}
      />
      <SimpleGrid cols={{ base: 1, sm: 2 }}>
        <Checkbox label="Ignore my own email and name" description="So your presence never routes a meeting." checked={v.ignoreOwner} onChange={(e) => form.patch({ ignoreOwner: e.currentTarget.checked })} />
        <Checkbox label="Use calendar invitees as participants" checked={v.useCalendarInvitees} onChange={(e) => form.patch({ useCalendarInvitees: e.currentTarget.checked })} />
        <NumberInput
          label="Minimum identified segments per speaker"
          min={1}
          max={1000}
          value={v.minimumSpeakerSegments}
          onChange={(val) => form.patch({ minimumSpeakerSegments: Number(val) })}
          error={fieldError(error, 'classification.minimumSpeakerSegments')}
        />
      </SimpleGrid>

      <Divider label="AI-assisted classification (optional)" labelPosition="left" />
      <Alert color="blue" variant="light">
        Off by default. When enabled, meetings that no rule matched are sent to Anthropic's Claude API with: title, participant names and
        emails, identified speaker names, Granola folder names and your project list (names, clients, descriptions). Granola's summary is sent
        only if you tick “Include summary”. Transcripts are never sent. The model can only choose an existing project or none, and by default
        its answer is a suggestion for review.
      </Alert>
      <SimpleGrid cols={{ base: 1, sm: 2 }}>
        <Switch label="Enable AI suggestions" checked={v.ai.enabled} onChange={(e) => form.patch({ ai: { ...v.ai, enabled: e.currentTarget.checked } })} />
        <TextInput label="Model" value={v.ai.model} onChange={(e) => form.patch({ ai: { ...v.ai, model: e.currentTarget.value } })} ff="monospace" error={fieldError(error, 'classification.ai.model')} />
        <Checkbox label="Assign automatically (low certainty) instead of only suggesting" checked={v.ai.autoAssign} onChange={(e) => form.patch({ ai: { ...v.ai, autoAssign: e.currentTarget.checked } })} />
        <Checkbox label="Include Granola's summary" checked={v.ai.includeSummary} onChange={(e) => form.patch({ ai: { ...v.ai, includeSummary: e.currentTarget.checked } })} />
      </SimpleGrid>
      <SaveBar dirty={form.dirty} saving={saving} onReset={form.reset} error={error} onSave={() => save({ classification: v }, 'Classification')} />

      <Group align="flex-end" wrap="wrap">
        <div>
          <Text size="xs" c="dimmed">Anthropic API key</Text>
          <Text size="sm" ff="monospace">{ai.data?.keyConfigured ? ai.data.maskedKey : 'not configured'}</Text>
        </div>
        {ai.data?.canStore ? (
          <>
            <PasswordInput placeholder="sk-ant-…" value={aiKey} onChange={(e) => setAiKey(e.currentTarget.value)} autoComplete="off" aria-label="Anthropic API key" style={{ flex: 1, minWidth: 220 }} />
            <Button
              variant="light"
              disabled={aiKey.trim().length < 10}
              loading={setAi.isPending}
              onClick={() => setAi.mutate({ apiKey: aiKey.trim() }, { onSuccess: () => { setAiKey(''); notifySuccess('AI key saved.'); }, onError: (e) => notifyError(e) })}
            >
              Save key
            </Button>
            {ai.data.keyConfigured && (
              <Button variant="subtle" color="red" loading={delAi.isPending} onClick={() => delAi.mutate(undefined, { onError: (e) => notifyError(e) })}>
                Remove
              </Button>
            )}
          </>
        ) : (
          <Text size="sm" c="dimmed">Set <Code>ANTHROPIC_API_KEY</Code> on this host.</Text>
        )}
      </Group>
    </Section>
  );
}

// ---------------------------------------------------------------- Logging

function LoggingSection({ settings }: { settings: SettingsDto }) {
  const form = useSectionForm<LoggingSettings>(settings.logging);
  const { save, saving, error } = useSaveSection();
  const v = form.value!;
  if (!v) return null;
  return (
    <Section title="Logging" description="Structured JSON-lines log files. API keys and bearer tokens are always redacted.">
      <SimpleGrid cols={{ base: 1, sm: 3 }}>
        <Select label="Level" data={LOG_LEVELS} value={v.minimumLevel} onChange={(val) => val && form.patch({ minimumLevel: val })} allowDeselect={false} error={fieldError(error, 'logging.minimumLevel')} />
        <TextInput label="Log directory" value={v.logDirectory} onChange={(e) => form.patch({ logDirectory: e.currentTarget.value })} ff="monospace" error={fieldError(error, 'logging.logDirectory')} />
        <NumberInput label="Retention (days)" min={1} max={365} value={v.retentionDays} onChange={(val) => form.patch({ retentionDays: Number(val) })} error={fieldError(error, 'logging.retentionDays')} />
      </SimpleGrid>
      <SaveBar dirty={form.dirty} saving={saving} onReset={form.reset} error={error} onSave={() => save({ logging: v }, 'Logging')} />
    </Section>
  );
}

export function SettingsPage() {
  const settings = useSettings();
  return (
    <>
      <PageHeader title="Settings" description={<>Configuration is stored in the local database. API docs: <Anchor href="https://docs.granola.ai/introduction" target="_blank" rel="noopener noreferrer">Granola</Anchor>, <Anchor href="https://docs.fireflies.ai/getting-started/quickstart" target="_blank" rel="noopener noreferrer">Fireflies</Anchor>, <Anchor href="https://whisperai.com/whisper-api/docs" target="_blank" rel="noopener noreferrer">WhisperAI</Anchor>.</>} docTitle="Settings" />
      <Tabs defaultValue="connection" keepMounted={false}>
        <Tabs.List mb="md">
          <Tabs.Tab value="connection">Connections</Tabs.Tab>
          <Tabs.Tab value="sync">Synchronization</Tabs.Tab>
          <Tabs.Tab value="export">Export</Tabs.Tab>
          <Tabs.Tab value="classification">Classification</Tabs.Tab>
          <Tabs.Tab value="logging">Logging</Tabs.Tab>
        </Tabs.List>
        <Tabs.Panel value="connection">
          <Stack gap="lg">
            <SourceConnection source="Granola" />
            <SourceConnection source="Fireflies" />
            <SourceConnection source="WhisperAi" />
          </Stack>
        </Tabs.Panel>
        {(['sync', 'export', 'classification', 'logging'] as const).map((tab) => (
          <Tabs.Panel key={tab} value={tab}>
            <QueryState isLoading={settings.isLoading} error={settings.error} onRetry={() => void settings.refetch()}>
              {settings.data && tab === 'sync' && <SyncSection settings={settings.data} />}
              {settings.data && tab === 'export' && <ExportSection settings={settings.data} />}
              {settings.data && tab === 'classification' && <ClassificationSection settings={settings.data} />}
              {settings.data && tab === 'logging' && <LoggingSection settings={settings.data} />}
            </QueryState>
          </Tabs.Panel>
        ))}
      </Tabs>
    </>
  );
}
