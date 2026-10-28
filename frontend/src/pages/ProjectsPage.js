import { jsxs as _jsxs, jsx as _jsx, Fragment as _Fragment } from "react/jsx-runtime";
import { ActionIcon, Alert, Anchor, Badge, Button, FileButton, Group, List, Paper, Stack, Switch, Table, Tabs, Text, TextInput, Tooltip, } from '@mantine/core';
import { useDebouncedValue } from '@mantine/hooks';
import { modals } from '@mantine/modals';
import { IconDownload, IconEdit, IconPlus, IconSearch, IconTrash, IconUpload } from '@tabler/icons-react';
import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { ClientFormModal } from '../components/ClientFormModal';
import { EmptyState } from '../components/EmptyState';
import { PageHeader } from '../components/PageHeader';
import { ProblemAlert } from '../components/ProblemAlert';
import { ProjectFormModal } from '../components/ProjectFormModal';
import { QueryState } from '../components/QueryState';
import { useClients, useDeleteClient, useImportConfig, useProjects } from '../hooks/useProjects';
import { api } from '../services/api';
import { downloadJson, formatDate, formatRelative } from '../utils/format';
import { notifyError, notifySuccess } from '../utils/notify';
function ProjectsTab() {
    const navigate = useNavigate();
    const [search, setSearch] = useState('');
    const [debounced] = useDebouncedValue(search, 300);
    const [includeArchived, setIncludeArchived] = useState(false);
    const [createOpen, setCreateOpen] = useState(false);
    const projects = useProjects(debounced.trim(), includeArchived);
    const importCfg = useImportConfig();
    const [importResult, setImportResult] = useState(null);
    const [exporting, setExporting] = useState(false);
    const doExport = async () => {
        setExporting(true);
        try {
            const cfg = await api.projects.exportConfig();
            downloadJson(`granola-projects-${new Date().toISOString().slice(0, 10)}.json`, cfg);
        }
        catch (e) {
            notifyError(e, 'Export failed');
        }
        finally {
            setExporting(false);
        }
    };
    const doImport = async (file) => {
        if (!file)
            return;
        let cfg;
        try {
            cfg = JSON.parse(await file.text());
            if (!cfg || !Array.isArray(cfg.projects) || !Array.isArray(cfg.clients))
                throw new Error('File does not look like a project configuration export.');
        }
        catch (e) {
            notifyError(e, 'Invalid file');
            return;
        }
        modals.openConfirmModal({
            title: 'Import project configuration?',
            children: (_jsxs(Text, { size: "sm", children: [cfg.projects.length, " project(s) and ", cfg.clients.length, " client(s) will be created or updated (matched by name/slug). Meetings are not re-classified automatically."] })),
            labels: { confirm: 'Import', cancel: 'Cancel' },
            onConfirm: () => importCfg.mutate(cfg, {
                onSuccess: (r) => {
                    setImportResult(r);
                    notifySuccess('Configuration imported.');
                },
                onError: (e) => notifyError(e, 'Import failed'),
            }),
        });
    };
    return (_jsxs(Stack, { gap: "md", children: [_jsxs(Group, { justify: "space-between", wrap: "wrap", children: [_jsxs(Group, { gap: "sm", wrap: "wrap", children: [_jsx(TextInput, { placeholder: "Search projects", "aria-label": "Search projects", leftSection: _jsx(IconSearch, { size: 14 }), value: search, onChange: (e) => setSearch(e.currentTarget.value), w: 260 }), _jsx(Switch, { label: "Include archived", checked: includeArchived, onChange: (e) => setIncludeArchived(e.currentTarget.checked) })] }), _jsxs(Group, { gap: "xs", children: [_jsx(Button, { variant: "default", leftSection: _jsx(IconDownload, { size: 16 }), onClick: doExport, loading: exporting, children: "Export config" }), _jsx(FileButton, { onChange: doImport, accept: "application/json,.json", children: (props) => (_jsx(Button, { variant: "default", leftSection: _jsx(IconUpload, { size: 16 }), loading: importCfg.isPending, ...props, children: "Import config" })) }), _jsx(Button, { leftSection: _jsx(IconPlus, { size: 16 }), onClick: () => setCreateOpen(true), children: "New project" })] })] }), importResult && (_jsxs(Alert, { color: importResult.warnings.length ? 'yellow' : 'green', title: "Import result", withCloseButton: true, onClose: () => setImportResult(null), children: [_jsxs(Text, { size: "sm", children: ["Clients: ", importResult.clientsCreated, " created, ", importResult.clientsUpdated, " updated. Projects: ", importResult.projectsCreated, " created,", ' ', importResult.projectsUpdated, " updated."] }), importResult.warnings.length > 0 && (_jsx(List, { size: "sm", mt: 4, children: importResult.warnings.map((w, i) => (_jsx(List.Item, { children: w }, i))) }))] })), _jsx(QueryState, { isLoading: projects.isLoading, error: projects.error, onRetry: () => void projects.refetch(), children: projects.data && projects.data.length === 0 ? (_jsx(EmptyState, { title: debounced ? 'No projects match your search' : 'No projects yet', description: "Projects define where meeting notes are filed and which rules route meetings to them.", action: _jsx(Button, { onClick: () => setCreateOpen(true), children: "Create project" }) })) : (_jsx(Paper, { withBorder: true, radius: "md", children: _jsx(Table.ScrollContainer, { minWidth: 800, children: _jsxs(Table, { highlightOnHover: true, striped: true, verticalSpacing: "xs", children: [_jsx(Table.Thead, { children: _jsxs(Table.Tr, { children: [_jsx(Table.Th, { children: "Name" }), _jsx(Table.Th, { children: "Client" }), _jsx(Table.Th, { children: "Status" }), _jsx(Table.Th, { children: "Meetings" }), _jsx(Table.Th, { children: "Rules" }), _jsx(Table.Th, { children: "Last meeting" }), _jsx(Table.Th, { children: "Priority" })] }) }), _jsx(Table.Tbody, { children: projects.data?.map((p) => (_jsxs(Table.Tr, { className: "gpi-clickable-row", tabIndex: 0, onClick: () => navigate(`/projects/${p.id}`), onKeyDown: (e) => {
                                            if (e.key === 'Enter')
                                                navigate(`/projects/${p.id}`);
                                        }, "aria-label": `Open project ${p.name}`, children: [_jsxs(Table.Td, { children: [_jsx(Anchor, { component: Link, to: `/projects/${p.id}`, fw: 500, size: "sm", tabIndex: -1, onClick: (e) => e.stopPropagation(), children: p.name }), _jsx(Text, { size: "xs", c: "dimmed", ff: "monospace", children: p.slug })] }), _jsx(Table.Td, { children: p.clientName ?? _jsx(Text, { c: "dimmed", span: true, children: "\u2014" }) }), _jsx(Table.Td, { children: _jsx(Badge, { color: p.status === 'Active' ? 'green' : 'gray', variant: "light", style: { textTransform: 'none' }, children: p.status }) }), _jsx(Table.Td, { children: p.meetingCount }), _jsxs(Table.Td, { children: [p.rules.filter((r) => r.enabled).length, p.rules.some((r) => !r.enabled) && _jsxs(Text, { span: true, c: "dimmed", size: "xs", children: [" (+", p.rules.filter((r) => !r.enabled).length, " disabled)"] })] }), _jsx(Table.Td, { title: formatDate(p.lastMeetingAt), children: formatRelative(p.lastMeetingAt, '—') }), _jsx(Table.Td, { children: p.defaultClassificationPriority })] }, p.id))) })] }) }) })) }), _jsx(ProjectFormModal, { opened: createOpen, onClose: () => setCreateOpen(false), onSaved: (p) => navigate(`/projects/${p.id}`) })] }));
}
function ClientsTab() {
    const clients = useClients();
    const del = useDeleteClient();
    const [editing, setEditing] = useState(null);
    const [formOpen, setFormOpen] = useState(false);
    const confirmDelete = (c) => modals.openConfirmModal({
        title: `Delete client “${c.name}”?`,
        children: (_jsxs(Text, { size: "sm", children: ["Its client-level rules are removed. ", c.projects.length > 0 ? `${c.projects.length} project(s) will no longer be linked to a client.` : '', " Meetings and files are not deleted."] })),
        labels: { confirm: 'Delete client', cancel: 'Cancel' },
        confirmProps: { color: 'red' },
        onConfirm: () => del.mutate(c.id, { onSuccess: () => notifySuccess('Client deleted.'), onError: (e) => notifyError(e) }),
    });
    return (_jsxs(Stack, { gap: "md", children: [_jsx(Alert, { variant: "light", color: "blue", children: "Clients group projects and can carry their own rules (typically the client\u2019s email domain). When a client has more than one project, a client-rule match cannot decide between them \u2014 add project rules (participants, speakers, folders, title keywords) to disambiguate, otherwise matching meetings go to the review queue." }), _jsx(Group, { justify: "flex-end", children: _jsx(Button, { leftSection: _jsx(IconPlus, { size: 16 }), onClick: () => {
                        setEditing(null);
                        setFormOpen(true);
                    }, children: "New client" }) }), _jsx(QueryState, { isLoading: clients.isLoading, error: clients.error, onRetry: () => void clients.refetch(), children: clients.data && clients.data.length === 0 ? (_jsx(EmptyState, { title: "No clients yet", description: "Clients are optional. Use them to share rules between related projects." })) : (_jsx(Paper, { withBorder: true, radius: "md", children: _jsx(Table.ScrollContainer, { minWidth: 700, children: _jsxs(Table, { striped: true, verticalSpacing: "xs", children: [_jsx(Table.Thead, { children: _jsxs(Table.Tr, { children: [_jsx(Table.Th, { children: "Name" }), _jsx(Table.Th, { children: "Projects" }), _jsx(Table.Th, { children: "Rules" }), _jsx(Table.Th, { children: "Actions" })] }) }), _jsx(Table.Tbody, { children: clients.data?.map((c) => (_jsxs(Table.Tr, { children: [_jsxs(Table.Td, { children: [_jsx(Text, { fw: 500, size: "sm", children: c.name }), c.description && (_jsx(Text, { size: "xs", c: "dimmed", lineClamp: 2, children: c.description }))] }), _jsxs(Table.Td, { children: [_jsxs(Group, { gap: 6, children: [c.projects.length === 0 && _jsx(Text, { size: "sm", c: "dimmed", children: "None" }), c.projects.map((p) => (_jsx(Anchor, { component: Link, to: `/projects/${p.id}`, size: "sm", children: p.name }, p.id)))] }), c.projects.length > 1 && (_jsx(Text, { size: "xs", c: "yellow.8", children: "Multiple projects \u2014 project rules needed to disambiguate." }))] }), _jsx(Table.Td, { children: _jsxs(Stack, { gap: 2, children: [c.rules.length === 0 && _jsx(Text, { size: "sm", c: "dimmed", children: "No rules" }), c.rules.map((r) => (_jsxs(Text, { size: "xs", c: r.enabled ? undefined : 'dimmed', children: [r.kind, ": ", _jsx(Text, { span: true, ff: "monospace", size: "xs", children: r.value }), !r.enabled && ' (disabled)'] }, r.id)))] }) }), _jsx(Table.Td, { children: _jsxs(Group, { gap: 4, wrap: "nowrap", children: [_jsx(Tooltip, { label: "Edit client", children: _jsx(ActionIcon, { variant: "subtle", onClick: () => {
                                                                    setEditing(c);
                                                                    setFormOpen(true);
                                                                }, "aria-label": `Edit client ${c.name}`, children: _jsx(IconEdit, { size: 16 }) }) }), _jsx(Tooltip, { label: "Delete client", children: _jsx(ActionIcon, { variant: "subtle", color: "red", onClick: () => confirmDelete(c), "aria-label": `Delete client ${c.name}`, children: _jsx(IconTrash, { size: 16 }) }) })] }) })] }, c.id))) })] }) }) })) }), del.error && _jsx(ProblemAlert, { error: del.error }), _jsx(ClientFormModal, { opened: formOpen, onClose: () => setFormOpen(false), client: editing })] }));
}
export function ProjectsPage({ initialTab = 'projects' }) {
    const [params, setParams] = useSearchParams();
    const tab = params.get('tab') ?? initialTab;
    return (_jsxs(_Fragment, { children: [_jsx(PageHeader, { title: "Projects & clients", description: "Projects decide where meetings are filed; rules decide which meetings belong to them." }), _jsxs(Tabs, { value: tab, onChange: (v) => {
                    const next = new URLSearchParams(params);
                    next.set('tab', v ?? 'projects');
                    setParams(next, { replace: true });
                }, children: [_jsxs(Tabs.List, { mb: "md", children: [_jsx(Tabs.Tab, { value: "projects", children: "Projects" }), _jsx(Tabs.Tab, { value: "clients", children: "Clients" })] }), _jsx(Tabs.Panel, { value: "projects", children: _jsx(ProjectsTab, {}) }), _jsx(Tabs.Panel, { value: "clients", children: _jsx(ClientsTab, {}) })] })] }));
}
