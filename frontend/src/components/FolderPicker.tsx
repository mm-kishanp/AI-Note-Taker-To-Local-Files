import {
  Alert, Breadcrumbs, Button, Checkbox, Group, List, Modal, NavLink, ScrollArea, Stack, Text, TextInput,
} from '@mantine/core';
import { IconArrowUp, IconCheck, IconFolder, IconHome, IconX } from '@tabler/icons-react';
import { useEffect, useState } from 'react';
import { useDirectories, useValidatePath } from '../hooks/useExports';
import type { ValidatePathResultDto } from '../types/api';
import { formatBytes } from '../utils/format';
import { ProblemAlert } from './ProblemAlert';
import { LoadingRows } from './QueryState';

export interface FolderPickerProps {
  opened: boolean;
  onClose: () => void;
  onSelect: (path: string) => void;
  initialPath?: string | null;
  title?: string;
}

/** Folder picker backed by GET /api/exports/directories + POST /api/exports/validate-path. */
export function FolderPicker({ opened, onClose, onSelect, initialPath, title = 'Choose a folder' }: FolderPickerProps) {
  const [current, setCurrent] = useState<string | null>(initialPath || null);
  const [typed, setTyped] = useState(initialPath ?? '');
  const [createIfMissing, setCreateIfMissing] = useState(false);
  const [result, setResult] = useState<ValidatePathResultDto | null>(null);
  const listing = useDirectories(current, opened);
  const validate = useValidatePath();

  useEffect(() => {
    if (opened) {
      setCurrent(initialPath || null);
      setTyped(initialPath ?? '');
      setResult(null);
      validate.reset();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [opened, initialPath]);

  const navigate = (path: string | null) => {
    setCurrent(path);
    setTyped(path ?? '');
    setResult(null);
  };

  const runValidate = () => {
    if (!typed.trim()) return;
    validate.mutate({ path: typed.trim(), createIfMissing }, { onSuccess: setResult });
  };

  const segments = (listing.data?.path ?? '').split(/[\\/]/).filter(Boolean);

  return (
    <Modal opened={opened} onClose={onClose} title={title} size="lg">
      <Stack gap="sm">
        <Group gap="xs" align="flex-end" wrap="nowrap">
          <TextInput
            label="Folder path"
            value={typed}
            onChange={(e) => {
              setTyped(e.currentTarget.value);
              setResult(null);
            }}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                navigate(typed.trim() || null);
              }
            }}
            style={{ flex: 1 }}
            ff="monospace"
            description="Type a path and press Enter to browse it, or pick a sub-folder below."
          />
        </Group>
        <Group gap="xs">
          <Button size="xs" variant="default" leftSection={<IconHome size={14} />} onClick={() => navigate(null)}>
            Drives
          </Button>
          <Button
            size="xs"
            variant="default"
            leftSection={<IconArrowUp size={14} />}
            disabled={!listing.data?.path}
            onClick={() => navigate(listing.data?.parent ?? null)}
          >
            Up
          </Button>
          {segments.length > 0 && (
            <Breadcrumbs separator="›" style={{ flexWrap: 'wrap' }}>
              {segments.map((s, i) => (
                <Text key={i} size="xs" ff="monospace">
                  {s}
                </Text>
              ))}
            </Breadcrumbs>
          )}
        </Group>
        <ScrollArea h={260} type="auto" style={{ border: '1px solid var(--mantine-color-default-border)', borderRadius: 4 }}>
          {listing.isLoading ? (
            <LoadingRows rows={6} height={22} />
          ) : listing.error ? (
            <ProblemAlert error={listing.error} />
          ) : (listing.data?.directories.length ?? 0) === 0 ? (
            <Text size="sm" c="dimmed" p="sm">
              No sub-folders.
            </Text>
          ) : (
            <nav aria-label="Sub-folders">
              {listing.data!.directories.map((d) => (
                <NavLink
                  key={d.fullPath}
                  label={d.name}
                  leftSection={<IconFolder size={16} />}
                  onClick={() => navigate(d.fullPath)}
                  aria-label={`Open folder ${d.name}`}
                />
              ))}
            </nav>
          )}
        </ScrollArea>
        <Checkbox
          label="Create the folder if it does not exist"
          checked={createIfMissing}
          onChange={(e) => setCreateIfMissing(e.currentTarget.checked)}
        />
        {validate.error && <ProblemAlert error={validate.error} />}
        {result && (
          <Alert
            color={result.valid ? 'green' : 'red'}
            icon={result.valid ? <IconCheck size={16} /> : <IconX size={16} />}
            title={result.valid ? 'Folder is usable' : 'Folder cannot be used'}
            role="status"
          >
            <Text size="sm" ff="monospace" style={{ wordBreak: 'break-all' }}>
              {result.fullPath}
            </Text>
            <List size="sm" mt={4}>
              <List.Item>{result.exists ? 'Exists' : 'Does not exist yet'}</List.Item>
              <List.Item>{result.writable ? 'Writable' : 'Not writable'}</List.Item>
              {result.freeBytes !== null && <List.Item>Free space: {formatBytes(result.freeBytes)}</List.Item>}
              {result.messages.map((m, i) => (
                <List.Item key={i}>{m}</List.Item>
              ))}
            </List>
          </Alert>
        )}
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="light" onClick={runValidate} loading={validate.isPending} disabled={!typed.trim()}>
            Validate
          </Button>
          <Button
            disabled={!result?.valid}
            onClick={() => {
              if (result?.valid) {
                onSelect(result.fullPath);
                onClose();
              }
            }}
          >
            Use this folder
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
