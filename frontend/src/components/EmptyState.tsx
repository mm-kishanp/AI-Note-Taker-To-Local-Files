import { Paper, Stack, Text, ThemeIcon } from '@mantine/core';
import { IconInbox } from '@tabler/icons-react';
import type { ReactNode } from 'react';

export interface EmptyStateProps {
  title: string;
  description?: ReactNode;
  icon?: ReactNode;
  action?: ReactNode;
}

export function EmptyState({ title, description, icon, action }: EmptyStateProps) {
  return (
    <Paper withBorder p="xl" radius="md" role="status">
      <Stack align="center" gap="xs">
        <ThemeIcon size={44} radius="xl" variant="light" color="gray" aria-hidden>
          {icon ?? <IconInbox size={24} />}
        </ThemeIcon>
        <Text fw={600}>{title}</Text>
        {description && (
          <Text c="dimmed" size="sm" ta="center" maw={520}>
            {description}
          </Text>
        )}
        {action}
      </Stack>
    </Paper>
  );
}
