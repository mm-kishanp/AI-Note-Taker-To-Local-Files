import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { Button, Code, Group, Modal, NumberInput, Select, Stack, Text, TextInput, Textarea } from '@mantine/core';
import { useEffect, useState } from 'react';
import { useClients, useSaveProject } from '../hooks/useProjects';
import { ApiError } from '../services/api';
import { notifySuccess } from '../utils/notify';
import { TEMPLATE_VARIABLES } from '../utils/rules';
import { ProblemAlert } from './ProblemAlert';
import { cleanRules, RuleEditor, toEditableRules } from './RuleEditor';
export function TemplateHelp() {
    return (_jsxs(Text, { size: "xs", c: "dimmed", children: ["Relative to the export root. Leave empty to use the global template. Variables:", ' ', TEMPLATE_VARIABLES.map((v, i) => (_jsxs("span", { children: [_jsx(Code, { children: v }), i < TEMPLATE_VARIABLES.length - 1 ? ' ' : ''] }, v)))] }));
}
function errorsWithPrefix(err, prefix) {
    if (!(err instanceof ApiError))
        return undefined;
    const msgs = err.errors.filter((e) => e.field.toLowerCase().startsWith(prefix)).map((e) => `${e.field}: ${e.message}`);
    return msgs.length ? msgs.join(' ') : undefined;
}
export function ProjectFormModal({ opened, onClose, project, onSaved, initialRules, initialName }) {
    const clients = useClients();
    const save = useSaveProject();
    const [name, setName] = useState('');
    const [slug, setSlug] = useState('');
    const [description, setDescription] = useState('');
    const [clientId, setClientId] = useState(null);
    const [priority, setPriority] = useState(100);
    const [notesFolder, setNotesFolder] = useState('');
    const [transcriptFolder, setTranscriptFolder] = useState('');
    const [rules, setRules] = useState([]);
    useEffect(() => {
        if (!opened)
            return;
        save.reset();
        setName(project?.name ?? initialName ?? '');
        setSlug(project?.slug ?? '');
        setDescription(project?.description ?? '');
        setClientId(project?.clientId ?? null);
        setPriority(project?.defaultClassificationPriority ?? 100);
        setNotesFolder(project?.notesFolderPath ?? '');
        setTranscriptFolder(project?.transcriptFolderPath ?? '');
        setRules(project ? toEditableRules(project.rules) : initialRules ?? []);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [opened, project]);
    const err = save.error instanceof ApiError ? save.error : null;
    const submit = () => {
        const input = {
            name: name.trim(),
            slug: slug.trim() || null,
            description: description.trim() || null,
            clientId,
            status: project?.status ?? 'Active',
            notesFolderPath: notesFolder.trim() || null,
            transcriptFolderPath: transcriptFolder.trim() || null,
            defaultClassificationPriority: priority,
            rules: cleanRules(rules),
        };
        save.mutate({ id: project?.id, input }, {
            onSuccess: (p) => {
                notifySuccess(`Project “${p.name}” saved.`);
                onSaved?.(p);
                onClose();
            },
        });
    };
    return (_jsx(Modal, { opened: opened, onClose: onClose, title: project ? `Edit project “${project.name}”` : 'New project', size: "xl", children: _jsx("form", { onSubmit: (e) => {
                e.preventDefault();
                submit();
            }, children: _jsxs(Stack, { gap: "sm", children: [_jsxs(Group, { grow: true, align: "flex-start", children: [_jsx(TextInput, { label: "Name", required: true, value: name, onChange: (e) => setName(e.currentTarget.value), error: err?.fieldError('name'), "data-autofocus": true }), _jsx(TextInput, { label: "Slug", description: "Optional \u2014 generated from the name. Used in folder names ({ProjectSlug}).", value: slug, onChange: (e) => setSlug(e.currentTarget.value), error: err?.fieldError('slug'), ff: "monospace" })] }), _jsx(Textarea, { label: "Description", value: description, onChange: (e) => setDescription(e.currentTarget.value), autosize: true, minRows: 2, maxRows: 5 }), _jsxs(Group, { grow: true, align: "flex-start", children: [_jsx(Select, { label: "Client", placeholder: "No client", data: (clients.data ?? []).map((c) => ({ value: c.id, label: c.name })), value: clientId, onChange: setClientId, clearable: true, searchable: true, error: err?.fieldError('clientId') }), _jsx(NumberInput, { label: "Priority", description: "When several projects match, lower numbers are ranked first (e.g. primary copy in multi-export mode).", value: priority, onChange: (v) => setPriority(typeof v === 'number' ? v : Number(v) || 0), min: 0, max: 10000 })] }), _jsx(TextInput, { label: "Notes folder override", placeholder: "e.g. Clients/{ClientName}/{ProjectSlug}/Notes", value: notesFolder, onChange: (e) => setNotesFolder(e.currentTarget.value), error: err?.fieldError('notesFolderPath'), ff: "monospace" }), _jsx(TextInput, { label: "Transcripts folder override", placeholder: "e.g. Clients/{ClientName}/{ProjectSlug}/Transcripts/{Year}", value: transcriptFolder, onChange: (e) => setTranscriptFolder(e.currentTarget.value), error: err?.fieldError('transcriptFolderPath'), ff: "monospace" }), _jsx(TemplateHelp, {}), _jsx(RuleEditor, { value: rules, onChange: setRules, label: "Mapping rules", description: "A meeting is routed to this project when any enabled rule matches (evaluated in the configured tier order).", error: errorsWithPrefix(save.error, 'rules') }), save.error && _jsx(ProblemAlert, { error: save.error }), _jsxs(Group, { justify: "flex-end", children: [_jsx(Button, { variant: "default", onClick: onClose, children: "Cancel" }), _jsx(Button, { type: "submit", loading: save.isPending, disabled: !name.trim(), children: project ? 'Save changes' : 'Create project' })] })] }) }) }));
}
