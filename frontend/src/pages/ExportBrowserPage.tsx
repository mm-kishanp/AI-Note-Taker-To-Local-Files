import {
  Alert, Badge, Button, Grid, Group, NavLink, Paper, ScrollArea, SegmentedControl, Stack, Tabs, Text,
} from '@mantine/core';
import { IconExternalLink, IconFileText, IconFolder, IconFolderOpen } from '@tabler/icons-react';
import { useState } from 'react';
import { EmptyState } from '../components/EmptyState';
import { Markdown } from '../components/Markdown';
import { PageHeader } from '../components/PageHeader';
import { PathText } from '../components/PathText';
import { LoadingRows, QueryState } from '../components/QueryState';
import { useExportFile, useExportTree, useOpenExportPath } from '../hooks/useExports';
import type { ExportRootKind, ExportTreeNodeDto } from '../types/api';
import { formatBytes, formatDateTime, formatRelative } from '../utils/format';
import { notifyError } from '../utils/notify';

/** Strips YAML front matter for the rendered view (it is shown separately). */
function splitFrontMatter(content: string): { frontMatter: string | null; body: string } {
  const text = content.replace(/\r\n/g, '\n');
  if (!text.startsWith('---\n')) return { frontMatter: null, body: content };
  const end = text.indexOf('\n---\n', 3);
  if (end < 0) return { frontMatter: null, body: content };
  return { frontMatter: text.slice(4, end), body: text.slice(end + 5) };
}

function TreeNode({
  node,
  depth,
  selected,
  onSelect,
}: {
  node: ExportTreeNodeDto;
  depth: number;
  selected: string | null;
  onSelect: (path: string) => void;
}) {
  if (!node.isDirectory) {
    return (
      <NavLink
        label={node.name}
        description={`${formatBytes(node.size)} · ${formatRelative(node.lastModified)}`}
        leftSection={<IconFileText size={16} />}
        active={selected === node.relativePath}
        onClick={() => onSelect(node.relativePath)}
        pl={8 + depth * 14}
        aria-label={`Open file ${node.name}`}
      />
    );
  }
  return (
    <NavLink
      label={node.name || 'Export root'}
      leftSection={<IconFolder size={16} />}
      rightSection={
        <Badge size="xs" variant="light" color="gray">
          {node.fileCount}
        </Badge>
      }
      description={node.lastModified ? `updated ${formatRelative(node.lastModified)}` : undefined}
      defaultOpened={depth < 2}
      childrenOffset={0}
      pl={8 + depth * 14}
      aria-label={`Folder ${node.name || 'export root'}, ${node.fileCount} files`}
    >
      {node.children.map((c) => (
        <TreeNode key={c.relativePath || c.name} node={c} depth={depth + 1} selected={selected} onSelect={onSelect} />
      ))}
    </NavLink>
  );
}

function FileViewer({ root, path }: { root: ExportRootKind; path: string }) {
  const file = useExportFile(root, path);
  const open = useOpenExportPath();
  const [mode, setMode] = useState<'rendered' | 'raw'>('rendered');
  const parts = file.data ? splitFrontMatter(file.data.content) : null;

  return (
    <Stack gap="sm">
      <Group justify="space-between" wrap="wrap">
        <PathText path={path} />
        <Group gap="xs">
          <SegmentedControl
            size="xs"
            value={mode}
            onChange={(v) => setMode(v as 'rendered' | 'raw')}
            data={[
              { label: 'Rendered', value: 'rendered' },
              { label: 'Raw', value: 'raw' },
            ]}
            aria-label="View mode"
          />
          <Button
            size="xs"
            variant="light"
            leftSection={<IconExternalLink size={14} />}
            loading={open.isPending}
            onClick={() => open.mutate({ root, path }, { onError: (e) => notifyError(e, 'Could not open the file') })}
          >
            Open in editor
          </Button>
        </Group>
      </Group>
      <QueryState isLoading={file.isLoading} error={file.error}>
        {file.data && parts && (
          <>
            <Text size="xs" c="dimmed">
              {formatBytes(file.data.size)} · modified {formatDateTime(file.data.lastModified)}
            </Text>
            {mode === 'raw' ? (
              <Paper withBorder p="sm" radius="sm">
                <Text component="pre" ff="monospace" size="xs" style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word', margin: 0 }}>
                  {file.data.content}
                </Text>
              </Paper>
            ) : (
              <>
                {parts.frontMatter && (
                  <Paper withBorder p="xs" radius="sm" bg="var(--mantine-color-default-hover)">
                    <Text component="pre" ff="monospace" size="xs" style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word', margin: 0 }}>
                      {parts.frontMatter}
                    </Text>
                  </Paper>
                )}
                <Markdown content={parts.body} />
              </>
            )}
          </>
        )}
      </QueryState>
    </Stack>
  );
}

