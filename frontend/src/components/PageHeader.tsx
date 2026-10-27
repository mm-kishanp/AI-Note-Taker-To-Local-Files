import { Group, Stack, Text, Title } from '@mantine/core';
import type { ReactNode } from 'react';
import { useEffect } from 'react';

export interface PageHeaderProps {
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  /** Plain-text title for the document <title>. */
  docTitle?: string;
}

export function PageHeader({ title, description, actions, docTitle }: PageHeaderProps) {
  useEffect(() => {
    const t = docTitle ?? (typeof title === 'string' ? title : undefined);
    if (t) document.title = `${t} · Granola PI Sync`;
  }, [docTitle, title]);

  return (
    <Group justify="space-between" align="flex-start" mb="lg" wrap="wrap" gap="sm">
      <Stack gap={4} style={{ minWidth: 0, flex: 1 }}>
        <Title order={2}>{title}</Title>
        {description && (
          <Text c="dimmed" size="sm">
            {description}
          </Text>
        )}
      </Stack>
      {actions && (
        <Group gap="xs" wrap="wrap">
          {actions}
        </Group>
      )}
    </Group>
  );
}
