import { Badge, Drawer, Group, Pagination, Paper, SimpleGrid, Stack, Table, Text } from '@mantine/core';
import { IconHistory } from '@tabler/icons-react';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { EmptyState } from '../components/EmptyState';
import { PageHeader } from '../components/PageHeader';
import { LoadingRows, QueryState } from '../components/QueryState';
import { StatusBadge } from '../components/StatusBadge';
import { SyncNowButton } from '../components/SyncControls';
import { SyncRunsTable } from '../components/SyncRunsTable';
import { useSyncHistory, useSyncRun } from '../hooks/useSystem';
import type { SyncRunDto } from '../types/api';
import { formatDateTime, formatDuration, formatTime, humanize } from '../utils/format';

const PAGE_SIZE = 20;

export function SyncHistoryPage() {
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<SyncRunDto | null>(null);
  const history = useSyncHistory(page, PAGE_SIZE);
  const totalPages = history.data ? Math.max(1, Math.ceil(history.data.total / PAGE_SIZE)) : 1;

  return (
    <>
      <PageHeader
        title="Synchronization history"
        description="Every sync run with its counters. Select a run to see per-meeting outcomes and errors."
        actions={<SyncNowButton />}
      />
      <QueryState isLoading={history.isLoading} error={history.error} onRetry={() => void history.refetch()}>
        {history.data && history.data.items.length === 0 ? (
          <EmptyState icon={<IconHistory size={24} />} title="No sync runs yet" description="Start a sync to import meetings from Granola." />
        ) : (
          <Stack gap="md">
            <Paper withBorder radius="md" p="xs">
              <SyncRunsTable runs={history.data?.items ?? []} onSelect={setSelected} />
            </Paper>
            {totalPages > 1 && (
              <Group justify="center">
                <Pagination value={page} onChange={setPage} total={totalPages} aria-label="Sync history pages" />
              </Group>
            )}
          </Stack>
        )}
      </QueryState>
      <RunDrawer run={selected} onClose={() => setSelected(null)} />
    </>
  );
}

function Counter({ label, value, danger }: { label: string; value: number | string; danger?: boolean }) {
  return (
    <Stack gap={0}>
      <Text size="xs" c="dimmed">
        {label}
      </Text>
      <Text fw={600} c={danger ? 'red' : undefined}>
        {value}
      </Text>
    </Stack>
  );
}

function RunDrawer({ run, onClose }: { run: SyncRunDto | null; onClose: () => void }) {
  const detail = useSyncRun(run?.id ?? null);
  const r = detail.data?.run ?? run;
  return (
    <Drawer opened={!!run} onClose={onClose} position="right" size="xl" title="Sync run details">
      {r && (
        <Stack gap="md">
          <Group gap="xs">
            <StatusBadge kind="sync" value={r.status} size="md" srLabel="Run status" />
            <Badge variant="outline" color="gray" style={{ textTransform: 'none' }}>
              {humanize(r.mode)}
            </Badge>
            <Badge variant="outline" color="gray" style={{ textTransform: 'none' }}>
              {r.trigger}
            </Badge>
          </Group>
          <Text size="sm">
            Started {formatDateTime(r.startedAt)} · {r.finishedAt ? `finished ${formatDateTime(r.finishedAt)} · ${formatDuration(r.durationMs)}` : 'still running'}
          </Text>
          {r.error && (
            <Text size="sm" c="red" role="alert">
              {r.error}
            </Text>
          )}
          <SimpleGrid cols={{ base: 2, sm: 4 }}>
            <Counter label="Discovered" value={r.notesDiscovered} />
            <Counter label="Processed" value={r.meetingsProcessed} />
            <Counter label="Skipped (unchanged)" value={r.meetingsSkipped} />
            <Counter label="Files updated" value={r.filesUpdated} />
            <Counter label="Notes exported" value={r.notesExported} />
            <Counter label="Transcripts exported" value={r.transcriptsExported} />
            <Counter label="Conflicts" value={r.conflicts} danger={r.conflicts > 0} />
            <Counter label="API failures" value={r.apiFailures} danger={r.apiFailures > 0} />
            <Counter label="File-system failures" value={r.fileSystemFailures} danger={r.fileSystemFailures > 0} />
            <Counter label="Classification failures" value={r.classificationFailures} danger={r.classificationFailures > 0} />
          </SimpleGrid>

          <Text fw={600}>Meeting outcomes</Text>
          <Text size="xs" c="dimmed">
            Unchanged meetings are not listed.
          </Text>
          {detail.isLoading ? (
            <LoadingRows rows={4} />
          ) : (detail.data?.items.length ?? 0) === 0 ? (
            <Text size="sm" c="dimmed">
              No per-meeting changes recorded for this run.
            </Text>
          ) : (
            <Table.ScrollContainer minWidth={600}>
              <Table verticalSpacing="xs" striped>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Time</Table.Th>
                    <Table.Th>Note</Table.Th>
                    <Table.Th>Outcome</Table.Th>
                    <Table.Th>Category</Table.Th>
                    <Table.Th>Message</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {detail.data!.items.map((i) => (
                    <Table.Tr key={i.id}>
                      <Table.Td style={{ whiteSpace: 'nowrap' }}>{formatTime(i.timestamp)}</Table.Td>
                      <Table.Td>
                        {i.meetingId ? (
                          <Link to={`/meetings/${i.meetingId}`} onClick={onClose}>
                            {i.granolaNoteId ?? 'meeting'}
                          </Link>
                        ) : (
                          <Text size="sm" ff="monospace">
                            {i.granolaNoteId}
                          </Text>
                        )}
                      </Table.Td>
                      <Table.Td>{humanize(i.outcome)}</Table.Td>
                      <Table.Td>
                        {i.errorCategory !== 'None' && (
                          <Text size="sm" c="red">
                            {humanize(i.errorCategory)}
                            {i.retryable ? ' (will retry)' : ''}
                          </Text>
                        )}
                      </Table.Td>
                      <Table.Td maw={320}>
                        <Text size="xs" lineClamp={3} title={i.message ?? undefined}>
                          {i.message}
                        </Text>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          )}
        </Stack>
      )}
    </Drawer>
  );
}
