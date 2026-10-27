import { Button, Card, Group, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { IconFolderOpen, IconInbox } from '@tabler/icons-react';
import { Link, useNavigate } from 'react-router-dom';
import { EmptyState } from '../components/EmptyState';
import { MeetingsTable } from '../components/MeetingsTable';
import { PageHeader } from '../components/PageHeader';
import { QueryState } from '../components/QueryState';
import { StatCard } from '../components/StatCard';
import { SyncNowButton } from '../components/SyncControls';
import { SyncRunsTable } from '../components/SyncRunsTable';
import { useOpenExportPath } from '../hooks/useExports';
import { useDashboard } from '../hooks/useSystem';
import { formatDateTime, formatRelative } from '../utils/format';
import { notifyError, notifySuccess } from '../utils/notify';

export function DashboardPage() {
  const { data, isLoading, error, refetch } = useDashboard();
  const openFolder = useOpenExportPath();
  const navigate = useNavigate();
  const d = data;

  return (
    <>
      <PageHeader
        title="Dashboard"
        description="Overview of synchronized Granola meetings and your project knowledge base."
        actions={
          <>
            <SyncNowButton />
            <Button component={Link} to="/unassigned" variant="light" leftSection={<IconInbox size={16} />}>
              View unassigned
            </Button>
            <Button
              variant="default"
              leftSection={<IconFolderOpen size={16} />}
              loading={openFolder.isPending}
              onClick={() =>
                openFolder.mutate(
                  { root: 'root' },
                  { onSuccess: () => notifySuccess('Export folder opened.'), onError: (e) => notifyError(e, 'Could not open folder') },
                )
              }
            >
              Open export folder
            </Button>
          </>
        }
      />
      <QueryState isLoading={false} error={error} onRetry={() => void refetch()}>
        <Stack gap="lg">
          <SimpleGrid cols={{ base: 2, sm: 3, lg: 6 }} spacing="sm">
            <StatCard loading={isLoading} label="Total meetings" value={d?.totalMeetings} to="/meetings" />
            <StatCard loading={isLoading} label="Exported" value={d?.meetingsExported} hint="meetings with files" to="/meetings?exportStatus=Exported" />
            <StatCard loading={isLoading} label="Notes exported" value={d?.notesExported} />
            <StatCard loading={isLoading} label="Transcripts exported" value={d?.transcriptsExported} />
            <StatCard loading={isLoading} label="Projects" value={d?.projectsConfigured} to="/projects" />
            <StatCard
              loading={isLoading}
              label="Unassigned"
              value={d?.unassignedMeetings}
              color={d && d.unassignedMeetings > 0 ? 'yellow.7' : undefined}
              to="/unassigned"
            />
            <StatCard
              loading={isLoading}
              label="Needs review"
              value={d?.needsReview}
              color={d && d.needsReview > 0 ? 'yellow.7' : undefined}
              to="/meetings?needsReview=true"
            />
            <StatCard
              loading={isLoading}
              label="Failed exports"
              value={d?.failedExports}
              color={d && d.failedExports > 0 ? 'red' : undefined}
              to="/meetings?exportStatus=Failed"
            />
            <StatCard
              loading={isLoading}
              label="Conflicts"
              value={d?.conflicts}
              color={d && d.conflicts > 0 ? 'red' : undefined}
              to="/meetings?exportStatus=Conflict"
            />
            <StatCard
              loading={isLoading}
              label="Last successful sync"
              value={<Text size="md" fw={700}>{formatRelative(d?.lastSuccessfulSync)}</Text>}
              hint={formatDateTime(d?.lastSuccessfulSync, 'No successful sync yet')}
              to="/sync-history"
            />
            <StatCard
              loading={isLoading}
              label="Next scheduled sync"
              value={
                <Text size="md" fw={700}>
                  {d?.autoSyncEnabled ? (d.nextScheduledSync ? formatRelative(d.nextScheduledSync) : 'Pending') : 'Auto-sync off'}
                </Text>
              }
              hint={d?.autoSyncEnabled ? formatDateTime(d.nextScheduledSync) : 'Enable in Settings → Synchronization'}
              to="/settings#sync"
            />
            <StatCard
              loading={isLoading}
              label="Granola key"
              value={<Text size="md" fw={700} c={d && !d.credentialsConfigured ? 'red' : undefined}>{d?.credentialsConfigured ? 'Configured' : 'Missing'}</Text>}
              to="/settings#granola"
            />
          </SimpleGrid>

          <Card withBorder radius="md" p="md">
            <Group justify="space-between" mb="sm">
              <Title order={4}>Recent meetings</Title>
              <Button component={Link} to="/meetings" variant="subtle" size="xs">
                All meetings
              </Button>
            </Group>
            <QueryState isLoading={isLoading} error={null}>
              {d && d.recentMeetings.length > 0 ? (
                <MeetingsTable items={d.recentMeetings} compact />
              ) : (
                <EmptyState title="No meetings yet" description="Run a sync to import your Granola meetings." action={<SyncNowButton size="xs" />} />
              )}
            </QueryState>
          </Card>

          <Card withBorder radius="md" p="md">
            <Group justify="space-between" mb="sm">
              <Title order={4}>Recent sync runs</Title>
              <Button component={Link} to="/sync-history" variant="subtle" size="xs">
                Full history
              </Button>
            </Group>
            <QueryState isLoading={isLoading} error={null}>
              {d && d.recentRuns.length > 0 ? (
                <SyncRunsTable runs={d.recentRuns} compact onSelect={(r) => navigate(`/sync-history?run=${r.id}`)} />
              ) : (
                <EmptyState title="No sync runs yet" />
              )}
            </QueryState>
          </Card>
        </Stack>
      </QueryState>
    </>
  );
}
