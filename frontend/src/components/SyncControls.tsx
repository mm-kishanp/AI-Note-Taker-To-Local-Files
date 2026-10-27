import { Badge, Button, Group, Loader, Menu, Progress, Text, Tooltip } from '@mantine/core';
import { IconChevronDown, IconPlayerStop, IconRefresh } from '@tabler/icons-react';
import { useCancelSync, useStartSync, useSyncStatus } from '../hooks/useSystem';
import { ApiError } from '../services/api';
import type { SyncMode } from '../types/api';
import { formatRelative } from '../utils/format';
import { notifyError, notifyInfo, notifySuccess } from '../utils/notify';
import { statusColor, statusLabel } from '../utils/status';

export const SYNC_MODE_OPTIONS: { mode: SyncMode; label: string; description: string }[] = [
  { mode: 'Incremental', label: 'Incremental sync', description: 'Fetch new and changed meetings since the last checkpoint.' },
  { mode: 'Full', label: 'Full sync', description: 'Re-list every meeting from Granola and refresh changed ones.' },
  { mode: 'RetryFailed', label: 'Retry failed', description: 'Retry meetings whose last fetch or export failed.' },
  { mode: 'Reconcile', label: 'Reconcile files', description: 'Check exported files on disk for missing or edited files.' },
];

/** Compact live status chip shown in the header. */
export function SyncStatusChip() {
  const { data, error } = useSyncStatus();
  if (error) {
    return (
      <Badge color="red" variant="light" style={{ textTransform: 'none' }} role="status">
        Backend unreachable
      </Badge>
    );
  }
  if (!data) return <Loader size="xs" aria-label="Loading sync status" />;
  if (data.isRunning) {
    const total = Math.max(data.queued, data.discovered, data.processed, 1);
    const pct = Math.min(100, Math.round((data.processed / total) * 100));
    return (
      <Tooltip label={data.currentItem ? `Current: ${data.currentItem}` : data.phase} withArrow>
        <Group gap={6} wrap="nowrap" role="status" aria-live="polite" aria-label={`Sync running: ${data.phase}, ${data.processed} of ${total} processed`}>
          <Loader size={14} />
          <Text size="sm" fw={500}>
            Syncing · {data.phase}
          </Text>
          <Progress value={pct} w={80} size="sm" aria-hidden />
          <Text size="xs" c="dimmed">
            {data.processed}/{total}
            {data.failed > 0 ? ` · ${data.failed} failed` : ''}
          </Text>
        </Group>
      </Tooltip>
    );
  }
  return (
    <Group gap={6} wrap="nowrap" role="status" aria-live="polite">
      <Badge variant="dot" color="gray" style={{ textTransform: 'none' }}>
        Idle
      </Badge>
      {data.lastRunStatus && (
        <Text size="xs" c="dimmed" visibleFrom="md">
          Last run{' '}
          <Text span c={statusColor('sync', data.lastRunStatus)} fw={600} size="xs">
            {statusLabel(data.lastRunStatus)}
          </Text>{' '}
          {formatRelative(data.lastRunFinishedAt)}
        </Text>
      )}
    </Group>
  );
}

/** "Sync now" split button with a mode menu; Incremental is the default. Shows Cancel while running. */
export function SyncNowButton({ size = 'sm' }: { size?: 'xs' | 'sm' | 'md' }) {
  const status = useSyncStatus();
  const start = useStartSync();
  const cancel = useCancelSync();
  const running = status.data?.isRunning ?? false;

  const run = (mode: SyncMode) =>
    start.mutate(
      { mode },
      {
        onSuccess: (r) => notifySuccess(r.message || `${mode} sync started`, 'Sync started'),
        onError: (e) => {
          if (e instanceof ApiError && e.status === 409) notifyInfo(e.detail ?? 'A synchronization is already running.', 'Already running');
          else notifyError(e, 'Could not start sync');
        },
      },
    );

  if (running) {
    return (
      <Button
        size={size}
        color="red"
        variant="light"
        leftSection={<IconPlayerStop size={16} />}
        loading={cancel.isPending}
        onClick={() =>
          cancel.mutate(undefined, {
            onSuccess: () => notifyInfo('Cancellation requested.'),
            onError: (e) => notifyError(e),
          })
        }
      >
        Cancel sync
      </Button>
    );
  }

  return (
    <Group gap={0} wrap="nowrap">
      <Button
        size={size}
        leftSection={<IconRefresh size={16} />}
        loading={start.isPending}
        onClick={() => run('Incremental')}
        style={{ borderTopRightRadius: 0, borderBottomRightRadius: 0 }}
      >
        Sync now
      </Button>
      <Menu position="bottom-end" withinPortal>
        <Menu.Target>
          <Button
            size={size}
            px={8}
            aria-label="Choose sync mode"
            style={{ borderTopLeftRadius: 0, borderBottomLeftRadius: 0, borderLeft: '1px solid rgba(255,255,255,0.35)' }}
            disabled={start.isPending}
          >
            <IconChevronDown size={16} />
          </Button>
        </Menu.Target>
        <Menu.Dropdown>
          <Menu.Label>Sync mode</Menu.Label>
          {SYNC_MODE_OPTIONS.map((o) => (
            <Menu.Item key={o.mode} onClick={() => run(o.mode)}>
              <Text size="sm" fw={500}>
                {o.label}
              </Text>
              <Text size="xs" c="dimmed" maw={260}>
                {o.description}
              </Text>
            </Menu.Item>
          ))}
        </Menu.Dropdown>
      </Menu>
    </Group>
  );
}
