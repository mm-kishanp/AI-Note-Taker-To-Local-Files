import { Button, Group, NativeSelect, Pagination, Paper, Select, SimpleGrid, Stack, Text, TextInput } from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import { IconFilterOff, IconSearch } from '@tabler/icons-react';
import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { EmptyState } from '../components/EmptyState';
import { MeetingsTable } from '../components/MeetingsTable';
import { PageHeader } from '../components/PageHeader';
import { LoadingRows, QueryState } from '../components/QueryState';
import { useMeetings } from '../hooks/useMeetings';
import { useClients, useProjects } from '../hooks/useProjects';
import {
  CLASSIFICATION_STATUSES, type ClassificationStatus, CONTENT_STATUSES, type ContentStatus, EXPORT_STATUSES, type ExportStatus,
  type MeetingQuery,
} from '../types/api';
import { dateParamToIso } from '../utils/format';
import { statusLabel } from '../utils/status';

const SORTS = ['date', 'title', 'project', 'synced', 'status'] as const;
type Sort = (typeof SORTS)[number];
const PAGE_SIZES = ['10', '25', '50', '100'];

const FILTER_KEYS = ['search', 'from', 'to', 'projectId', 'clientId', 'classification', 'exportStatus', 'transcriptStatus', 'needsReview'];

function opt<T extends string>(values: readonly T[], anyLabel: string) {
  return [{ value: '', label: anyLabel }, ...values.map((v) => ({ value: v, label: statusLabel(v) }))];
}

