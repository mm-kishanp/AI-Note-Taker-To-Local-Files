import { Anchor, Badge, Group, Table, Text, Tooltip, UnstyledButton } from '@mantine/core';
import { IconAlertCircle, IconArrowDown, IconArrowUp, IconSelector } from '@tabler/icons-react';
import type { KeyboardEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { MEETING_SOURCE_LABELS, type MeetingListItemDto, type MeetingQuery } from '../types/api';
import { formatDateTime, formatRelative, truncateList } from '../utils/format';
import { StatusBadge } from './StatusBadge';

type SortKey = NonNullable<MeetingQuery['sort']>;

export interface MeetingsTableProps {
  items: MeetingListItemDto[];
  sort?: SortKey;
  desc?: boolean;
  onSort?: (key: SortKey) => void;
  compact?: boolean;
}

function SortHeader({ label, k, sort, desc, onSort }: { label: string; k: SortKey; sort?: SortKey; desc?: boolean; onSort?: (k: SortKey) => void }) {
  if (!onSort) return <Table.Th>{label}</Table.Th>;
  const active = sort === k;
  const Icon = active ? (desc ? IconArrowDown : IconArrowUp) : IconSelector;
  return (
    <Table.Th aria-sort={active ? (desc ? 'descending' : 'ascending') : 'none'}>
      <UnstyledButton onClick={() => onSort(k)} aria-label={`Sort by ${label}`}>
        <Group gap={4} wrap="nowrap">
          <Text fw={700} size="sm">
            {label}
          </Text>
          <Icon size={14} aria-hidden />
        </Group>
      </UnstyledButton>
    </Table.Th>
  );
}

export function MeetingsTable({ items, sort, desc, onSort, compact }: MeetingsTableProps) {
  const navigate = useNavigate();
  const open = (id: string) => navigate(`/meetings/${id}`);
  const onKey = (e: KeyboardEvent, id: string) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      open(id);
    }
  };

  return (
    <Table.ScrollContainer minWidth={compact ? 600 : 1100}>
      <Table highlightOnHover verticalSpacing="xs" striped>
        <Table.Thead>
          <Table.Tr>
            <SortHeader label="Title" k="title" sort={sort} desc={desc} onSort={onSort} />
            <SortHeader label="Date" k="date" sort={sort} desc={desc} onSort={onSort} />
            {!compact && <Table.Th>Participants</Table.Th>}
            <SortHeader label="Project" k="project" sort={sort} desc={desc} onSort={onSort} />
            <SortHeader label="Classification" k="status" sort={sort} desc={desc} onSort={onSort} />
            {!compact && <Table.Th>Notes</Table.Th>}
            {!compact && <Table.Th>Transcript</Table.Th>}
            <Table.Th>Export</Table.Th>
            {!compact && <SortHeader label="Last synced" k="synced" sort={sort} desc={desc} onSort={onSort} />}
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {items.map((m) => {
            const parts = truncateList(m.participants, 3);
            return (
              <Table.Tr
                key={m.id}
                className="gpi-clickable-row"
                onClick={() => open(m.id)}
                onKeyDown={(e) => onKey(e, m.id)}
                tabIndex={0}
                aria-label={`Open meeting ${m.title}`}
              >
                <Table.Td maw={320}>
                  <Group gap={4} wrap="nowrap">
                    <Anchor component={Link} to={`/meetings/${m.id}`} onClick={(e) => e.stopPropagation()} size="sm" fw={500} lineClamp={2} tabIndex={-1}>
                      {m.title || '(untitled)'}
                    </Anchor>
                    {m.source !== 'Granola' && (
                      <Badge size="xs" variant="light" color={m.source === 'Fireflies' ? 'violet' : 'teal'} style={{ flexShrink: 0 }}>
                        {MEETING_SOURCE_LABELS[m.source]}
                      </Badge>
                    )}
                    {m.lastError && (
                      <Tooltip label={m.lastError} multiline w={300}>
                        <IconAlertCircle size={16} color="var(--mantine-color-red-6)" aria-label={`Error: ${m.lastError}`} />
                      </Tooltip>
                    )}
                  </Group>
                  {m.sourceStatus !== 'Active' && (
                    <Text size="xs" c="red">
                      Source {m.sourceStatus.toLowerCase()}
                    </Text>
                  )}
                </Table.Td>
                <Table.Td style={{ whiteSpace: 'nowrap' }}>
                  <Text size="sm">{formatDateTime(m.meetingDate)}</Text>
                </Table.Td>
                {!compact && (
                  <Table.Td maw={240}>
                    <Text size="sm" lineClamp={1} title={m.participants.join(', ')}>
                      {parts.shown || '—'}
                      {parts.hidden > 0 && <Text span c="dimmed"> +{parts.hidden}</Text>}
                    </Text>
                  </Table.Td>
                )}
                <Table.Td>
                  <Text size="sm">{m.projectName ?? <Text span c="dimmed">—</Text>}</Text>
                  {m.clientName && (
                    <Text size="xs" c="dimmed">
                      {m.clientName}
                    </Text>
                  )}
                </Table.Td>
                <Table.Td>
                  <StatusBadge kind="classification" value={m.classificationStatus} srLabel="Classification" />
                </Table.Td>
                {!compact && (
                  <Table.Td>
                    <StatusBadge kind="content" value={m.notesStatus} srLabel="Notes" />
                  </Table.Td>
                )}
                {!compact && (
                  <Table.Td>
                    <StatusBadge kind="content" value={m.transcriptStatus} srLabel="Transcript" />
                  </Table.Td>
                )}
                <Table.Td>
                  <StatusBadge kind="export" value={m.exportStatus} srLabel="Export" />
                </Table.Td>
                {!compact && (
                  <Table.Td style={{ whiteSpace: 'nowrap' }}>
                    <Text size="sm" title={formatDateTime(m.lastSyncedAt)}>
                      {formatRelative(m.lastSyncedAt)}
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
