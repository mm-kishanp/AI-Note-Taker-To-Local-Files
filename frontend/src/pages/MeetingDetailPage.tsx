import {
  Anchor, Badge, Button, Card, Group, List, Menu, Pagination, SimpleGrid, Stack, Switch, Table, Tabs, Text, Title, Tooltip,
} from '@mantine/core';
import { modals } from '@mantine/modals';
import {
  IconArrowLeft, IconBan, IconCloudDownload, IconDots, IconExternalLink, IconFileExport, IconFolderOpen, IconFileText,
  IconRefresh, IconSwitchHorizontal,
} from '@tabler/icons-react';
import { type ReactNode, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ConflictModal } from '../components/ConflictModal';
import { EmptyState } from '../components/EmptyState';
import { Markdown } from '../components/Markdown';
import { PageHeader } from '../components/PageHeader';
import { PathText } from '../components/PathText';
import { ProblemAlert } from '../components/ProblemAlert';
import { LoadingRows } from '../components/QueryState';
import { ReassignModal } from '../components/ReassignModal';
import { ReExportModal } from '../components/ReExportModal';
import { StatusBadge } from '../components/StatusBadge';
import { useAssignMeeting, useMeeting, useOpenExport, useReclassify, useRetryMeeting, useTranscript } from '../hooks/useMeetings';
import { ApiError } from '../services/api';
import type { MeetingDetailDto } from '../types/api';
import { formatDateTime, formatTime, humanize } from '../utils/format';
import { notifyError, notifyInfo, notifySuccess } from '../utils/notify';

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
        {label}
      </Text>
      <div>{children ?? <Text size="sm" c="dimmed">—</Text>}</div>
    </div>
  );
}

function Transcript({ id, total }: { id: string; total: number }) {
  const [page, setPage] = useState(1);
  const pageSize = 100;
  const q = useTranscript(id, page, pageSize, total > 0);
  if (total === 0) return <EmptyState title="No transcript" description="Granola has no transcript for this meeting (yet)." />;
  if (q.isLoading) return <LoadingRows rows={8} />;
  if (q.error) return <ProblemAlert error={q.error} onRetry={() => void q.refetch()} />;
  const pages = Math.max(1, Math.ceil((q.data?.total ?? 0) / pageSize));
  return (
    <Stack gap="xs">
      <Text size="sm" c="dimmed">
        {q.data?.total} segments · page {page} of {pages}
      </Text>
      <Stack gap={6} style={{ opacity: q.isPlaceholderData ? 0.6 : 1 }}>
        {q.data?.items.map((s, i) => (
          <Group key={i} gap="sm" align="flex-start" wrap="nowrap">
            <Text size="xs" c="dimmed" w={64} style={{ flexShrink: 0 }} ff="monospace">
              {formatTime(s.startTime)}
            </Text>
            <div style={{ minWidth: 0 }}>
              <Text size="sm" fw={600}>
                {s.speaker}
              </Text>
              <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>
                {s.text}
              </Text>
            </div>
          </Group>
        ))}
      </Stack>
      {pages > 1 && <Pagination total={pages} value={page} onChange={setPage} mt="sm" />}
    </Stack>
  );
}

