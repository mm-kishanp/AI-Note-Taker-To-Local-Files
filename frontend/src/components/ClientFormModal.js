import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { Alert, Button, Group, Modal, Stack, TextInput, Textarea } from '@mantine/core';
import { IconInfoCircle } from '@tabler/icons-react';
import { useEffect, useState } from 'react';
import { useSaveClient } from '../hooks/useProjects';
import { ApiError } from '../services/api';
import { notifySuccess } from '../utils/notify';
import { ProblemAlert } from './ProblemAlert';
import { cleanRules, RuleEditor, toEditableRules } from './RuleEditor';
export function ClientFormModal({ opened, onClose, client }) {
    const save = useSaveClient();
    const [name, setName] = useState('');
    const [description, setDescription] = useState('');
    const [rules, setRules] = useState([]);
    useEffect(() => {
        if (!opened)
            return;
        save.reset();
        setName(client?.name ?? '');
        setDescription(client?.description ?? '');
        setRules(toEditableRules(client?.rules));
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [opened, client]);
    const err = save.error instanceof ApiError ? save.error : null;
    const ruleErrors = err?.errors.filter((e) => e.field.startsWith('rules')).map((e) => `${e.field}: ${e.message}`).join(' ');
    return (_jsx(Modal, { opened: opened, onClose: onClose, title: client ? `Edit client “${client.name}”` : 'New client', size: "xl", children: _jsx("form", { onSubmit: (e) => {
                e.preventDefault();
                save.mutate({ id: client?.id, input: { name: name.trim(), description: description.trim() || null, rules: cleanRules(rules) } }, {
                    onSuccess: (c) => {
                        notifySuccess(`Client “${c.name}” saved.`);
                        onClose();
                    },
                });
            }, children: _jsxs(Stack, { gap: "sm", children: [_jsx(TextInput, { label: "Name", required: true, value: name, onChange: (e) => setName(e.currentTarget.value), error: err?.fieldError('name'), "data-autofocus": true }), _jsx(Textarea, { label: "Description", value: description, onChange: (e) => setDescription(e.currentTarget.value), autosize: true, minRows: 2 }), _jsx(Alert, { variant: "light", color: "blue", icon: _jsx(IconInfoCircle, { size: 16 }), children: "Client rules (e.g. the client\u2019s email domain) identify the client, not a single project. If the client has only one project, a client match routes the meeting there. If the client has several projects, a client match alone is ambiguous: add project-level rules (participants, speakers, folders or title keywords) to disambiguate, otherwise the meeting goes to the review queue." }), _jsx(RuleEditor, { value: rules, onChange: setRules, label: "Client rules", error: ruleErrors || undefined }), save.error && _jsx(ProblemAlert, { error: save.error }), _jsxs(Group, { justify: "flex-end", children: [_jsx(Button, { variant: "default", onClick: onClose, children: "Cancel" }), _jsx(Button, { type: "submit", loading: save.isPending, disabled: !name.trim(), children: client ? 'Save changes' : 'Create client' })] })] }) }) }));
}
