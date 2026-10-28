import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { ActionIcon, Button, Group, Paper, Select, Stack, Switch, Text, TextInput, Tooltip } from '@mantine/core';
import { IconPlus, IconTrash } from '@tabler/icons-react';
import { RULE_KINDS } from '../types/api';
import { RULE_KIND_INFO } from '../utils/rules';
const kindOptions = RULE_KINDS.map((k) => ({ value: k, label: RULE_KIND_INFO[k].label }));
/** Editable list of {kind, value, enabled} rules with help text for each kind. */
export function RuleEditor({ value, onChange, label = 'Rules', description, error }) {
    const update = (i, patch) => onChange(value.map((r, idx) => (idx === i ? { ...r, ...patch } : r)));
    const remove = (i) => onChange(value.filter((_, idx) => idx !== i));
    const add = () => onChange([...value, { kind: 'Email', value: '', enabled: true }]);
    return (_jsxs(Stack, { gap: "xs", role: "group", "aria-label": label, children: [_jsxs("div", { children: [_jsx(Text, { fw: 500, size: "sm", children: label }), description && (_jsx(Text, { size: "xs", c: "dimmed", children: description }))] }), value.length === 0 && (_jsx(Text, { size: "sm", c: "dimmed", children: "No rules yet. Add one to route meetings automatically." })), value.map((rule, i) => {
                const info = RULE_KIND_INFO[rule.kind];
                return (_jsx(Paper, { withBorder: true, p: "xs", radius: "sm", children: _jsxs(Group, { align: "flex-start", gap: "xs", wrap: "wrap", children: [_jsx(Select, { label: `Rule ${i + 1} kind`, data: kindOptions, value: rule.kind, onChange: (v) => v && update(i, { kind: v }), allowDeselect: false, w: 190, size: "xs" }), _jsx(TextInput, { label: `Rule ${i + 1} value`, placeholder: info.placeholder, value: rule.value, onChange: (e) => update(i, { value: e.currentTarget.value }), description: info.help, style: { flex: 1, minWidth: 220 }, size: "xs", required: true }), _jsx(Switch, { label: "Enabled", checked: rule.enabled, onChange: (e) => update(i, { enabled: e.currentTarget.checked }), mt: 26, size: "sm" }), _jsx(Tooltip, { label: "Remove rule", children: _jsx(ActionIcon, { variant: "subtle", color: "red", mt: 22, onClick: () => remove(i), "aria-label": `Remove rule ${i + 1}`, children: _jsx(IconTrash, { size: 16 }) }) })] }) }, i));
            }), error && (_jsx(Text, { size: "xs", c: "red", role: "alert", children: error })), _jsx("div", { children: _jsx(Button, { size: "xs", variant: "light", leftSection: _jsx(IconPlus, { size: 14 }), onClick: add, children: "Add rule" }) })] }));
}
export function toEditableRules(rules) {
    return (rules ?? []).map((r) => ({ kind: r.kind, value: r.value, enabled: r.enabled ?? true }));
}
export function cleanRules(rules) {
    return rules.filter((r) => r.value.trim()).map((r) => ({ kind: r.kind, value: r.value.trim(), enabled: r.enabled }));
}
