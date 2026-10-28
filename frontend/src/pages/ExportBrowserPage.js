import { jsx as _jsx, jsxs as _jsxs, Fragment as _Fragment } from "react/jsx-runtime";
import { Alert, Badge, Button, Grid, Group, NavLink, Paper, ScrollArea, SegmentedControl, Stack, Tabs, Text, } from '@mantine/core';
import { IconExternalLink, IconFileText, IconFolder, IconFolderOpen } from '@tabler/icons-react';
import { useState } from 'react';
import { EmptyState } from '../components/EmptyState';
import { Markdown } from '../components/Markdown';
import { PageHeader } from '../components/PageHeader';
import { PathText } from '../components/PathText';
import { LoadingRows, QueryState } from '../components/QueryState';
import { useExportFile, useExportTree, useOpenExportPath } from '../hooks/useExports';
import { formatBytes, formatDateTime, formatRelative } from '../utils/format';
import { notifyError } from '../utils/notify';
/** Strips YAML front matter for the rendered view (it is shown separately). */
function splitFrontMatter(content) {
    const text = content.replace(/\r\n/g, '\n');
    if (!text.startsWith('---\n'))
        return { frontMatter: null, body: content };
    const end = text.indexOf('\n---\n', 3);
    if (end < 0)
        return { frontMatter: null, body: content };
    return { frontMatter: text.slice(4, end), body: text.slice(end + 5) };
}
function TreeNode({ node, depth, selected, onSelect, }) {
    if (!node.isDirectory) {
        return (_jsx(NavLink, { label: node.name, description: `${formatBytes(node.size)} · ${formatRelative(node.lastModified)}`, leftSection: _jsx(IconFileText, { size: 16 }), active: selected === node.relativePath, onClick: () => onSelect(node.relativePath), pl: 8 + depth * 14, "aria-label": `Open file ${node.name}` }));
    }
    return (_jsx(NavLink, { label: node.name || 'Export root', leftSection: _jsx(IconFolder, { size: 16 }), rightSection: _jsx(Badge, { size: "xs", variant: "light", color: "gray", children: node.fileCount }), description: node.lastModified ? `updated ${formatRelative(node.lastModified)}` : undefined, defaultOpened: depth < 2, childrenOffset: 0, pl: 8 + depth * 14, "aria-label": `Folder ${node.name || 'export root'}, ${node.fileCount} files`, children: node.children.map((c) => (_jsx(TreeNode, { node: c, depth: depth + 1, selected: selected, onSelect: onSelect }, c.relativePath || c.name))) }));
}
function FileViewer({ root, path }) {
    const file = useExportFile(root, path);
    const open = useOpenExportPath();
    const [mode, setMode] = useState('rendered');
    const parts = file.data ? splitFrontMatter(file.data.content) : null;
    return (_jsxs(Stack, { gap: "sm", children: [_jsxs(Group, { justify: "space-between", wrap: "wrap", children: [_jsx(PathText, { path: path }), _jsxs(Group, { gap: "xs", children: [_jsx(SegmentedControl, { size: "xs", value: mode, onChange: (v) => setMode(v), data: [
                                    { label: 'Rendered', value: 'rendered' },
                                    { label: 'Raw', value: 'raw' },
                                ], "aria-label": "View mode" }), _jsx(Button, { size: "xs", variant: "light", leftSection: _jsx(IconExternalLink, { size: 14 }), loading: open.isPending, onClick: () => open.mutate({ root, path }, { onError: (e) => notifyError(e, 'Could not open the file') }), children: "Open in editor" })] })] }), _jsx(QueryState, { isLoading: file.isLoading, error: file.error, children: file.data && parts && (_jsxs(_Fragment, { children: [_jsxs(Text, { size: "xs", c: "dimmed", children: [formatBytes(file.data.size), " \u00B7 modified ", formatDateTime(file.data.lastModified)] }), mode === 'raw' ? (_jsx(Paper, { withBorder: true, p: "sm", radius: "sm", children: _jsx(Text, { component: "pre", ff: "monospace", size: "xs", style: { whiteSpace: 'pre-wrap', wordBreak: 'break-word', margin: 0 }, children: file.data.content }) })) : (_jsxs(_Fragment, { children: [parts.frontMatter && (_jsx(Paper, { withBorder: true, p: "xs", radius: "sm", bg: "var(--mantine-color-default-hover)", children: _jsx(Text, { component: "pre", ff: "monospace", size: "xs", style: { whiteSpace: 'pre-wrap', wordBreak: 'break-word', margin: 0 }, children: parts.frontMatter }) })), _jsx(Markdown, { content: parts.body })] }))] })) })] }));
}
function RootPanel({ root }) {
    const tree = useExportTree(root);
    const open = useOpenExportPath();
    const [selected, setSelected] = useState(null);
    return (_jsx(QueryState, { isLoading: tree.isLoading, error: tree.error, onRetry: () => void tree.refetch(), skeleton: _jsx(LoadingRows, { rows: 8 }), children: tree.data && (_jsxs(Stack, { gap: "sm", children: [_jsxs(Group, { justify: "space-between", wrap: "wrap", children: [_jsx(PathText, { path: tree.data.root }), _jsx(Button, { size: "xs", variant: "default", leftSection: _jsx(IconFolderOpen, { size: 14 }), disabled: !tree.data.exists, loading: open.isPending, onClick: () => open.mutate({ root }, { onError: (e) => notifyError(e, 'Could not open the folder') }), children: "Open folder" })] }), tree.data.warning && (_jsx(Alert, { color: tree.data.exists ? 'yellow' : 'blue', role: "status", children: tree.data.warning })), tree.data.exists && tree.data.tree && (tree.data.tree.fileCount === 0 ? (_jsx(EmptyState, { title: "No exported files yet", description: "Run a sync to export meetings into this folder." })) : (_jsxs(Grid, { gutter: "md", children: [_jsx(Grid.Col, { span: { base: 12, md: 4 }, children: _jsx(Paper, { withBorder: true, radius: "md", children: _jsx(ScrollArea, { h: 620, type: "auto", children: _jsx("nav", { "aria-label": "Exported files", children: _jsx(TreeNode, { node: tree.data.tree, depth: 0, selected: selected, onSelect: setSelected }) }) }) }) }), _jsx(Grid.Col, { span: { base: 12, md: 8 }, children: _jsx(Paper, { withBorder: true, radius: "md", p: "md", mih: 200, children: selected ? (_jsx(FileViewer, { root: root, path: selected })) : (_jsx(Text, { c: "dimmed", size: "sm", children: "Select a Markdown file to preview it." })) }) })] })))] })) }));
}
export function ExportBrowserPage() {
    const [tab, setTab] = useState('root');
    return (_jsxs(_Fragment, { children: [_jsx(PageHeader, { title: "Export browser", description: "Browse the generated Markdown repository. Notes and transcripts can live under separate roots (see Settings \u2192 Export)." }), _jsxs(Tabs, { value: tab, onChange: (v) => setTab(v ?? 'root'), keepMounted: false, children: [_jsxs(Tabs.List, { mb: "md", children: [_jsx(Tabs.Tab, { value: "root", children: "Export root" }), _jsx(Tabs.Tab, { value: "notes", children: "Notes root" }), _jsx(Tabs.Tab, { value: "transcripts", children: "Transcripts root" })] }), _jsx(Tabs.Panel, { value: "root", children: _jsx(RootPanel, { root: "root" }) }), _jsx(Tabs.Panel, { value: "notes", children: _jsx(RootPanel, { root: "notes" }) }), _jsx(Tabs.Panel, { value: "transcripts", children: _jsx(RootPanel, { root: "transcripts" }) })] })] }));
}
