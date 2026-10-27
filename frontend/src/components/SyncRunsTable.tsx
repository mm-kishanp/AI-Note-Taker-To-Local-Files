import { Table, Text, Tooltip } from '@mantine/core';
import type { KeyboardEvent } from 'react';
import type { SyncRunDto } from '../types/api';
import { formatDateTime, formatDuration, humanize } from '../utils/format';
import { StatusBadge } from './StatusBadge';

export interface SyncRunsTableProps {
  runs: SyncRunDto[];
  onSelect?: (run: SyncRunDto) => void;
  compact?: boolean;
}

export function SyncRunsTable({ runs, onSelect, compact }: SyncRunsTableProps) {
  const onKey = (e: KeyboardEvent, r: SyncRunDto) => {
    if (onSelect && (e.key === 'Enter' || e.key === ' ')) {
      e.preventDefault();
      onSelect(r);
    }
  };
  return (
    <Table.ScrollContainer minWidth={compact ? 560 : 1000}>
      <Table highlightOnHover={!!onSelect} verticalSpacing="xs" striped>
        <Table.Thead>
          <Table.Tr>
            <Table.Th>Started</Table.Th>
            <Table.Th>Mode</Table.Th>
            {!compact && <Table.Th>Trigger</Table.Th>}
            <Table.Th>Status</Table.Th>
            <Table.Th>Processed</Table.Th>
            {!compact && <Table.Th>Discovered</Table.Th>}
            {!compact && <Table.Th>Notes / transcripts</Table.Th>}
            {!compact && <Table.Th>Files updated</Table.Th>}
            <Table.Th>Failures</Table.Th>
            {!compact && <Table.Th>Conflicts</Table.Th>}
            <Table.Th>Duration</Table.Th>
            {!compact && <Table.Th>Error</Table.Th>}
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {runs.map((r) => {
            const failures = r.apiFailures + r.fileSystemFailures + r.classificationFailures;
            return (
              <Table.Tr
                key={r.id}
                className={onSelect ? 'gpi-clickable-row' : undefined}
                onClick={onSelect ? () => onSelect(r) : undefined}
                onKeyDown={(e) => onKey(e, r)}
                tabIndex={onSelect ? 0 : undefined}
                aria-label={onSelect ? `Open run started ${formatDateTime(r.startedAt)}` : undefined}
              >
                <Table.Td style={{ whiteSpace: 'nowrap' }}>{formatDateTime(r.startedAt)}</Table.Td>
                <Table.Td>{humanize(r.mode)}</Table.Td>
                {!compact && <Table.Td>{r.trigger}</Table.Td>}
                <Table.Td>
                  <StatusBadge kind="sync" value={r.status} srLabel="Run status" />
                </Table.Td>
                <Table.Td>{r.meetingsProcessed}</Table.Td>
                {!compact && <Table.Td>{r.notesDiscovered}</Table.Td>}
                {!compact && (
                  <Table.Td>
                    {r.notesExported} / {r.transcriptsExported}
                  </Table.Td>
                )}
                {!compact && <Table.Td>{r.filesUpdated}</Table.Td>}
                <Table.Td>
                  <Tooltip
                    label={`API ${r.apiFailures} · File system ${r.fileSystemFailures} · Classification ${r.classificationFailures}`}
                    withArrow
                  >
                    <Text size="sm" c={failures > 0 ? 'red' : undefined} fw={failures > 0 ? 600 : undefined}>
                      {failures}
                    </Text>
                  </Tooltip>
                </Table.Td>
                {!compact && <Table.Td>{r.conflicts}</Table.Td>}
                <Table.Td style={{ whiteSpace: 'nowrap' }}>{r.finishedAt ? formatDuration(r.durationMs) : 'running…'}</Table.Td>
                {!compact && (
                  <Table.Td maw={260}>
                    <Text size="xs" c="red" lineClamp={2} title={r.error ?? undefined}>
                      {r.error}
                    </Text>
                  </Table.Td>
                )}
              </Table.Tr>
            );
          })}
        </Table.Tbody>
      </Table>
    </Table.ScrollContainer>
  );
}