function ClassificationCard({ d }: { d: MeetingDetailDto }) {
  const c = d.classification;
  return (
    <Card withBorder radius="md">
      <Title order={4} mb="sm">
        Classification
      </Title>
      <Stack gap="sm">
        <Group gap="xs">
          <StatusBadge kind="classification" value={c.status} srLabel="Classification" />
          <Badge variant="outline" color="gray" style={{ textTransform: 'none' }}>
            Method: {c.method}
          </Badge>
          <StatusBadge kind="certainty" value={c.certainty} variant="outline" srLabel="Certainty" />
          {c.tier && (
            <Badge variant="outline" color="gray" style={{ textTransform: 'none' }}>
              Tier: {humanize(c.tier)}
            </Badge>
          )}
          {c.needsReview && (
            <Badge color="yellow" style={{ textTransform: 'none' }}>
              Needs review
            </Badge>
          )}
        </Group>
        <Field label="Reason">
          <Text size="sm">{c.reason}</Text>
        </Field>
        {c.suggestedNextAction && (
          <Field label="Suggested next action">
            <Text size="sm">{c.suggestedNextAction}</Text>
          </Field>
        )}
        <Field label="Evidence">
          {c.evidence.length ? (
            <List size="sm" spacing={2}>
              {c.evidence.map((e, i) => (
                <List.Item key={i}>{e}</List.Item>
              ))}
            </List>
          ) : (
            <Text size="sm" c="dimmed">
              No matching evidence.
            </Text>
          )}
        </Field>
        {c.candidates.length > 0 && (
          <Field label="Candidate projects">
            <Group gap={6}>
              {c.candidates.map((p) => (
                <Anchor key={p.id} component={Link} to={`/projects/${p.id}`} size="sm">
                  {p.name}
                </Anchor>
              ))}
            </Group>
          </Field>
        )}
        {c.conflicts.length > 0 && (
          <Field label="Conflicts">
            <List size="sm" spacing={2}>
              {c.conflicts.map((e, i) => (
                <List.Item key={i}>{e}</List.Item>
              ))}
            </List>
          </Field>
        )}
        <Field label="Manual assignment">
          <Text size="sm">
            {c.manualAssignment === 'None' ? 'None (automatic)' : `${c.manualAssignment} · ${formatDateTime(c.manualAssignedAt)}`}
          </Text>
        </Field>
      </Stack>
    </Card>
  );
}

