import { Skeleton, Stack } from '@mantine/core';
import type { ReactNode } from 'react';
import { ProblemAlert } from './ProblemAlert';

export function LoadingRows({ rows = 5, height = 28 }: { rows?: number; height?: number }) {
  return (
    <Stack gap="xs" aria-busy="true" aria-label="Loading">
      {Array.from({ length: rows }).map((_, i) => (
        <Skeleton key={i} height={height} radius="sm" />
      ))}
    </Stack>
  );
}

export interface QueryStateProps {
  isLoading: boolean;
  error: unknown;
  onRetry?: () => void;
  skeleton?: ReactNode;
  children: ReactNode;
}

/** Loading skeleton / error alert / content switch for a query. */
export function QueryState({ isLoading, error, onRetry, skeleton, children }: QueryStateProps) {
  if (isLoading) return <>{skeleton ?? <LoadingRows />}</>;
  if (error) return <ProblemAlert error={error} onRetry={onRetry} />;
  return <>{children}</>;
}
