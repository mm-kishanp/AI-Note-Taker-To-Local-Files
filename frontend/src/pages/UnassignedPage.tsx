import { Alert, Anchor, Badge, Button, Card, Group, Menu, Pagination, Stack, Text, Tooltip, UnstyledButton } from '@mantine/core';
import { modals } from '@mantine/modals';
import {
  IconBan, IconBulb, IconChevronDown, IconFolderPlus, IconLink, IconRefresh, IconShare, IconSparkles, IconTag,
} from '@tabler/icons-react';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { EmptyState } from '../components/EmptyState';
import { PageHeader } from '../components/PageHeader';
import { ProjectFormModal } from '../components/ProjectFormModal';
import { LoadingRows, QueryState } from '../components/QueryState';
import { ReassignModal } from '../components/ReassignModal';
import { SaveMappingModal } from '../components/SaveMappingModal';
import { StatusBadge } from '../components/StatusBadge';
import { useReclassify } from '../hooks/useMeetings';
import { useAssignUnassigned, useUnassigned } from '../hooks/useUnassigned';
import type { ManualAssignmentKind, RuleKind, UnassignedItemDto } from '../types/api';
import { formatDateTime } from '../utils/format';
import { notifyError, notifySuccess } from '../utils/notify';

interface AssignState {
  item: UnassignedItemDto;
  kind: ManualAssignmentKind;
  projectId: string | null;
}

interface MappingState {
  item: UnassignedItemDto;
  kind: RuleKind;
  value: string;
  projectId?: string | null;
}

function Chip({ label, onClick, title }: { label: string; onClick: () => void; title: string }) {
  return (
    <Tooltip label={title} withArrow openDelay={300}>
      <UnstyledButton onClick={onClick} aria-label={title}>
        <Badge variant="light" color="gray" style={{ textTransform: 'none', cursor: 'pointer' }} leftSection={<IconTag size={10} />}>
          {label}
        </Badge>
      </UnstyledButton>
    </Tooltip>
  );
}

