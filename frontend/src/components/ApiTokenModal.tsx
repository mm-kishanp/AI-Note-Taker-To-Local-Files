import { Alert, Button, Group, Modal, PasswordInput, Stack, Text } from '@mantine/core';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { AUTH_REQUIRED_EVENT, setApiToken } from '../services/api';

/**
 * Shown when the server answers 401 ("A valid API token is required."). This only happens when the backend is
 * deployed remotely with Security:ApiToken configured; local (loopback) use never needs a token.
 * The token is kept in sessionStorage (cleared when the browser session ends) and every query is refetched.
 */
export function ApiTokenModal() {
  const qc = useQueryClient();
  const [opened, setOpened] = useState(false);
  const [hadToken, setHadToken] = useState(false);
  const [token, setToken] = useState('');

  useEffect(() => {
    const handler = (e: Event) => {
      const detail = (e as CustomEvent<{ hadToken?: boolean }>).detail;
      setHadToken(!!detail?.hadToken);
      setOpened(true);
    };
    window.addEventListener(AUTH_REQUIRED_EVENT, handler);
    return () => window.removeEventListener(AUTH_REQUIRED_EVENT, handler);
  }, []);

  const save = () => {
    if (!token.trim()) return;
    setApiToken(token);
    setToken('');
    setOpened(false);
    void qc.invalidateQueries();
  };

  return (
    <Modal opened={opened} onClose={() => setOpened(false)} title="API token required" centered>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          save();
        }}
      >
        <Stack gap="sm">
          <Text size="sm">
            This server requires an API token for remote access. Enter the token configured on the server
            (<code>Security:ApiToken</code>). It is kept only for this browser session.
          </Text>
          {hadToken && (
            <Alert color="red" variant="light" role="alert">
              The stored token was rejected. Check it and try again.
            </Alert>
          )}
          <PasswordInput
            label="API token"
            value={token}
            onChange={(e) => setToken(e.currentTarget.value)}
            autoComplete="off"
            data-autofocus
            required
          />
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setOpened(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={!token.trim()}>
              Save and retry
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