function RootPanel({ root }: { root: ExportRootKind }) {
  const tree = useExportTree(root);
  const open = useOpenExportPath();
  const [selected, setSelected] = useState<string | null>(null);

  return (
    <QueryState isLoading={tree.isLoading} error={tree.error} onRetry={() => void tree.refetch()} skeleton={<LoadingRows rows={8} />}>
      {tree.data && (
        <Stack gap="sm">
          <Group justify="space-between" wrap="wrap">
            <PathText path={tree.data.root} />
            <Button
              size="xs"
              variant="default"
              leftSection={<IconFolderOpen size={14} />}
              disabled={!tree.data.exists}
              loading={open.isPending}
              onClick={() => open.mutate({ root }, { onError: (e) => notifyError(e, 'Could not open the folder') })}
            >
              Open folder
            </Button>
          </Group>
          {tree.data.warning && (
            <Alert color={tree.data.exists ? 'yellow' : 'blue'} role="status">
              {tree.data.warning}
            </Alert>
          )}
          {tree.data.exists && tree.data.tree && (
            tree.data.tree.fileCount === 0 ? (
              <EmptyState title="No exported files yet" description="Run a sync to export meetings into this folder." />
            ) : (
              <Grid gutter="md">
                <Grid.Col span={{ base: 12, md: 4 }}>
                  <Paper withBorder radius="md">
                    <ScrollArea h={620} type="auto">
                      <nav aria-label="Exported files">
                        <TreeNode node={tree.data.tree} depth={0} selected={selected} onSelect={setSelected} />
                      </nav>
                    </ScrollArea>
                  </Paper>
                </Grid.Col>
                <Grid.Col span={{ base: 12, md: 8 }}>
                  <Paper withBorder radius="md" p="md" mih={200}>
                    {selected ? (
                      <FileViewer root={root} path={selected} />
                    ) : (
                      <Text c="dimmed" size="sm">
                        Select a Markdown file to preview it.
                      </Text>
                    )}
                  </Paper>
                </Grid.Col>
              </Grid>
            )
          )}
        </Stack>
      )}
    </QueryState>
  );
}

export function ExportBrowserPage() {
  const [tab, setTab] = useState<ExportRootKind>('root');
  return (
    <>
      <PageHeader
        title="Export browser"
        description="Browse the generated Markdown repository. Notes and transcripts can live under separate roots (see Settings → Export)."
      />
      <Tabs value={tab} onChange={(v) => setTab((v as ExportRootKind) ?? 'root')} keepMounted={false}>
        <Tabs.List mb="md">
          <Tabs.Tab value="root">Export root</Tabs.Tab>
          <Tabs.Tab value="notes">Notes root</Tabs.Tab>
          <Tabs.Tab value="transcripts">Transcripts root</Tabs.Tab>
        </Tabs.List>
        <Tabs.Panel value="root">
          <RootPanel root="root" />
        </Tabs.Panel>
        <Tabs.Panel value="notes">
          <RootPanel root="notes" />
        </Tabs.Panel>
        <Tabs.Panel value="transcripts">
          <RootPanel root="transcripts" />
        </Tabs.Panel>
      </Tabs>
    </>
  );
}