/** Server-side paginated meetings list. All filters live in the URL search params. */
export function MeetingsPage() {
  const [params, setParams] = useSearchParams();
  const get = (k: string) => params.get(k) ?? '';

  const setParam = (updates: Record<string, string | null>, resetPage = true) => {
    const next = new URLSearchParams(params);
    for (const [k, v] of Object.entries(updates)) {
      if (v === null || v === '') next.delete(k);
      else next.set(k, v);
    }
    if (resetPage) next.delete('page');
    setParams(next, { replace: true });
  };

  // Debounced free-text search, synced to the URL.
  const [searchInput, setSearchInput] = useState(get('search'));
  const [debouncedSearch] = useDebouncedValue(searchInput, 350);
  useEffect(() => {
    if (debouncedSearch !== get('search')) setParam({ search: debouncedSearch || null });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedSearch]);
  useEffect(() => {
    // Keep the input in sync when the URL changes externally (back/forward, clear filters).
    setSearchInput(params.get('search') ?? '');
  }, [params]);

  const sort = (SORTS as readonly string[]).includes(get('sort')) ? (get('sort') as Sort) : 'date';
  const desc = get('desc') ? get('desc') === 'true' : true;
  const page = Math.max(1, Number(get('page')) || 1);
  const pageSize = PAGE_SIZES.includes(get('pageSize')) ? Number(get('pageSize')) : 25;

  const query: MeetingQuery = useMemo(
    () => ({
      search: get('search') || undefined,
      from: dateParamToIso(get('from')),
      to: dateParamToIso(get('to'), true),
      projectId: get('projectId') || undefined,
      clientId: get('clientId') || undefined,
      classification: (get('classification') || undefined) as ClassificationStatus | undefined,
      exportStatus: (get('exportStatus') || undefined) as ExportStatus | undefined,
      transcriptStatus: (get('transcriptStatus') || undefined) as ContentStatus | undefined,
      needsReview: get('needsReview') === 'true' ? true : get('needsReview') === 'false' ? false : undefined,
      sort,
      desc,
      page,
      pageSize,
    }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [params],
  );

  const meetings = useMeetings(query);
  const projects = useProjects('', true);
  const clients = useClients();
  const hasFilters = FILTER_KEYS.some((k) => params.has(k));
  const total = meetings.data?.total ?? 0;
  const pages = Math.max(1, Math.ceil(total / pageSize));

  const onSort = (k: Sort) => setParam({ sort: k, desc: sort === k ? String(!desc) : k === 'title' || k === 'project' ? 'false' : 'true' }, false);

  return (
    <>
      <PageHeader title="Meetings" description="All meetings synchronized from Granola. Click a row for details, classification and files." />
      <Paper withBorder p="sm" radius="md" mb="md" component="section" aria-label="Filters">
        <Stack gap="xs">
          <SimpleGrid cols={{ base: 1, sm: 2, md: 4 }} spacing="xs">
            <TextInput
              label="Search"
              placeholder="Title, participant…"
              leftSection={<IconSearch size={14} />}
              value={searchInput}
              onChange={(e) => setSearchInput(e.currentTarget.value)}
            />
            <TextInput label="From" type="date" value={get('from')} onChange={(e) => setParam({ from: e.currentTarget.value })} />
            <TextInput label="To" type="date" value={get('to')} onChange={(e) => setParam({ to: e.currentTarget.value })} />
            <NativeSelect
              label="Project"
              value={get('projectId')}
              onChange={(e) => setParam({ projectId: e.currentTarget.value })}
              data={[{ value: '', label: 'Any project' }, ...(projects.data ?? []).map((p) => ({ value: p.id, label: p.name }))]}
            />
            <NativeSelect
              label="Client"
              value={get('clientId')}
              onChange={(e) => setParam({ clientId: e.currentTarget.value })}
              data={[{ value: '', label: 'Any client' }, ...(clients.data ?? []).map((c) => ({ value: c.id, label: c.name }))]}
            />
            <NativeSelect
              label="Classification"
              value={get('classification')}
              onChange={(e) => setParam({ classification: e.currentTarget.value })}
              data={opt(CLASSIFICATION_STATUSES, 'Any classification')}
            />
            <NativeSelect
              label="Export status"
              value={get('exportStatus')}
              onChange={(e) => setParam({ exportStatus: e.currentTarget.value })}
              data={opt(EXPORT_STATUSES, 'Any export status')}
            />
            <NativeSelect
              label="Transcript status"
              value={get('transcriptStatus')}
              onChange={(e) => setParam({ transcriptStatus: e.currentTarget.value })}
              data={opt(CONTENT_STATUSES, 'Any transcript status')}
            />
            <NativeSelect
              label="Needs review"
              value={get('needsReview')}
              onChange={(e) => setParam({ needsReview: e.currentTarget.value })}
              data={[
                { value: '', label: 'Any' },
                { value: 'true', label: 'Needs review' },
                { value: 'false', label: 'Reviewed / no review needed' },
              ]}
            />
          </SimpleGrid>
          <Group justify="space-between">
            <Text size="sm" c="dimmed" aria-live="polite">
              {meetings.data ? `${total} meeting${total === 1 ? '' : 's'}` : ' '}
            </Text>
            <Button
              variant="subtle"
              size="xs"
              leftSection={<IconFilterOff size={14} />}
              disabled={!hasFilters}
              onClick={() => {
                const next = new URLSearchParams(params);
                FILTER_KEYS.forEach((k) => next.delete(k));
                next.delete('page');
                setParams(next, { replace: true });
              }}
            >
              Clear filters
            </Button>
          </Group>
        </Stack>
      </Paper>

      <QueryState isLoading={meetings.isLoading} error={meetings.error} onRetry={() => void meetings.refetch()} skeleton={<LoadingRows rows={10} />}>
        {meetings.data && meetings.data.items.length === 0 ? (
          <EmptyState
            title={hasFilters ? 'No meetings match these filters' : 'No meetings yet'}
            description={hasFilters ? 'Try widening the date range or clearing filters.' : 'Run a sync to import meetings from Granola.'}
          />
        ) : (
          <Paper withBorder radius="md" style={{ opacity: meetings.isPlaceholderData ? 0.6 : 1 }}>
            <MeetingsTable items={meetings.data?.items ?? []} sort={sort} desc={desc} onSort={onSort} />
          </Paper>
        )}
        <Group justify="space-between" mt="md">
          <Select
            label="Rows per page"
            data={PAGE_SIZES}
            value={String(pageSize)}
            onChange={(v) => v && setParam({ pageSize: v })}
            w={130}
            size="xs"
            allowDeselect={false}
          />
          {pages > 1 && (
            <Pagination
              total={pages}
              value={Math.min(page, pages)}
              onChange={(p) => setParam({ page: String(p) }, false)}
              getControlProps={(control) => ({ 'aria-label': `${control} page` })}
            />
          )}
        </Group>
      </QueryState>
    </>
  );
}
