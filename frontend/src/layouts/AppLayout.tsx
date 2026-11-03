import {
  ActionIcon, Alert, AppShell, Badge, Burger, Button, Group, NavLink, ScrollArea, Text, Tooltip, useComputedColorScheme,
  useMantineColorScheme,
} from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import {
  IconAlertTriangle, IconFolders, IconHistory, IconInbox, IconLayoutDashboard, IconMoon, IconNotes, IconSettings, IconSun,
  IconTags,
} from '@tabler/icons-react';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { Link, Outlet, useLocation } from 'react-router-dom';
import { ApiTokenModal } from '../components/ApiTokenModal';
import { SyncNowButton, SyncStatusChip } from '../components/SyncControls';
import { qk } from '../hooks/queryKeys';
import { useDashboard, useHealth, useSyncStatus } from '../hooks/useSystem';

const NAV = [
  { to: '/', label: 'Dashboard', icon: IconLayoutDashboard, exact: true },
  { to: '/meetings', label: 'Meetings', icon: IconNotes },
  { to: '/projects', label: 'Projects & clients', icon: IconTags },
  { to: '/unassigned', label: 'Unassigned', icon: IconInbox, badge: 'unassigned' as const },
  { to: '/sync-history', label: 'Sync history', icon: IconHistory },
  { to: '/exports', label: 'Export browser', icon: IconFolders },
  { to: '/settings', label: 'Settings', icon: IconSettings },
];

/** Refreshes data views when a background run finishes. */
function SyncWatcher() {
  const qc = useQueryClient();
  const { data } = useSyncStatus();
  const wasRunning = useRef<boolean | null>(null);
  useEffect(() => {
    if (!data) return;
    if (wasRunning.current && !data.isRunning) {
      for (const key of [qk.meetings, qk.unassigned, qk.dashboard, qk.projects, qk.syncHistory, qk.exports, qk.health]) {
        void qc.invalidateQueries({ queryKey: key });
      }
    }
    wasRunning.current = data.isRunning;
  }, [data, qc]);
  return null;
}

function ColorSchemeToggle() {
  const { setColorScheme } = useMantineColorScheme();
  const computed = useComputedColorScheme('light', { getInitialValueInEffect: true });
  const next = computed === 'dark' ? 'light' : 'dark';
  return (
    <Tooltip label={`Switch to ${next} mode`}>
      <ActionIcon variant="default" size="lg" onClick={() => setColorScheme(next)} aria-label={`Switch to ${next} color scheme`}>
        {computed === 'dark' ? <IconSun size={18} /> : <IconMoon size={18} />}
      </ActionIcon>
    </Tooltip>
  );
}

export function AppLayout() {
  const [opened, { toggle, close }] = useDisclosure();
  const location = useLocation();
  const health = useHealth();
  const dashboard = useDashboard();
  const unassigned = dashboard.data?.unassignedMeetings ?? 0;

  useEffect(() => close(), [location.pathname, close]);

  const isActive = (to: string, exact?: boolean) => (exact ? location.pathname === to : location.pathname === to || location.pathname.startsWith(to + '/'));

  return (
    <AppShell header={{ height: 60 }} navbar={{ width: 240, breakpoint: 'sm', collapsed: { mobile: !opened } }} padding="md">
      <a href="#main-content" className="gpi-skip-link">
        Skip to content
      </a>
      <SyncWatcher />
      <ApiTokenModal />
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between" wrap="nowrap">
          <Group gap="sm" wrap="nowrap">
            <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" aria-label="Toggle navigation" />
            <Text fw={700} size="lg" component={Link} to="/" style={{ textDecoration: 'none', color: 'inherit', whiteSpace: 'nowrap' }}>
              CHRONIQ
            </Text>
          </Group>
          <Group gap="sm" wrap="nowrap">
            <SyncStatusChip />
            <SyncNowButton />
            <ColorSchemeToggle />
          </Group>
        </Group>
      </AppShell.Header>
      <AppShell.Navbar p="xs">
        <AppShell.Section grow component={ScrollArea}>
          <nav aria-label="Main navigation">
            {NAV.map((item) => (
              <NavLink
                key={item.to}
                component={Link}
                to={item.to}
                label={item.label}
                leftSection={<item.icon size={18} aria-hidden />}
                active={isActive(item.to, item.exact)}
                aria-current={isActive(item.to, item.exact) ? 'page' : undefined}
                rightSection={
                  item.badge === 'unassigned' && unassigned > 0 ? (
                    <Badge size="sm" color="yellow" variant="filled" aria-label={`${unassigned} unassigned meetings`}>
                      {unassigned}
                    </Badge>
                  ) : undefined
                }
              />
            ))}
          </nav>
        </AppShell.Section>
        <AppShell.Section>
          <Text size="xs" c="dimmed" px="sm" py={4}>
            {health.data ? `Backend v${health.data.version} · ${health.data.status}` : health.error ? 'Backend unreachable' : ' '}
          </Text>
        </AppShell.Section>
      </AppShell.Navbar>
      <AppShell.Main id="main-content" tabIndex={-1}>
        {health.data && !health.data.credentialsConfigured && (
          <Alert
            color="orange"
            icon={<IconAlertTriangle size={18} />}
            title="Granola API key not configured"
            mb="md"
            role="alert"
          >
            <Group justify="space-between" wrap="wrap" gap="xs">
              <Text size="sm">Synchronization cannot fetch meetings until you add your Granola API key.</Text>
              <Button component={Link} to="/settings#granola" size="xs" variant="light" color="orange">
                Open Settings
              </Button>
            </Group>
          </Alert>
        )}
        {health.error && (
          <Alert color="red" icon={<IconAlertTriangle size={18} />} title="Cannot reach the backend" mb="md" role="alert">
            The CHRONIQ service is not responding. Data shown may be out of date.
          </Alert>
        )}
        <Outlet />
      </AppShell.Main>
    </AppShell>
  );
}
