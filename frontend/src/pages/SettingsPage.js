import { jsx as _jsx, jsxs as _jsxs, Fragment as _Fragment } from "react/jsx-runtime";
import { ActionIcon, Alert, Anchor, Badge, Button, Checkbox, Code, Divider, Group, List, Modal, NumberInput, Paper, PasswordInput, Select, SimpleGrid, Stack, Switch, Tabs, Text, TextInput, Title, Tooltip, } from '@mantine/core';
import { IconArrowDown, IconArrowUp, IconCheck, IconFolderSearch, IconKey, IconPlugConnected, IconTrash, IconX, } from '@tabler/icons-react';
import { useEffect, useState } from 'react';
import { FolderPicker } from '../components/FolderPicker';
import { PageHeader } from '../components/PageHeader';
import { PathText } from '../components/PathText';
import { ProblemAlert } from '../components/ProblemAlert';
import { LoadingRows, QueryState } from '../components/QueryState';
import { useAiCredential, useDeleteAiCredential, useDeleteSourceCredential, useMigrateRoot, useSetAiCredential, useSetSourceCredential, useSettings, useSourceCredential, useTestConnection, useUpdateSettings, } from '../hooks/useSettings';
import { ApiError } from '../services/api';
import { CLASSIFICATION_TIERS, CONFLICT_POLICIES, MULTI_PROJECT_MODES, } from '../types/api';
import { formatDateTime, formatRelative, humanize } from '../utils/format';
import { notifyError, notifySuccess } from '../utils/notify';
const TIER_HELP = {
    ParticipantEmail: 'Exact participant / invitee email (Email rules)',
    ParticipantIdentity: 'Attendee name or email domain (ParticipantName, EmailDomain rules)',
    SpeakerIdentity: 'Speakers Granola or Fireflies identified by name in the transcript',
    Client: 'Client rules → that client’s projects',
    Metadata: 'Granola folder membership (GranolaFolder rules)',
    Keyword: 'Title / summary keywords',
    Ai: 'Optional AI suggestion (only when enabled below)',
};
const LOG_LEVELS = ['Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical'];
/** Field errors from a 400 problem whose field starts with the given prefix. */
function fieldError(error, field) {
    if (!(error instanceof ApiError))
        return undefined;
    return error.errors.find((e) => e.field?.toLowerCase() === field.toLowerCase())?.message;
}
function Section({ title, description, children }) {
    return (_jsx(Paper, { withBorder: true, radius: "md", p: "lg", children: _jsxs(Stack, { gap: "md", children: [_jsxs("div", { children: [_jsx(Title, { order: 4, children: title }), description && (_jsx(Text, { size: "sm", c: "dimmed", mt: 4, children: description }))] }), children] }) }));
}
/** Local editable copy of one settings section with a Save/Reset bar. */
function useSectionForm(server) {
    const [value, setValue] = useState(server);
    useEffect(() => setValue(server), [server]);
    const dirty = JSON.stringify(value) !== JSON.stringify(server);
    const patch = (p) => setValue((v) => (v ? { ...v, ...p } : v));
    return { value, setValue, patch, dirty, reset: () => setValue(server) };
}
function SaveBar({ dirty, saving, onSave, onReset, error }) {
    return (_jsxs(Stack, { gap: "xs", children: [error ? _jsx(ProblemAlert, { error: error, title: "Settings were not saved" }) : null, _jsxs(Group, { justify: "flex-end", children: [_jsx(Button, { variant: "default", disabled: !dirty || saving, onClick: onReset, children: "Reset" }), _jsx(Button, { disabled: !dirty, loading: saving, onClick: onSave, children: "Save" })] })] }));
}
function useSaveSection() {
    const update = useUpdateSettings();
    const save = (req, label) => update.mutate({ export: null, sync: null, classification: null, logging: null, ...req }, { onSuccess: () => notifySuccess(`${label} settings saved.`) });
    return { save, saving: update.isPending, error: update.error, reset: update.reset };
}
// ---------------------------------------------------------------- Source connections
const SOURCE_INFO = {
    Granola: {
        title: 'Granola connection',
        where: _jsxs(_Fragment, { children: ["Create a key in the Granola desktop app under ", _jsx("b", { children: "Settings \u2192 Connectors \u2192 API keys" }), "."] }),
        placeholder: 'grn_…',
        envVar: 'GRANOLA_API_KEY',
    },
    Fireflies: {
        title: 'Fireflies connection',
        where: (_jsxs(_Fragment, { children: ["Optional. Every call Fireflies.ai recorded is imported with its AI notes and transcript. Copy the key from", ' ', _jsx(Anchor, { href: "https://app.fireflies.ai/integrations", target: "_blank", rel: "noopener noreferrer", children: "app.fireflies.ai \u2192 Integrations \u2192 Fireflies API" }), "."] })),
        placeholder: 'Fireflies API key',
        envVar: 'FIREFLIES_API_KEY',
    },
};
function SourceConnection({ source }) {
    const info = SOURCE_INFO[source];
    const status = useSourceCredential(source);
    const setKey = useSetSourceCredential(source);
    const del = useDeleteSourceCredential(source);
    const test = useTestConnection(source);
    const [key, setKeyValue] = useState('');
    const [confirmDelete, setConfirmDelete] = useState(false);
    const [lastTest, setLastTest] = useState(null);
    const s = status.data;
    return (_jsxs(Section, { title: info.title, description: _jsxs(_Fragment, { children: [info.where, " The key is validated, then stored in the operating system's credential store; it is never shown again in full and never sent to this browser."] }), children: [_jsx(QueryState, { isLoading: status.isLoading, error: status.error, skeleton: _jsx(LoadingRows, { rows: 3 }), children: s && (_jsxs(Stack, { gap: "md", children: [_jsxs(SimpleGrid, { cols: { base: 1, sm: 2 }, spacing: "sm", children: [_jsxs("div", { children: [_jsx(Text, { size: "xs", c: "dimmed", children: "Status" }), _jsxs(Group, { gap: "xs", children: [_jsx(Badge, { color: s.configured ? 'green' : 'gray', variant: "light", style: { textTransform: 'none' }, children: s.configured ? 'Key configured' : 'No key' }), s.lastCheckSucceeded !== null && (_jsx(Badge, { color: s.lastCheckSucceeded ? 'green' : 'red', variant: "outline", style: { textTransform: 'none' }, children: s.lastCheckSucceeded ? 'Last check OK' : 'Last check failed' }))] })] }), _jsxs("div", { children: [_jsx(Text, { size: "xs", c: "dimmed", children: "Key" }), _jsx(Text, { ff: "monospace", size: "sm", children: s.maskedKey ?? '—' })] }), _jsxs("div", { children: [_jsx(Text, { size: "xs", c: "dimmed", children: "Stored in" }), _jsx(Text, { size: "sm", children: s.source ?? s.storeName })] }), _jsxs("div", { children: [_jsx(Text, { size: "xs", c: "dimmed", children: "Last successful request" }), _jsx(Text, { size: "sm", children: s.lastSuccessAt ? `${formatDateTime(s.lastSuccessAt)} (${formatRelative(s.lastSuccessAt)})` : 'never' })] })] }), s.lastError && s.lastCheckSucceeded === false && (_jsx(Alert, { color: "red", title: humanize(s.lastErrorCategory), role: "alert", children: s.lastError })), s.canStore ? (_jsx("form", { onSubmit: (e) => {
                                e.preventDefault();
                                setKey.mutate({ apiKey: key.trim(), validate: true }, {
                                    onSuccess: () => {
                                        setKeyValue('');
                                        notifySuccess(`API key saved and verified with ${source}.`);
                                    },
                                });
                            }, children: _jsxs(Group, { align: "flex-end", wrap: "wrap", children: [_jsx(PasswordInput, { label: s.configured ? 'Replace API key' : 'API key', placeholder: info.placeholder, value: key, onChange: (e) => setKeyValue(e.currentTarget.value), autoComplete: "off", style: { flex: 1, minWidth: 260 }, error: fieldError(setKey.error, 'apiKey') }), _jsx(Button, { type: "submit", leftSection: _jsx(IconKey, { size: 16 }), loading: setKey.isPending, disabled: key.trim().length < 10, children: "Save & verify" })] }) })) : (_jsxs(Alert, { color: "blue", children: [s.storeName, " is read-only on this host. Provide the key with the ", _jsx(Code, { children: info.envVar }), " environment variable."] })), setKey.error && !fieldError(setKey.error, 'apiKey') ? _jsx(ProblemAlert, { error: setKey.error }) : null, _jsxs(Group, { children: [_jsx(Button, { variant: "light", leftSection: _jsx(IconPlugConnected, { size: 16 }), loading: test.isPending, disabled: !s.configured, onClick: () => test.mutate(undefined, { onSuccess: setLastTest, onError: (e) => notifyError(e) }), children: "Test connection" }), s.configured && s.canStore && (_jsx(Button, { variant: "subtle", color: "red", leftSection: _jsx(IconTrash, { size: 16 }), onClick: () => setConfirmDelete(true), children: "Remove key" }))] }), lastTest && (_jsx(Alert, { color: lastTest.success ? 'green' : 'red', icon: lastTest.success ? _jsx(IconCheck, { size: 16 }) : _jsx(IconX, { size: 16 }), title: lastTest.success ? 'Connected' : `Connection failed (${humanize(lastTest.errorCategory)})`, role: "status", children: lastTest.message }))] })) }), _jsx(Modal, { opened: confirmDelete, onClose: () => setConfirmDelete(false), title: `Remove the ${source} API key?`, children: _jsxs(Stack, { children: [_jsxs(Text, { size: "sm", children: [source, " meetings stop synchronizing until a new key is added. Exported files and meeting history are not affected. To revoke the key itself, also delete it in ", source, "."] }), _jsxs(Group, { justify: "flex-end", children: [_jsx(Button, { variant: "default", onClick: () => setConfirmDelete(false), children: "Cancel" }), _jsx(Button, { color: "red", loading: del.isPending, onClick: () => del.mutate(undefined, {
                                        onSuccess: () => {
                                            setConfirmDelete(false);
                                            notifySuccess('API key removed.');
                                        },
                                        onError: (e) => notifyError(e),
                                    }), children: "Remove key" })] })] }) })] }));
}
// ---------------------------------------------------------------- Synchronization
function SyncSection({ settings }) {
    const form = useSectionForm(settings.sync);
    const { save, saving, error } = useSaveSection();
    const v = form.value;
    if (!v)
        return null;
    const num = (field) => (val) => form.patch({ [field]: typeof val === 'number' ? val : Number(val) });
    return (_jsxs(Section, { title: "Synchronization", description: "Incremental syncs only fetch meetings changed since the last successful run. Failed meetings are retried with backoff.", children: [_jsx(Switch, { label: "Automatic synchronization", description: "Runs an incremental sync on the interval below while the app is running.", checked: v.autoSyncEnabled, onChange: (e) => form.patch({ autoSyncEnabled: e.currentTarget.checked }) }), _jsxs(SimpleGrid, { cols: { base: 1, sm: 2 }, children: [_jsx(NumberInput, { label: "Interval (minutes)", min: 5, max: 1440, value: v.intervalMinutes, onChange: num('intervalMinutes'), error: fieldError(error, 'sync.intervalMinutes') }), _jsx(TextInput, { type: "date", label: "Initial import from", description: "Only import meetings created on or after this date. Empty = all history.", value: v.initialImportFrom ? v.initialImportFrom.slice(0, 10) : '', onChange: (e) => form.patch({ initialImportFrom: e.currentTarget.value ? `${e.currentTarget.value}T00:00:00Z` : null }) }), _jsx(NumberInput, { label: "Concurrent meetings", description: "1\u20138. Requests are throttled to Granola's rate limit regardless.", min: 1, max: 8, value: v.maxConcurrency, onChange: num('maxConcurrency'), error: fieldError(error, 'sync.maxConcurrency') }), _jsx(NumberInput, { label: "Attempts per request", description: "Retries for transient Granola errors (1\u201310).", min: 1, max: 10, value: v.maxRequestAttempts, onChange: num('maxRequestAttempts'), error: fieldError(error, 'sync.maxRequestAttempts') }), _jsx(NumberInput, { label: "Retries per failed meeting", description: "Scheduled retries before only manual retry applies.", min: 0, max: 50, value: v.maxMeetingRetries, onChange: num('maxMeetingRetries'), error: fieldError(error, 'sync.maxMeetingRetries') }), _jsx(NumberInput, { label: "Incremental overlap (minutes)", description: "Tolerates clock skew between this computer and Granola.", min: 0, max: 1440, value: v.incrementalOverlapMinutes, onChange: num('incrementalOverlapMinutes'), error: fieldError(error, 'sync.incrementalOverlapMinutes') }), _jsx(NumberInput, { label: "Fireflies look-back (days)", description: "Fireflies finishes transcripts and AI notes after the call; recent calls are re-checked until both arrive.", min: 1, max: 60, value: v.firefliesLookbackDays, onChange: num('firefliesLookbackDays'), error: fieldError(error, 'sync.firefliesLookbackDays') })] }), _jsx(SaveBar, { dirty: form.dirty, saving: saving, onReset: form.reset, error: error, onSave: () => save({ sync: v }, 'Synchronization') })] }));
}
// ---------------------------------------------------------------- Export
function PathField({ label, description, value, onChange, optional }) {
    const [picker, setPicker] = useState(false);
    return (_jsxs(_Fragment, { children: [_jsx(TextInput, { label: label, description: description, value: value ?? '', placeholder: optional ? 'Same as export root' : undefined, onChange: (e) => onChange(e.currentTarget.value || (optional ? null : '')), ff: "monospace", rightSection: _jsx(Tooltip, { label: "Browse\u2026", children: _jsx(ActionIcon, { variant: "subtle", onClick: () => setPicker(true), "aria-label": `Browse for ${label}`, children: _jsx(IconFolderSearch, { size: 16 }) }) }) }), _jsx(FolderPicker, { opened: picker, onClose: () => setPicker(false), initialPath: value, onSelect: (p) => onChange(p), title: label })] }));
}
function ExportSection({ settings }) {
    const form = useSectionForm(settings.export);
    const { save, saving, error } = useSaveSection();
    const [migrateOpen, setMigrateOpen] = useState(false);
    const v = form.value;
    if (!v)
        return null;
    const text = (field) => (e) => form.patch({ [field]: e.currentTarget.value });
    const flag = (field) => (e) => form.patch({ [field]: e.currentTarget.checked });
    return (_jsxs(Section, { title: "Export", description: _jsxs(_Fragment, { children: ["Templates are relative to their root. Variables: ", _jsx(Code, { children: '{ProjectSlug}' }), " ", _jsx(Code, { children: '{ProjectName}' }), " ", _jsx(Code, { children: '{ClientName}' }), ' ', _jsx(Code, { children: '{ProjectDirectory}' }), " ", _jsx(Code, { children: '{MeetingDate}' }), " ", _jsx(Code, { children: '{Year}' }), " ", _jsx(Code, { children: '{Month}' }), " ", _jsx(Code, { children: '{Day}' }), ' ', _jsx(Code, { children: '{MeetingId}' }), " ", _jsx(Code, { children: '{MeetingTitle}' }), ". Existing files keep their paths; changes apply to newly exported meetings."] }), children: [_jsxs(Stack, { gap: 4, children: [_jsx(Text, { size: "xs", c: "dimmed", children: "Currently resolved" }), _jsxs(Group, { gap: "xs", children: [_jsx(Text, { size: "sm", w: 110, children: "Root" }), _jsx(PathText, { path: settings.resolvedPaths.root })] }), _jsxs(Group, { gap: "xs", children: [_jsx(Text, { size: "sm", w: 110, children: "Notes" }), _jsx(PathText, { path: settings.resolvedPaths.notesRoot })] }), _jsxs(Group, { gap: "xs", children: [_jsx(Text, { size: "sm", w: 110, children: "Transcripts" }), _jsx(PathText, { path: settings.resolvedPaths.transcriptsRoot })] })] }), _jsx(PathField, { label: "Export root", description: "Absolute, or relative to the app's base directory.", value: v.rootDirectory, onChange: (p) => form.patch({ rootDirectory: p ?? '' }) }), _jsxs(SimpleGrid, { cols: { base: 1, sm: 2 }, children: [_jsx(PathField, { label: "Separate notes root (optional)", value: v.notesRootDirectory, optional: true, onChange: (p) => form.patch({ notesRootDirectory: p }) }), _jsx(PathField, { label: "Separate transcripts root (optional)", value: v.transcriptsRootDirectory, optional: true, onChange: (p) => form.patch({ transcriptsRootDirectory: p }) }), _jsx(TextInput, { label: "Project directory", value: v.projectDirectoryTemplate, onChange: text('projectDirectoryTemplate'), ff: "monospace" }), _jsx(TextInput, { label: "File name", description: "Without .md", value: v.filenameTemplate, onChange: text('filenameTemplate'), ff: "monospace" }), _jsx(TextInput, { label: "Notes directory", value: v.notesDirectoryTemplate, onChange: text('notesDirectoryTemplate'), ff: "monospace" }), _jsx(TextInput, { label: "Transcripts directory", value: v.transcriptsDirectoryTemplate, onChange: text('transcriptsDirectoryTemplate'), ff: "monospace" }), _jsx(TextInput, { label: "Unassigned notes", value: v.unassignedNotesDirectory, onChange: text('unassignedNotesDirectory'), ff: "monospace" }), _jsx(TextInput, { label: "Unassigned transcripts", value: v.unassignedTranscriptsDirectory, onChange: text('unassignedTranscriptsDirectory'), ff: "monospace" }), _jsx(TextInput, { label: "Shared notes", value: v.sharedNotesDirectory, onChange: text('sharedNotesDirectory'), ff: "monospace" }), _jsx(TextInput, { label: "Shared transcripts", value: v.sharedTranscriptsDirectory, onChange: text('sharedTranscriptsDirectory'), ff: "monospace" })] }), _jsxs(SimpleGrid, { cols: { base: 1, sm: 2 }, children: [_jsx(Checkbox, { label: "Export notes", checked: v.exportNotes, onChange: flag('exportNotes') }), _jsx(Checkbox, { label: "Export transcripts", checked: v.exportTranscripts, onChange: flag('exportTranscripts') }), _jsx(Checkbox, { label: "Move unmodified files when a meeting is reassigned", checked: v.moveFilesOnReassignment, onChange: flag('moveFilesOnReassignment') }), _jsx(Checkbox, { label: "Link Granola photo attachments (signed URLs)", checked: v.includeAttachmentLinks, onChange: flag('includeAttachmentLinks') }), _jsx(Checkbox, { label: "Maintain README.md and PROJECT_CONTEXT.md per project", checked: v.generateProjectContext, onChange: flag('generateProjectContext') }), _jsx(Checkbox, { label: "Maintain Unassigned/ReviewQueue.md", checked: v.generateReviewQueue, onChange: flag('generateReviewQueue') })] }), _jsx(Select, { label: "When an exported file was edited locally", data: CONFLICT_POLICIES.map((p) => ({
                    value: p,
                    label: p === 'PreserveUserEdits' ? 'Preserve my edits (mark conflicts for review) — recommended' : 'Always overwrite with the source version',
                })), value: v.conflictPolicy, onChange: (val) => val && form.patch({ conflictPolicy: val }), allowDeselect: false }), _jsx(SaveBar, { dirty: form.dirty, saving: saving, onReset: form.reset, error: error, onSave: () => save({ export: v }, 'Export') }), _jsx(Divider, {}), _jsxs(Group, { justify: "space-between", wrap: "wrap", children: [_jsx(Text, { size: "sm", c: "dimmed", maw: 600, children: "Move the whole repository to another folder or drive. Files are copied; nothing is overwritten or deleted." }), _jsx(Button, { variant: "light", onClick: () => setMigrateOpen(true), children: "Migrate export root\u2026" })] }), _jsx(MigrateRootModal, { opened: migrateOpen, onClose: () => setMigrateOpen(false), current: settings.resolvedPaths.root })] }));
}
function MigrateRootModal({ opened, onClose, current }) {
    const migrate = useMigrateRoot();
    const [target, setTarget] = useState(null);
    const [confirmed, setConfirmed] = useState(false);
    const [result, setResult] = useState(null);
    useEffect(() => {
        if (opened) {
            setTarget(null);
            setConfirmed(false);
            setResult(null);
            migrate.reset();
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [opened]);
    return (_jsx(Modal, { opened: opened, onClose: onClose, title: "Migrate export root", size: "lg", children: _jsxs(Stack, { children: [_jsxs(Text, { size: "sm", children: ["Copies every file from ", _jsx(Code, { children: current }), " to the new folder and then points the settings at it. Files already at the destination are never overwritten, and the current folder is left in place \u2014 delete it yourself once you have checked the copy."] }), _jsx(PathField, { label: "New export root", value: target, onChange: setTarget }), _jsx(Checkbox, { label: "I understand the old folder is kept and must be removed manually.", checked: confirmed, onChange: (e) => setConfirmed(e.currentTarget.checked) }), migrate.error ? _jsx(ProblemAlert, { error: migrate.error }) : null, result && (_jsxs(Alert, { color: "green", title: "Migration complete", role: "status", children: [_jsxs(Text, { size: "sm", children: ["Copied ", result.filesCopied, " file(s), skipped ", result.filesSkipped, " identical file(s)."] }), result.warnings.length > 0 && (_jsx(List, { size: "sm", mt: 4, children: result.warnings.map((w, i) => (_jsx(List.Item, { children: w }, i))) }))] })), _jsxs(Group, { justify: "flex-end", children: [_jsx(Button, { variant: "default", onClick: onClose, children: result ? 'Close' : 'Cancel' }), !result && (_jsx(Button, { disabled: !target || !confirmed, loading: migrate.isPending, onClick: () => target && migrate.mutate({ newRoot: target, confirm: true }, { onSuccess: setResult }), children: "Copy and switch" }))] })] }) }));
}
// ---------------------------------------------------------------- Classification
function ClassificationSection({ settings }) {
    const form = useSectionForm(settings.classification);
    const { save, saving, error } = useSaveSection();
    const ai = useAiCredential();
    const setAi = useSetAiCredential();
    const delAi = useDeleteAiCredential();
    const [aiKey, setAiKey] = useState('');
    const v = form.value;
    if (!v)
        return null;
    const order = [...v.tierOrder, ...CLASSIFICATION_TIERS.filter((t) => !v.tierOrder.includes(t))];
    const move = (i, d) => {
        const next = [...order];
        [next[i], next[i + d]] = [next[i + d], next[i]];
        form.patch({ tierOrder: next });
    };
    const toggleTier = (t, enabled) => form.patch({ disabledTiers: enabled ? v.disabledTiers.filter((x) => x !== t) : [...v.disabledTiers, t] });
    return (_jsxs(Section, { title: "Classification", description: "Tiers are evaluated top to bottom; the first tier with evidence decides. A manual assignment always wins and is never changed by rules.", children: [_jsxs(Stack, { gap: 6, children: [_jsx(Text, { size: "sm", fw: 500, children: "Rule priority" }), order.map((t, i) => (_jsx(Paper, { withBorder: true, p: "xs", radius: "sm", children: _jsxs(Group, { justify: "space-between", wrap: "nowrap", children: [_jsxs(Group, { gap: "sm", wrap: "nowrap", style: { minWidth: 0 }, children: [_jsxs(Text, { fw: 600, w: 20, ta: "right", children: [i + 1, "."] }), _jsx(Switch, { "aria-label": `Enable ${humanize(t)}`, checked: !v.disabledTiers.includes(t), onChange: (e) => toggleTier(t, e.currentTarget.checked) }), _jsxs("div", { style: { minWidth: 0 }, children: [_jsx(Text, { size: "sm", fw: 500, children: t === 'Ai' ? 'AI' : humanize(t) }), _jsx(Text, { size: "xs", c: "dimmed", children: TIER_HELP[t] })] })] }), _jsxs(Group, { gap: 2, wrap: "nowrap", children: [_jsx(ActionIcon, { variant: "subtle", disabled: i === 0, onClick: () => move(i, -1), "aria-label": `Move ${humanize(t)} up`, children: _jsx(IconArrowUp, { size: 16 }) }), _jsx(ActionIcon, { variant: "subtle", disabled: i === order.length - 1, onClick: () => move(i, 1), "aria-label": `Move ${humanize(t)} down`, children: _jsx(IconArrowDown, { size: 16 }) })] })] }) }, t))), fieldError(error, 'classification.tierOrder') && _jsx(Text, { size: "sm", c: "red", children: fieldError(error, 'classification.tierOrder') })] }), _jsx(Select, { label: "When one tier matches several projects", data: MULTI_PROJECT_MODES.map((m) => ({
                    value: m,
                    label: m === 'SingleProject'
                        ? 'Send to review (Unassigned) — recommended'
                        : m === 'Shared'
                            ? 'Save in the Shared folder'
                            : 'Export to every matching project (one canonical file + copies)',
                })), value: v.multiProjectMode, onChange: (val) => val && form.patch({ multiProjectMode: val }), allowDeselect: false }), _jsxs(SimpleGrid, { cols: { base: 1, sm: 2 }, children: [_jsx(Checkbox, { label: "Ignore my own email and name", description: "So your presence never routes a meeting.", checked: v.ignoreOwner, onChange: (e) => form.patch({ ignoreOwner: e.currentTarget.checked }) }), _jsx(Checkbox, { label: "Use calendar invitees as participants", checked: v.useCalendarInvitees, onChange: (e) => form.patch({ useCalendarInvitees: e.currentTarget.checked }) }), _jsx(NumberInput, { label: "Minimum identified segments per speaker", min: 1, max: 1000, value: v.minimumSpeakerSegments, onChange: (val) => form.patch({ minimumSpeakerSegments: Number(val) }), error: fieldError(error, 'classification.minimumSpeakerSegments') })] }), _jsx(Divider, { label: "AI-assisted classification (optional)", labelPosition: "left" }), _jsx(Alert, { color: "blue", variant: "light", children: "Off by default. When enabled, meetings that no rule matched are sent to Anthropic's Claude API with: title, participant names and emails, identified speaker names, Granola folder names and your project list (names, clients, descriptions). Granola's summary is sent only if you tick \u201CInclude summary\u201D. Transcripts are never sent. The model can only choose an existing project or none, and by default its answer is a suggestion for review." }), _jsxs(SimpleGrid, { cols: { base: 1, sm: 2 }, children: [_jsx(Switch, { label: "Enable AI suggestions", checked: v.ai.enabled, onChange: (e) => form.patch({ ai: { ...v.ai, enabled: e.currentTarget.checked } }) }), _jsx(TextInput, { label: "Model", value: v.ai.model, onChange: (e) => form.patch({ ai: { ...v.ai, model: e.currentTarget.value } }), ff: "monospace", error: fieldError(error, 'classification.ai.model') }), _jsx(Checkbox, { label: "Assign automatically (low certainty) instead of only suggesting", checked: v.ai.autoAssign, onChange: (e) => form.patch({ ai: { ...v.ai, autoAssign: e.currentTarget.checked } }) }), _jsx(Checkbox, { label: "Include Granola's summary", checked: v.ai.includeSummary, onChange: (e) => form.patch({ ai: { ...v.ai, includeSummary: e.currentTarget.checked } }) })] }), _jsx(SaveBar, { dirty: form.dirty, saving: saving, onReset: form.reset, error: error, onSave: () => save({ classification: v }, 'Classification') }), _jsxs(Group, { align: "flex-end", wrap: "wrap", children: [_jsxs("div", { children: [_jsx(Text, { size: "xs", c: "dimmed", children: "Anthropic API key" }), _jsx(Text, { size: "sm", ff: "monospace", children: ai.data?.keyConfigured ? ai.data.maskedKey : 'not configured' })] }), ai.data?.canStore ? (_jsxs(_Fragment, { children: [_jsx(PasswordInput, { placeholder: "sk-ant-\u2026", value: aiKey, onChange: (e) => setAiKey(e.currentTarget.value), autoComplete: "off", "aria-label": "Anthropic API key", style: { flex: 1, minWidth: 220 } }), _jsx(Button, { variant: "light", disabled: aiKey.trim().length < 10, loading: setAi.isPending, onClick: () => setAi.mutate({ apiKey: aiKey.trim() }, { onSuccess: () => { setAiKey(''); notifySuccess('AI key saved.'); }, onError: (e) => notifyError(e) }), children: "Save key" }), ai.data.keyConfigured && (_jsx(Button, { variant: "subtle", color: "red", loading: delAi.isPending, onClick: () => delAi.mutate(undefined, { onError: (e) => notifyError(e) }), children: "Remove" }))] })) : (_jsxs(Text, { size: "sm", c: "dimmed", children: ["Set ", _jsx(Code, { children: "ANTHROPIC_API_KEY" }), " on this host."] }))] })] }));
}
// ---------------------------------------------------------------- Logging
function LoggingSection({ settings }) {
    const form = useSectionForm(settings.logging);
    const { save, saving, error } = useSaveSection();
    const v = form.value;
    if (!v)
        return null;
    return (_jsxs(Section, { title: "Logging", description: "Structured JSON-lines log files. API keys and bearer tokens are always redacted.", children: [_jsxs(SimpleGrid, { cols: { base: 1, sm: 3 }, children: [_jsx(Select, { label: "Level", data: LOG_LEVELS, value: v.minimumLevel, onChange: (val) => val && form.patch({ minimumLevel: val }), allowDeselect: false, error: fieldError(error, 'logging.minimumLevel') }), _jsx(TextInput, { label: "Log directory", value: v.logDirectory, onChange: (e) => form.patch({ logDirectory: e.currentTarget.value }), ff: "monospace", error: fieldError(error, 'logging.logDirectory') }), _jsx(NumberInput, { label: "Retention (days)", min: 1, max: 365, value: v.retentionDays, onChange: (val) => form.patch({ retentionDays: Number(val) }), error: fieldError(error, 'logging.retentionDays') })] }), _jsx(SaveBar, { dirty: form.dirty, saving: saving, onReset: form.reset, error: error, onSave: () => save({ logging: v }, 'Logging') })] }));
}
export function SettingsPage() {
    const settings = useSettings();
    return (_jsxs(_Fragment, { children: [_jsx(PageHeader, { title: "Settings", description: _jsxs(_Fragment, { children: ["Configuration is stored in the local database. API docs: ", _jsx(Anchor, { href: "https://docs.granola.ai/introduction", target: "_blank", rel: "noopener noreferrer", children: "Granola" }), ", ", _jsx(Anchor, { href: "https://docs.fireflies.ai/getting-started/quickstart", target: "_blank", rel: "noopener noreferrer", children: "Fireflies" }), "."] }), docTitle: "Settings" }), _jsxs(Tabs, { defaultValue: "connection", keepMounted: false, children: [_jsxs(Tabs.List, { mb: "md", children: [_jsx(Tabs.Tab, { value: "connection", children: "Connections" }), _jsx(Tabs.Tab, { value: "sync", children: "Synchronization" }), _jsx(Tabs.Tab, { value: "export", children: "Export" }), _jsx(Tabs.Tab, { value: "classification", children: "Classification" }), _jsx(Tabs.Tab, { value: "logging", children: "Logging" })] }), _jsx(Tabs.Panel, { value: "connection", children: _jsxs(Stack, { gap: "lg", children: [_jsx(SourceConnection, { source: "Granola" }), _jsx(SourceConnection, { source: "Fireflies" })] }) }), ['sync', 'export', 'classification', 'logging'].map((tab) => (_jsx(Tabs.Panel, { value: tab, children: _jsxs(QueryState, { isLoading: settings.isLoading, error: settings.error, onRetry: () => void settings.refetch(), children: [settings.data && tab === 'sync' && _jsx(SyncSection, { settings: settings.data }), settings.data && tab === 'export' && _jsx(ExportSection, { settings: settings.data }), settings.data && tab === 'classification' && _jsx(ClassificationSection, { settings: settings.data }), settings.data && tab === 'logging' && _jsx(LoggingSection, { settings: settings.data })] }) }, tab)))] })] }));
}
