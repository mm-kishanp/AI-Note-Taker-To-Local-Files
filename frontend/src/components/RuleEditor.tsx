import { ActionIcon, Button, Group, Paper, Select, Stack, Switch, Text, TextInput, Tooltip } from '@mantine/core';
import { IconPlus, IconTrash } from '@tabler/icons-react';
import { RULE_KINDS, type RuleInput, type RuleKind } from '../types/api';
import { RULE_KIND_INFO } from '../utils/rules';

export interface EditableRule extends RuleInput {
  enabled: boolean;
}

export interface RuleEditorProps {
  value: EditableRule[];
  onChange: (rules: EditableRule[]) => void;
  label?: string;
  description?: string;
  /** Validation error from the server (e.g. field "rules[0].value"). */
  error?: string;
}

const kindOptions = RULE_KINDS.map((k) => ({ value: k, label: RULE_KIND_INFO[k].label }));

/** Editable list of {kind, value, enabled} rules with help text for each kind. */
export function RuleEditor({ value, onChange, label = 'Rules', description, error }: RuleEditorProps) {
  const update = (i: number, patch: Partial<EditableRule>) => onChange(value.map((r, idx) => (idx === i ? { ...r, ...patch } : r)));
  const remove = (i: number) => onChange(value.filter((_, idx) => idx !== i));
  const add = () => onChange([...value, { kind: 'Email', value: '', enabled: true }]);

  return (
    <Stack gap="xs" role="group" aria-label={label}>
      <div>
        <Text fw={500} size="sm">
          {label}
        </Text>
        {description && (
          <Text size="xs" c="dimmed">
            {description}
          </Text>
        )}
      </div>
      {value.length === 0 && (
        <Text size="sm" c="dimmed">
          No rules yet. Add one to route meetings automatically.
        </Text>
      )}
      {value.map((rule, i) => {
        const info = RULE_KIND_INFO[rule.kind];
        return (
          <Paper key={i} withBorder p="xs" radius="sm">
            <Group align="flex-start" gap="xs" wrap="wrap">
              <Select
                label={`Rule ${i + 1} kind`}
                data={kindOptions}
                value={rule.kind}
                onChange={(v) => v && update(i, { kind: v as RuleKind })}
                allowDeselect={false}
                w={190}
                size="xs"
              />
              <TextInput
                label={`Rule ${i + 1} value`}
                placeholder={info.placeholder}
                value={rule.value}
                onChange={(e) => update(i, { value: e.currentTarget.value })}
                description={info.help}
                style={{ flex: 1, minWidth: 220 }}
                size="xs"
                required
              />
              <Switch
                label="Enabled"
                checked={rule.enabled}
                onChange={(e) => update(i, { enabled: e.currentTarget.checked })}
                mt={26}
                size="sm"
              />
              <Tooltip label="Remove rule">
                <ActionIcon variant="subtle" color="red" mt={22} onClick={() => remove(i)} aria-label={`Remove rule ${i + 1}`}>
                  <IconTrash size={16} />
                </ActionIcon>
              </Tooltip>
            </Group>
          </Paper>
        );
      })}
      {error && (
        <Text size="xs" c="red" role="alert">
          {error}
        </Text>
      )}
      <div>
        <Button size="xs" variant="light" leftSection={<IconPlus size={14} />} onClick={add}>
          Add rule
        </Button>
      </div>
    </Stack>
  );
}

export function toEditableRules(rules: { kind: RuleKind; value: string; enabled?: boolean }[] | undefined): EditableRule[] {
  return (rules ?? []).map((r) => ({ kind: r.kind, value: r.value, enabled: r.enabled ?? true }));
}

export function cleanRules(rules: EditableRule[]): RuleInput[] {
  return rules.filter((r) => r.value.trim()).map((r) => ({ kind: r.kind, value: r.value.trim(), enabled: r.enabled }));
}
