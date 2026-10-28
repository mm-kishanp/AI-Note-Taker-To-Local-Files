import { jsx as _jsx, jsxs as _jsxs, Fragment as _Fragment } from "react/jsx-runtime";
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
import { CLASSIFICATION_STATUSES, CONTENT_STATUSES, EXPORT_STATUSES, MEETING_SOURCES, } from '../types/api';
import { dateParamToIso } from '../utils/format';
import { statusLabel } from '../utils/status';
const SORTS = ['date', 'title', 'project', 'synced', 'status'];
const PAGE_SIZES = ['10', '25', '50', '100'];
const FILTER_KEYS = ['search', 'from', 'to', 'projectId', 'clientId', 'classification', 'exportStatus', 'transcriptStatus', 'needsReview', 'source'];
function opt(values, anyLabel) {
    return [{ value: '', label: anyLabel }, ...values.map((v) => ({ value: v, label: statusLabel(v) }))];
}
/** Server-side paginated meetings list. All filters live in the URL search params. */
export function MeetingsPage() {
    const [params, setParams] = useSearchParams();
    const get = (k) => params.get(k) ?? '';
    const setParam = (updates, resetPage = true) => {
        const next = new URLSearchParams(params);
        for (const [k, v] of Object.entries(updates)) {
            if (v === null || v === '')
                next.delete(k);
            else
                next.set(k, v);
        }
        if (resetPage)
            next.delete('page');
        setParams(next, { replace: true });
    };
    // Debounced free-text search, synced to the URL.
    const [searchInput, setSearchInput] = useState(get('search'));
    const [debouncedSearch] = useDebouncedValue(searchInput, 350);
    useEffect(() => {
        if (debouncedSearch !== get('search'))
            setParam({ search: debouncedSearch || null });
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [debouncedSearch]);
    useEffect(() => {
        // Keep the input in sync when the URL changes externally (back/forward, clear filters).
        setSearchInput(params.get('search') ?? '');
    }, [params]);
    const sort = SORTS.includes(get('sort')) ? get('sort') : 'date';
    const desc = get('desc') ? get('desc') === 'true' : true;
    const page = Math.max(1, Number(get('page')) || 1);
    const pageSize = PAGE_SIZES.includes(get('pageSize')) ? Number(get('pageSize')) : 25;
    const query = useMemo(() => ({
        search: get('search') || undefined,
        from: dateParamToIso(get('from')),
        to: dateParamToIso(get('to'), true),
        projectId: get('projectId') || undefined,
        clientId: get('clientId') || undefined,
        classification: (get('classification') || undefined),
        exportStatus: (get('exportStatus') || undefined),
        transcriptStatus: (get('transcriptStatus') || undefined),
        needsReview: get('needsReview') === 'true' ? true : get('needsReview') === 'false' ? false : undefined,
        source: (get('source') || undefined),
        sort,
        desc,
        page,
        pageSize,
    }), 
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [params]);
    const meetings = useMeetings(query);
    const projects = useProjects('', true);
    const clients = useClients();
    const hasFilters = FILTER_KEYS.some((k) => params.has(k));
    const total = meetings.data?.total ?? 0;
    const pages = Math.max(1, Math.ceil(total / pageSize));
    const onSort = (k) => setParam({ sort: k, desc: sort === k ? String(!desc) : k === 'title' || k === 'project' ? 'false' : 'true' }, false);
    return (_jsxs(_Fragment, { children: [_jsx(PageHeader, { title: "Meetings", description: "All meetings synchronized from Granola and Fireflies. Click a row for details, classification and files." }), _jsx(Paper, { withBorder: true, p: "sm", radius: "md", mb: "md", component: "section", "aria-label": "Filters", children: _jsxs(Stack, { gap: "xs", children: [_jsxs(SimpleGrid, { cols: { base: 1, sm: 2, md: 4 }, spacing: "xs", children: [_jsx(TextInput, { label: "Search", placeholder: "Title, participant\u2026", leftSection: _jsx(IconSearch, { size: 14 }), value: searchInput, onChange: (e) => setSearchInput(e.currentTarget.value) }), _jsx(TextInput, { label: "From", type: "date", value: get('from'), onChange: (e) => setParam({ from: e.currentTarget.value }) }), _jsx(TextInput, { label: "To", type: "date", value: get('to'), onChange: (e) => setParam({ to: e.currentTarget.value }) }), _jsx(NativeSelect, { label: "Project", value: get('projectId'), onChange: (e) => setParam({ projectId: e.currentTarget.value }), data: [{ value: '', label: 'Any project' }, ...(projects.data ?? []).map((p) => ({ value: p.id, label: p.name }))] }), _jsx(NativeSelect, { label: "Client", value: get('clientId'), onChange: (e) => setParam({ clientId: e.currentTarget.value }), data: [{ value: '', label: 'Any client' }, ...(clients.data ?? []).map((c) => ({ value: c.id, label: c.name }))] }), _jsx(NativeSelect, { label: "Classification", value: get('classification'), onChange: (e) => setParam({ classification: e.currentTarget.value }), data: opt(CLASSIFICATION_STATUSES, 'Any classification') }), _jsx(NativeSelect, { label: "Export status", value: get('exportStatus'), onChange: (e) => setParam({ exportStatus: e.currentTarget.value }), data: opt(EXPORT_STATUSES, 'Any export status') }), _jsx(NativeSelect, { label: "Transcript status", value: get('transcriptStatus'), onChange: (e) => setParam({ transcriptStatus: e.currentTarget.value }), data: opt(CONTENT_STATUSES, 'Any transcript status') }), _jsx(NativeSelect, { label: "Needs review", value: get('needsReview'), onChange: (e) => setParam({ needsReview: e.currentTarget.value }), data: [
                                        { value: '', label: 'Any' },
                                        { value: 'true', label: 'Needs review' },
                                        { value: 'false', label: 'Reviewed / no review needed' },
                                    ] }), _jsx(NativeSelect, { label: "Source", value: get('source'), onChange: (e) => setParam({ source: e.currentTarget.value }), data: [{ value: '', label: 'Any source' }, ...MEETING_SOURCES.map((v) => ({ value: v, label: v }))] })] }), _jsxs(Group, { justify: "space-between", children: [_jsx(Text, { size: "sm", c: "dimmed", "aria-live": "polite", children: meetings.data ? `${total} meeting${total === 1 ? '' : 's'}` : ' ' }), _jsx(Button, { variant: "subtle", size: "xs", leftSection: _jsx(IconFilterOff, { size: 14 }), disabled: !hasFilters, onClick: () => {
                                        const next = new URLSearchParams(params);
                                        FILTER_KEYS.forEach((k) => next.delete(k));
                                        next.delete('page');
                                        setParams(next, { replace: true });
                                    }, children: "Clear filters" })] })] }) }), _jsxs(QueryState, { isLoading: meetings.isLoading, error: meetings.error, onRetry: () => void meetings.refetch(), skeleton: _jsx(LoadingRows, { rows: 10 }), children: [meetings.data && meetings.data.items.length === 0 ? (_jsx(EmptyState, { title: hasFilters ? 'No meetings match these filters' : 'No meetings yet', description: hasFilters ? 'Try widening the date range or clearing filters.' : 'Run a sync to import meetings from Granola.' })) : (_jsx(Paper, { withBorder: true, radius: "md", style: { opacity: meetings.isPlaceholderData ? 0.6 : 1 }, children: _jsx(MeetingsTable, { items: meetings.data?.items ?? [], sort: sort, desc: desc, onSort: onSort }) })), _jsxs(Group, { justify: "space-between", mt: "md", children: [_jsx(Select, { label: "Rows per page", data: PAGE_SIZES, value: String(pageSize), onChange: (v) => v && setParam({ pageSize: v }), w: 130, size: "xs", allowDeselect: false }), pages > 1 && (_jsx(Pagination, { total: pages, value: Math.min(page, pages), onChange: (p) => setParam({ page: String(p) }, false), getControlProps: (control) => ({ 'aria-label': `${control} page` }) }))] })] })] }));
}
