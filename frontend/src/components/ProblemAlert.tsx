import { Alert, Button, Code, List, Stack, Text } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
import { ApiError } from '../services/api';

export interface ProblemAlertProps {
  error: unknown;
  title?: string;
  onRetry?: () => void;
  /** Hide per-field errors (when they are rendered inline next to inputs). */
  hideFieldErrors?: boolean;
}

/** Renders an API problem (RFC 7807) or any error in an accessible alert. */
export function ProblemAlert({ error, title, onRetry, hideFieldErrors }: ProblemAlertProps) {
  if (!error) return null;
  const apiErr = error instanceof ApiError ? error : null;
  const heading = title ?? apiErr?.title ?? 'Something went wrong';
  const detail = apiErr ? apiErr.detail : error instanceof Error ? error.message : String(error);
  const fieldErrors = apiErr && !hideFieldErrors ? apiErr.errors : [];

  return (
    <Alert color="red" variant="light" title={heading} icon={<IconAlertTriangle size={18} />} role="alert">
      <Stack gap={6}>
        {detail && detail !== heading && <Text size="sm">{detail}</Text>}
        {fieldErrors.length > 0 && (
          <List size="sm" spacing={2}>
            {fieldErrors.map((e, i) => (
              <List.Item key={i}>
                {e.field && <Code>{e.field}</Code>} {e.message}
              </List.Item>
            ))}
          </List>
        )}
        {(apiErr?.category || apiErr?.correlationId) && (
          <Text size="xs" c="dimmed">
            {apiErr?.status ? `HTTP ${apiErr.status}` : null}
            {apiErr?.category ? ` · ${apiErr.category}` : null}
            {apiErr?.correlationId ? ` · correlation id ${apiErr.correlationId}` : null}
          </Text>
        )}
        {onRetry && (
          <div>
            <Button size="xs" variant="light" color="red" onClick={onRetry}>
              Try again
            </Button>
          </div>
        )}
      </Stack>
    </Alert>
  );
}
