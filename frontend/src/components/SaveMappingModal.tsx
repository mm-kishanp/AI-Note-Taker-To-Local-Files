import { Alert, Anchor, Button, Checkbox, Group, Modal, NativeSelect, Stack, Table, Text, TextInput } from '@mantine/core';
import { modals } from '@mantine/modals';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useApplyMapping, useProjects } from '../hooks/useProjects';
import { useSaveMapping } from '../hooks/useUnassigned';
import { ApiError } from '../services/api';
import { RULE_KINDS, type RuleKind, type TestMappingResultDto } from '../types/api';
import { formatDate } from '../utils/format';
import { notifyInfo, notifySuccess, notifyError } from '../utils/notify';
import { RULE_KIND_INFO } from '../utils/rules';
import { ProblemAlert } from './ProblemAlert';
import { StatusBadge } from './StatusBadge';

export interface SaveMappingModalProps {
  meetingId: string;
  meetingTitle: string;
  opened: boolean;
  onClose: () => void;
  initialKind?: RuleKind;
  initialValue?: string;
  initialProjectId?: string | null;
}

/**
 * Saves a rule (participant email/name/speaker → project) from the review queue, then shows which other unassigned
 * meetings would now match and offers a bulk apply that requires explicit confirmation.
 */
export function SaveMappingModal({
  meetingId, meetingTitle, opened, onClose, initialKind = 'Email', initialValue = '', initialProjectId = null,
}: SaveMappingModalProps) {
  const projects = useProjects('', false);
  const save = useSaveMapping();
  const apply = useApplyMapping();
  const [projectId, setProjectId] = useState<string>(initialProjectId ?? '');
  const [kind, setKind] = useState<RuleKind>(initialKind);
  const [value, setValue] = useState(initialValue);
  const [assignThis, setAssignThis] = useState(true);
  const [preview, setPreview] = useState<TestMappingResultDto | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (!opened) return;
    setProjectId(initialProjectId ?? '');
    setKind(initialKind);
    setValue(initialValue);
    setAssignThis(true);
    setPreview(null);
    setSelected(new Set());
    save.reset();
    apply.reset();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [opened, initialKind, initialValue, initialProjectId]);

  const others = (preview?.items ?? []).filter((i) => i.meetingId !== meetingId && i.changes);
  const err = save.error instanceof ApiError ? save.error : null;

  const submit = () =>
    save.mutate(
      { id: meetingId, req: { projectId, kind, value: value.trim(), assignThisMeeting: assignThis } },
      {
        onSuccess: (r) => {
          setPreview(r);
          setSelected(new Set(r.items.filter((i) => i.meetingId !== meetingId && i.changes && !i.isManual).map((i) => i.meetingId)));
          notifySuccess(assignThis ? 'Rule saved and this meeting assigned.' : 'Rule saved.');
        },
      },
    );

  const projectName = projects.data?.find((p) => p.id === projectId)?.name ?? 'the project';

  const confirmApply = () =>
    modals.openConfirmModal({
      title: `Apply to ${selected.size} other meeting(s)?`,
      children: (
        <Text size="sm">
          The selected meetings will be re-classified with the saved rules of “{projectName}” and re-exported in the background.
          Manual assignments are kept.
        </Text>
      ),
      labels: { confirm: 'Apply mapping', cancel: 'Cancel' },
      onConfirm: () =>
        apply.mutate(
          { id: projectId, req: { meetingIds: Array.from(selected), confirm: true } },
          {
            onSuccess: (r) => {
              notifySuccess(r.message || 'Re-classification started.', 'Started');
              onClose();
            },
            onError: (e) => (e instanceof ApiError && e.status === 409 ? notifyInfo(e.detail ?? 'A sync is already running.') : notifyError(e)),
          },
        ),
    });

  return (
    <Modal opened={opened} onClose={onClose} title={`Save mapping from “${meetingTitle}”`} size="lg">
      {!preview ? (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            submit();
          }}
        >
          <Stack gap="sm">
            <NativeSelect
              label="Project"
              required
              value={projectId}
              onChange={(e) => setProjectId(e.currentTarget.value)}
              data={[{ value: '', label: projects.isLoading ? 'Loading…' : 'Choose a project' }, ...(projects.data ?? []).map((p) => ({ value: p.id, label: p.name }))]}
              error={err?.fieldError('projectId')}
            />
            <NativeSelect
              label="Rule kind"
              value={kind}
              onChange={(e) => setKind(e.currentTarget.value as RuleKind)}
              data={RULE_KINDS.map((k) => ({ value: k, label: RULE_KIND_INFO[k].label }))}
              description={RULE_KIND_INFO[kind].help}
            />
            <TextInput
              label="Value"
              required
              value={value}
              onChange={(e) => setValue(e.currentTarget.value)}
              placeholder={RULE_KIND_INFO[kind].placeholder}
              error={err?.fieldError('rule') ?? err?.fieldError('value')}
              ff="monospace"
            />
            <Checkbox label="Also assign this meeting to the project" checked={assignThis} onChange={(e) => setAssignThis(e.currentTarget.checked)} />
            {save.error && <ProblemAlert error={save.error} />}
            <Group justify="flex-end">
              <Button variant="default" onClick={onClose}>
                Cancel
              </Button>
              <Button type="submit" loading={save.isPending} disabled={!projectId || !value.trim()}>
                Save mapping
              </Button>
            </Group>
          </Stack>
        </form>
      ) : (
        <Stack gap="sm">
          <Alert color="green" variant="light" title="Mapping saved">
            {RULE_KIND_INFO[kind].label} <b>{value}</b> → {projectName}. Evaluated {preview.evaluated} meeting(s); {preview.wouldMatch} would match.
          </Alert>
          {others.length === 0 ? (
            <Text size="sm" c="dimmed" data-testid="no-other-matches">
              No other unassigned meetings would change with this mapping.
            </Text>
          ) : (
            <>
              <Text size="sm">
                {others.length} other meeting(s) would now be assigned. Select the ones to update:
              </Text>
              <Table verticalSpacing={4} data-testid="mapping-preview">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th w={40} />
                    <Table.Th>Meeting</Table.Th>
                    <Table.Th>Current</Table.Th>
                    <Table.Th>Proposed</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {others.map((i) => (
                    <Table.Tr key={i.meetingId}>
                      <Table.Td>
                        <Checkbox
                          aria-label={`Select ${i.title}`}
                          checked={selected.has(i.meetingId)}
                          disabled={i.isManual}
                          onChange={() =>
                            setSelected((s) => {
                              const n = new Set(s);
                              if (n.has(i.meetingId)) n.delete(i.meetingId);
                              else n.add(i.meetingId);
                              return n;
                            })
                          }
                        />
                      </Table.Td>
                      <Table.Td>
                        <Anchor component={Link} to={`/meetings/${i.meetingId}`} size="sm" onClick={onClose}>
                          {i.title}
                        </Anchor>
                        <Text size="xs" c="dimmed">
                          {formatDate(i.meetingDate)}
                          {i.isManual ? ' · manual assignment (kept)' : ''}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <StatusBadge kind="classification" value={i.currentStatus} />
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{i.proposedProject ?? i.proposedStatus}</Text>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </>
          )}
          {apply.error && <ProblemAlert error={apply.error} />}
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              Done
            </Button>
            {others.length > 0 && (
              <Button onClick={confirmApply} disabled={selected.size === 0} loading={apply.isPending}>
                Apply to {selected.size} meeting(s)
              </Button>
            )}
          </Group>
        </Stack>
      )}
    </Modal>
  );
}