function UnassignedCard({
  item, onAssign, onMapping, onCreateProject,
}: {
  item: UnassignedItemDto;
  onAssign: (s: AssignState) => void;
  onMapping: (s: MappingState) => void;
  onCreateProject: (item: UnassignedItemDto) => void;
}) {
  const m = item.meeting;
  const reclassify = useReclassify();
  const assign = useAssignUnassigned();
  const external = item.participants.filter((p) => !p.isOwner);

  const exclude = () =>
    modals.openConfirmModal({
      title: 'Exclude from export?',
      children: <Text size="sm">“{m.title}” will not be exported and leaves the review queue.</Text>,
      labels: { confirm: 'Exclude', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () =>
        assign.mutate(
          { id: m.id, req: { kind: 'Excluded', projectId: null, note: null } },
          { onSuccess: () => notifySuccess('Meeting excluded.'), onError: (e) => notifyError(e) },
        ),
    });

  return (
    <Card withBorder radius="md" component="article" aria-labelledby={`un-${m.id}`}>
      <Stack gap="sm">
        <Group justify="space-between" align="flex-start" wrap="wrap">
          <div style={{ minWidth: 0 }}>
            <Anchor component={Link} to={`/meetings/${m.id}`} fw={600} id={`un-${m.id}`}>
              {m.title || '(untitled)'}
            </Anchor>
            <Group gap="xs">
              <Text size="sm" c="dimmed">
                {formatDateTime(m.meetingDate)}
              </Text>
              <StatusBadge kind="classification" value={m.classificationStatus} srLabel="Classification" />
            </Group>
          </div>
          <Group gap="xs">
            <Button size="xs" onClick={() => onAssign({ item, kind: 'Project', projectId: null })}>
              Assign to project
            </Button>
            <Menu position="bottom-end">
              <Menu.Target>
                <Button size="xs" variant="default" rightSection={<IconChevronDown size={14} />}>
                  More actions
                </Button>
              </Menu.Target>
              <Menu.Dropdown>
                <Menu.Item leftSection={<IconShare size={16} />} onClick={() => onAssign({ item, kind: 'Shared', projectId: null })}>
                  Assign to Shared
                </Menu.Item>
                <Menu.Item
                  leftSection={<IconLink size={16} />}
                  onClick={() => onMapping({ item, kind: 'Email', value: external.find((p) => p.email)?.email ?? '' })}
                >
                  Save mapping…
                </Menu.Item>
                <Menu.Item leftSection={<IconFolderPlus size={16} />} onClick={() => onCreateProject(item)}>
                  Create new project…
                </Menu.Item>
                <Menu.Item
                  leftSection={<IconRefresh size={16} />}
                  onClick={() =>
                    reclassify.mutate(m.id, {
                      onSuccess: (d) => notifySuccess(`${d.classification.status}: ${d.classification.reason}`, 'Re-classified'),
                      onError: (e) => notifyError(e),
                    })
                  }
                >
                  Retry classification
                </Menu.Item>
                <Menu.Divider />
                <Menu.Item leftSection={<IconBan size={16} />} color="red" onClick={exclude}>
                  Exclude
                </Menu.Item>
              </Menu.Dropdown>
            </Menu>
          </Group>
        </Group>

        <Text size="sm">
          <Text span fw={600}>Why: </Text>
          {item.reason}
        </Text>
        {item.suggestedNextAction && (
          <Text size="sm" c="dimmed">
            <IconBulb size={14} style={{ verticalAlign: 'middle' }} aria-hidden /> {item.suggestedNextAction}
          </Text>
        )}
        {item.aiSuggestion && (
          <Alert color="violet" variant="light" icon={<IconSparkles size={16} />} title="AI suggestion" p="xs">
            <Text size="sm">{item.aiSuggestion}</Text>
          </Alert>
        )}

        {item.candidates.length > 0 && (
          <div>
            <Text size="xs" c="dimmed" tt="uppercase" fw={600} mb={4}>
              Candidate projects
            </Text>
            <Group gap={6}>
              {item.candidates.map((c) => (
                <Button key={c.id} size="compact-xs" variant="light" onClick={() => onAssign({ item, kind: 'Project', projectId: c.id })}>
                  Assign to {c.name}
                </Button>
              ))}
            </Group>
          </div>
        )}

        <div>
          <Text size="xs" c="dimmed" tt="uppercase" fw={600} mb={4}>
            Participants — click to create a mapping
          </Text>
          <Group gap={6}>
            {external.length === 0 && <Text size="sm" c="dimmed">No participants besides the owner.</Text>}
            {external.map((p, i) => {
              const label = p.email ? `${p.name ?? p.email} <${p.email}>` : p.name ?? '?';
              return (
                <Chip
                  key={i}
                  label={p.name && p.email ? `${p.name} · ${p.email}` : label}
                  title={`Map ${p.email ?? p.name} to a project`}
                  onClick={() =>
                    onMapping({ item, kind: p.email ? 'Email' : 'ParticipantName', value: p.email ?? p.name ?? '' })
                  }
                />
              );
            })}
          </Group>
        </div>
        {item.identifiedSpeakers.length > 0 && (
          <div>
            <Text size="xs" c="dimmed" tt="uppercase" fw={600} mb={4}>
              Identified speakers
            </Text>
            <Group gap={6}>
              {item.identifiedSpeakers.map((s) => (
                <Chip
                  key={s.name}
                  label={`${s.name} (${s.segments})`}
                  title={`Map speaker ${s.name} to a project`}
                  onClick={() => onMapping({ item, kind: 'SpeakerName', value: s.name })}
                />
              ))}
            </Group>
          </div>
        )}
      </Stack>
    </Card>
  );
}

export function UnassignedPage() {
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const q = useUnassigned(page, pageSize);
  const [assign, setAssign] = useState<AssignState | null>(null);
  const [mapping, setMapping] = useState<MappingState | null>(null);
  const [createFor, setCreateFor] = useState<UnassignedItemDto | null>(null);
  const pages = Math.max(1, Math.ceil((q.data?.total ?? 0) / pageSize));

  return (
    <>
      <PageHeader
        title="Unassigned"
        description="Meetings the rules could not place with confidence. Assign them, or save a mapping so similar meetings are routed automatically."
      />
      <QueryState isLoading={q.isLoading} error={q.error} onRetry={() => void q.refetch()} skeleton={<LoadingRows rows={4} height={140} />}>
        {q.data && q.data.items.length === 0 ? (
          <EmptyState title="Nothing to review" description="All meetings are assigned, shared or excluded." />
        ) : (
          <Stack gap="md" style={{ opacity: q.isPlaceholderData ? 0.6 : 1 }}>
            <Text size="sm" c="dimmed">
              {q.data?.total} meeting(s) need attention
            </Text>
            {q.data?.items.map((item) => (
              <UnassignedCard
                key={item.meeting.id}
                item={item}
                onAssign={setAssign}
                onMapping={setMapping}
                onCreateProject={setCreateFor}
              />
            ))}
            {pages > 1 && <Pagination total={pages} value={page} onChange={setPage} />}
          </Stack>
        )}
      </QueryState>

      {assign && (
        <ReassignModal
          opened
          meetingId={assign.item.meeting.id}
          meetingTitle={assign.item.meeting.title}
          initialKind={assign.kind}
          initialProjectId={assign.projectId}
          fromReviewQueue
          onClose={() => setAssign(null)}
        />
      )}
      {mapping && (
        <SaveMappingModal
          opened
          meetingId={mapping.item.meeting.id}
          meetingTitle={mapping.item.meeting.title}
          initialKind={mapping.kind}
          initialValue={mapping.value}
          initialProjectId={mapping.projectId ?? null}
          onClose={() => setMapping(null)}
        />
      )}
      <ProjectFormModal
        opened={!!createFor}
        onClose={() => setCreateFor(null)}
        initialName={createFor?.meeting.title}
        onSaved={(p) => {
          const item = createFor;
          setCreateFor(null);
          if (item) setAssign({ item, kind: 'Project', projectId: p.id });
        }}
      />
    </>
  );
}
