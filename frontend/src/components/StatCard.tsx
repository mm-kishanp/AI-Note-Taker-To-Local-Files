import { Paper, Skeleton, Text, UnstyledButton } from '@mantine/core';
import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';

export interface StatCardProps {
  label: string;
  value: ReactNode;
  hint?: ReactNode;
  to?: string;
  color?: string;
  loading?: boolean;
}

export function StatCard({ label, value, hint, to, color, loading }: StatCardProps) {
  const body = (
    <Paper withBorder p="md" radius="md" h="100%">
      <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
        {label}
      </Text>
      {loading ? (
        <Skeleton height={28} mt={6} width="50%" />
      ) : (
        <Text size="xl" fw={700} c={color} mt={4}>
          {value}
        </Text>
      )}
      {hint && (
        <Text size="xs" c="dimmed" mt={2}>
          {hint}
        </Text>
      )}
    </Paper>
  );
  if (!to) return body;
  return (
    <UnstyledButton component={Link} to={to} aria-label={`${label}: view details`} style={{ display: 'block', height: '100%' }}>
      {body}
    </UnstyledButton>
  );
}