export function MeetingDetailPage() {
  const { id = '' } = useParams();
  const meeting = useMeeting(id);
  const reclassify = useReclassify();
  const retry = useRetryMeeting();
  const assign = useAssignMeeting();
  const openExport = useOpenExport();
  const [reassignOpen, setReassignOpen] = useState(false);
  const [reExportOpen, setReExportOpen] = useState(false);
  const [conflictRecord, setConflictRecord] = useState<string | null>(null);
  const [showSuperseded, setShowSuperseded] = useState(false);

  if (meeting.isLoading) {
    return (
      <>
        <PageHeader title="Loading meeting…" />
        <LoadingRows rows={10} />
      </>
    );
  }
  if (meeting.error || !meeting.data) {
    return (
      <>
        <PageHeader title="Meeting" />
        {meeting.error instanceof ApiError && meeting.error.status === 404 ? (
          <EmptyState title="Meeting not found" action={<Button component={Link} to="/meetings">Back to meetings</Button>} />
        ) : (
          <ProblemAlert error={meeting.error} onRetry={() => void meeting.refetch()} />
        )}
      </>
    );
  }

  const d = meeting.data;
  const m = d.meeting;
  // Superseded records are earlier locations (moved after reassignment) — history, not current files.
  const currentExports = d.exports.filter((x) => x.status !== 'Superseded');
  const supersededCount = d.exports.length - currentExports.length;
  const visibleExports = showSuperseded ? d.exports : currentExports;

  const doExclude = () =>
    modals.openConfirmModal({
      title: 'Exclude from export?',
      children: (
        <Text size="sm">
          “{m.title}” will no longer be exported or re-classified. Existing files stay on disk. You can undo this with Reassign →
          Automatic.
        </Text>
      ),
      labels: { confirm: 'Exclude', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () =>
        assign.mutate(
          { id, req: { kind: 'Excluded', projectId: null, note: null } },
          { onSuccess: () => notifySuccess('Meeting excluded.'), onError: (e) => notifyError(e) },
        ),
    });

  const doOpen = (recordId: string, folder: boolean) =>
    openExport.mutate({ id, recordId, folder }, { onError: (e) => notifyError(e, 'Could not open') });

  return (
    <>
      <Anchor component={Link} to="/meetings" size="sm">
        <Group gap={4}>
          <IconArrowLeft size={14} /> Meetings
        </Group>
      </Anchor>
      <PageHeader
        title={m.title || '(untitled)'}
        docTitle={m.title}
        description={
          <Group gap="xs" component="span">
            <span>{formatDateTime(d.meetingStart ?? m.meetingDate)}</span>
            {m.projectName && (
              <Anchor component={Link} to={`/projects/${m.projectId}`} size="sm">
                {m.projectName}
              </Anchor>
            )}
            <StatusBadge kind="classification" value={m.classificationStatus} srLabel="Classification" />
            <StatusBadge kind="export" value={m.exportStatus} srLabel="Export" />
          </Group>
        }
        actions={
          <>
            <Button leftSection={<IconSwitchHorizontal size={16} />} onClick={() => setReassignOpen(true)}>
              Reassign
            </Button>
            <Button
              variant="light"
              leftSection={<IconRefresh size={16} />}
              loading={reclassify.isPending}
              onClick={() =>
                reclassify.mutate(id, {
                  onSuccess: (r) => notifySuccess(`Classification: ${r.classification.status}. ${r.classification.reason}`, 'Re-classified'),
                  onError: (e) => notifyError(e),
                })
              }
            >
              Reclassify
            </Button>
            <Menu position="bottom-end">
              <Menu.Target>
                <Button variant="default" rightSection={<IconDots size={16} />}>
                  More
                </Button>
              </Menu.Target>
              <Menu.Dropdown>
                <Menu.Item
                  leftSection={<IconCloudDownload size={16} />}
                  onClick={() =>
                    retry.mutate(id, {
                      onSuccess: (r) => notifySuccess(r.message || 'Retry started in the background.', 'Retry started'),
                      onError: (e) =>
                        e instanceof ApiError && e.status === 409 ? notifyInfo(e.detail ?? 'A sync is already running.') : notifyError(e),
                    })
                  }
                >
                  Retry from Granola
                </Menu.Item>
                <Menu.Item leftSection={<IconFileExport size={16} />} onClick={() => setReExportOpen(true)}>
                  Re-export files…
                </Menu.Item>
                <Menu.Divider />
                <Menu.Item leftSection={<IconBan size={16} />} color="red" onClick={doExclude} disabled={m.classificationStatus === 'Excluded'}>
                  Exclude from export
                </Menu.Item>
              </Menu.Dropdown>
            </Menu>
          </>
        }
      />

      {(m.lastError || d.failureCount > 0) && (
        <ProblemAlert
          title={`Last sync problem${d.lastErrorCategory !== 'None' ? ` (${humanize(d.lastErrorCategory)})` : ''}`}
          error={new Error(
            `${m.lastError ?? 'The last attempt failed.'} Failures: ${d.failureCount}.${d.nextRetryAt ? ` Next automatic retry ${formatDateTime(d.nextRetryAt)}.` : ''}`,
          )}
        />
      )}

      <SimpleGrid cols={{ base: 1, lg: 2 }} spacing="md" mt="md">
        <Card withBorder radius="md">
          <Title order={4} mb="sm">
            Details
          </Title>
          <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="sm">
            <Field label="Start">{formatDateTime(d.meetingStart ?? m.meetingDate)}</Field>
            <Field label="End">{formatDateTime(d.meetingEnd)}</Field>
            <Field label="Owner">
              {d.ownerName || d.ownerEmail ? (
                <Text size="sm">
                  {d.ownerName} {d.ownerEmail && <Text span c="dimmed">&lt;{d.ownerEmail}&gt;</Text>}
                </Text>
              ) : null}
            </Field>
            <Field label="Calendar event">{d.calendarEventTitle ? <Text size="sm">{d.calendarEventTitle}</Text> : null}</Field>
            <Field label="Granola folders">
              {d.granolaFolders.length ? (
                <Group gap={4}>
                  {d.granolaFolders.map((f) => (
                    <Badge key={f} variant="light" color="gray" style={{ textTransform: 'none' }}>
                      {f}
                    </Badge>
                  ))}
                </Group>
              ) : null}
            </Field>
            <Field label="Source">
              {d.sourceUrl ? (
                <Anchor href={d.sourceUrl} target="_blank" rel="noopener noreferrer" size="sm">
                  <Group gap={4} component="span">
                    Open in Granola <IconExternalLink size={14} aria-hidden />
                  </Group>
                </Anchor>
              ) : null}
              <Text size="xs" c="dimmed" ff="monospace">
                {m.granolaNoteId}
              </Text>
            </Field>
            <Field label="Notes / transcript">
              <Group gap={4}>
                <StatusBadge kind="content" value={m.notesStatus} srLabel="Notes" />
                <StatusBadge kind="content" value={m.transcriptStatus} srLabel="Transcript" />
              </Group>
            </Field>
            <Field label="Source status">
              <Text size="sm" c={m.sourceStatus === 'Active' ? undefined : 'red'}>
                {m.sourceStatus}
              </Text>
            </Field>
            <Field label="Created / updated in Granola">
              <Text size="sm">
                {formatDateTime(d.createdAt)} / {formatDateTime(d.updatedAt)}
              </Text>
            </Field>
            <Field label="Last synced">{formatDateTime(m.lastSyncedAt)}</Field>
          </SimpleGrid>
        </Card>
        <ClassificationCard d={d} />
      </SimpleGrid>

      <SimpleGrid cols={{ base: 1, lg: 2 }} spacing="md" mt="md">
        <Card withBorder radius="md">
          <Title order={4} mb="sm">
            Participants ({d.participants.length})
          </Title>
          {d.participants.length === 0 ? (
            <Text size="sm" c="dimmed">
              No participants recorded.
            </Text>
          ) : (
            <Table.ScrollContainer minWidth={480}>
              <Table verticalSpacing={4}>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Name</Table.Th>
                    <Table.Th>Email</Table.Th>
                    <Table.Th>Role</Table.Th>
                    <Table.Th>Spoken segments</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {d.participants.map((p, i) => (
                    <Table.Tr key={i}>
                      <Table.Td>{p.name ?? '—'}</Table.Td>
                      <Table.Td>
                        <Text size="sm" ff="monospace">
                          {p.email ?? '—'}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Group gap={4}>
                          {p.isOwner && <Badge size="xs" style={{ textTransform: 'none' }}>Owner</Badge>}
                          {p.isAttendee && <Badge size="xs" variant="light" style={{ textTransform: 'none' }}>Attendee</Badge>}
                          {p.isInvitee && <Badge size="xs" variant="outline" color="gray" style={{ textTransform: 'none' }}>Invitee</Badge>}
                        </Group>
                      </Table.Td>
                      <Table.Td>{p.spokenSegments}</Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          )}
        </Card>
        <Card withBorder radius="md">
          <Title order={4} mb="sm">
            Identified speakers ({d.identifiedSpeakers.length})
          </Title>
          {d.identifiedSpeakers.length === 0 ? (
            <Text size="sm" c="dimmed">
              No speakers identified in the transcript.
            </Text>
          ) : (
            <List size="sm" spacing={2}>
              {d.identifiedSpeakers.map((s) => (
                <List.Item key={s.name}>
                  {s.name} <Text span c="dimmed">· {s.segments} segments</Text>
                </List.Item>
              ))}
            </List>
          )}
        </Card>
      </SimpleGrid>

      <Card withBorder radius="md" mt="md">
        <Tabs defaultValue="files" keepMounted={false}>
          <Tabs.List>
            <Tabs.Tab value="files">Exported files ({currentExports.length})</Tabs.Tab>
            <Tabs.Tab value="summary">Granola summary</Tabs.Tab>
            <Tabs.Tab value="notes">Owner notes</Tabs.Tab>
            <Tabs.Tab value="transcript">Transcript ({d.transcriptSegmentCount})</Tabs.Tab>
            <Tabs.Tab value="history">Sync history ({d.history.length})</Tabs.Tab>
          </Tabs.List>

          <Tabs.Panel value="files" pt="md">
            {supersededCount > 0 && (
              <Switch
                mb="sm"
                size="xs"
                label={`Show ${supersededCount} earlier location(s) (files moved or left in place after reassignment)`}
                checked={showSuperseded}
                onChange={(e) => setShowSuperseded(e.currentTarget.checked)}
              />
            )}
            {visibleExports.length === 0 ? (
              <EmptyState title="No exported files" description="This meeting has not been written to disk." />
            ) : (
              <Table.ScrollContainer minWidth={900}>
                <Table verticalSpacing="xs">
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Kind</Table.Th>
                      <Table.Th>Location</Table.Th>
                      <Table.Th>Path</Table.Th>
                      <Table.Th>Status</Table.Th>
                      <Table.Th>Last exported</Table.Th>
                      <Table.Th>Actions</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {visibleExports.map((x) => (
                      <Table.Tr key={x.id}>
                        <Table.Td>
                          {x.kind}
                          {x.role === 'Copy' && <Text size="xs" c="dimmed">copy</Text>}
                        </Table.Td>
                        <Table.Td>
                          {x.location}
                          {x.projectName && <Text size="xs" c="dimmed">{x.projectName}</Text>}
                        </Table.Td>
                        <Table.Td maw={420}>
                          <PathText path={x.absolutePath} size="xs" />
                          {!x.fileExists && x.status !== 'Superseded' && (
                            <Text size="xs" c="red">
                              File missing on disk
                            </Text>
                          )}
                          {x.lastError && (
                            <Text size="xs" c={x.status === 'Superseded' || x.status === 'KeptLocal' ? 'dimmed' : 'red'}>
                              {x.lastError}
                            </Text>
                          )}
                        </Table.Td>
                        <Table.Td>
                          <StatusBadge kind="export" value={x.status} srLabel="Export status" />
                          {x.conflictDetectedAt && (
                            <Text size="xs" c="dimmed">
                              since {formatDateTime(x.conflictDetectedAt)}
                            </Text>
                          )}
                        </Table.Td>
                        <Table.Td style={{ whiteSpace: 'nowrap' }}>{formatDateTime(x.lastExportedAt)}</Table.Td>
                        <Table.Td>
                          <Group gap={4} wrap="nowrap">
                            {x.status === 'Conflict' && (
                              <Button size="compact-xs" color="red" onClick={() => setConflictRecord(x.id)}>
                                Review conflict
                              </Button>
                            )}
                            <Tooltip label="Open file">
                              <Button
                                size="compact-xs"
                                variant="default"
                                disabled={!x.fileExists}
                                onClick={() => doOpen(x.id, false)}
                                aria-label={`Open ${x.kind} file`}
                              >
                                <IconFileText size={14} />
                              </Button>
                            </Tooltip>
                            <Tooltip label="Open folder">
                              <Button size="compact-xs" variant="default" onClick={() => doOpen(x.id, true)} aria-label={`Open ${x.kind} folder`}>
                                <IconFolderOpen size={14} />
                              </Button>
                            </Tooltip>
                          </Group>
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
            )}
          </Tabs.Panel>

          <Tabs.Panel value="summary" pt="md">
            {d.summary ? <Markdown content={d.summary} /> : <EmptyState title="No summary" description="Granola has not produced a summary for this meeting." />}
          </Tabs.Panel>
          <Tabs.Panel value="notes" pt="md">
            {d.notesContent ? <Markdown content={d.notesContent} /> : <EmptyState title="No owner notes" description="You did not write notes in Granola for this meeting." />}
          </Tabs.Panel>
          <Tabs.Panel value="transcript" pt="md">
            <Transcript id={id} total={d.transcriptSegmentCount} />
          </Tabs.Panel>
          <Tabs.Panel value="history" pt="md">
            {d.history.length === 0 ? (
              <EmptyState title="No sync history for this meeting" />
            ) : (
              <Table.ScrollContainer minWidth={700}>
                <Table verticalSpacing={4}>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Time</Table.Th>
                      <Table.Th>Operation</Table.Th>
                      <Table.Th>Outcome</Table.Th>
                      <Table.Th>Error</Table.Th>
                      <Table.Th>Run</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {d.history.map((h) => (
                      <Table.Tr key={h.id}>
                        <Table.Td style={{ whiteSpace: 'nowrap' }}>{formatDateTime(h.timestamp)}</Table.Td>
                        <Table.Td>{humanize(h.operation)}</Table.Td>
                        <Table.Td>{h.outcome}</Table.Td>
                        <Table.Td>
                          {h.errorCategory !== 'None' && (
                            <Text size="sm" c="red">
                              {humanize(h.errorCategory)}
                              {h.retryable ? ' (retryable)' : ''}
                            </Text>
                          )}
                          {h.message && <Text size="xs">{h.message}</Text>}
                        </Table.Td>
                        <Table.Td>
                          <Anchor component={Link} to={`/sync-history?run=${h.syncRunId}`} size="sm">
                            View run
                          </Anchor>
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Table.ScrollContainer>
            )}
          </Tabs.Panel>
        </Tabs>
      </Card>

      <ReassignModal
        meetingId={id}
        meetingTitle={m.title}
        opened={reassignOpen}
        onClose={() => setReassignOpen(false)}
        initialProjectId={m.projectId}
      />
      <ReExportModal meetingId={id} opened={reExportOpen} onClose={() => setReExportOpen(false)} />
      <ConflictModal meetingId={id} recordId={conflictRecord} onClose={() => setConflictRecord(null)} />
    </>
  );
}
