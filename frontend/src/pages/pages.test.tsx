import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { ReassignModal } from '../components/ReassignModal';
import { SaveMappingModal } from '../components/SaveMappingModal';
import { credential, meeting, project } from '../test/fixtures';
import { mockFetch, paged, renderWithProviders, route, type RecordedCall } from '../test/utils';
import { MeetingsPage } from './MeetingsPage';
import { SettingsPage } from './SettingsPage';

const lookups = [
  route('GET /api/projects', [project()]),
  route('GET /api/clients', []),
];

describe('MeetingsPage', () => {
  it('renders meetings from the API and sends filter changes to the server', async () => {
    const { calls } = mockFetch([
      route('GET /api/meetings', (c: RecordedCall) =>
        paged(c.url.searchParams.get('search') === 'budget' ? [] : [meeting(), meeting({ id: 'm2', title: 'Sprint planning', classificationStatus: 'Assigned', projectName: 'Motor Dealer App' })])),
      ...lookups,
    ]);
    renderWithProviders(<MeetingsPage />, { path: '/meetings', initialEntry: '/meetings' });

    expect(await screen.findByText('Client Requirement Discussion')).toBeInTheDocument();
    expect(screen.getByText('Sprint planning')).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText('Search'), 'budget');
    await waitFor(() =>
      expect(calls.some((c) => c.url.pathname === '/api/meetings' && c.url.searchParams.get('search') === 'budget')).toBe(true));
    await waitFor(() => expect(screen.queryByText('Sprint planning')).not.toBeInTheDocument());
  });
});

describe('ReassignModal', () => {
  it('shows the destination preview before the assignment is confirmed', async () => {
    const detail = { meeting: meeting({ classificationStatus: 'Shared' }) };
    const { calls } = mockFetch([
      route('GET /api/meetings/m1/destination-preview', [
        { location: 'Shared', projectName: null, notesPath: 'C:/K/Shared/Notes/2026-10-26-Client.md', transcriptPath: 'C:/K/Shared/Transcripts/2026-10-26-Client.md' },
      ]),
      route('POST /api/meetings/m1/assign-project', detail),
      ...lookups,
    ]);
    renderWithProviders(<ReassignModal meetingId="m1" meetingTitle="Client" opened onClose={() => {}} initialKind="Shared" />);

    expect(await screen.findByText('C:/K/Shared/Notes/2026-10-26-Client.md')).toBeInTheDocument();
    expect(calls.find((c) => c.url.pathname.endsWith('/assign-project'))).toBeUndefined();

    await userEvent.click(screen.getByRole('button', { name: 'Confirm assignment' }));
    await waitFor(() => {
      const assign = calls.find((c) => c.url.pathname === '/api/meetings/m1/assign-project');
      expect(assign?.body).toMatchObject({ kind: 'Shared' });
      expect(assign?.headers['X-Chroniq-Request']).toBe('1');
    });
    const previewIdx = calls.findIndex((c) => c.url.pathname.endsWith('/destination-preview'));
    const assignIdx = calls.findIndex((c) => c.url.pathname.endsWith('/assign-project'));
    expect(previewIdx).toBeLessThan(assignIdx);
  });
});

describe('Settings: Granola credentials', () => {
  it('shows only the masked key and submits a new key with validation', async () => {
    const { calls } = mockFetch([
      route('GET /api/settings/credentials/granola', credential()),
      route('PUT /api/settings/credentials/granola', credential({ maskedKey: 'grn_••••••••9999' })),
    ]);
    renderWithProviders(<SettingsPage />);

    expect(await screen.findByText('grn_••••••••wxyz')).toBeInTheDocument();

    const input = screen.getByLabelText('Replace API key');
    expect(input).toHaveAttribute('type', 'password');
    await userEvent.type(input, 'grn_new_secret_key_9999');
    await userEvent.click(screen.getByRole('button', { name: /save & verify/i }));

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true));
    const put = calls.find((c) => c.method === 'PUT')!;
    expect(put.body).toEqual({ apiKey: 'grn_new_secret_key_9999', validate: true });
    expect(await screen.findByText('grn_••••••••9999')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByDisplayValue('grn_new_secret_key_9999')).not.toBeInTheDocument());
    expect(document.body.textContent).not.toContain('grn_new_secret_key_9999');
  });

  it('configures the Fireflies key separately from the Granola key', async () => {
    const { calls } = mockFetch([
      route('GET /api/settings/credentials/granola', credential()),
      route('GET /api/settings/credentials/fireflies', credential({ configured: false, maskedKey: null, source: null, lastCheckSucceeded: null })),
      route('PUT /api/settings/credentials/fireflies', credential({ maskedKey: 'ff_k••••••••7890' })),
    ]);
    renderWithProviders(<SettingsPage />);

    expect(await screen.findByRole('heading', { name: 'Fireflies connection' })).toBeInTheDocument();
    const input = await screen.findByLabelText('API key');
    await userEvent.type(input, 'ff_key_1234567890');
    const fireflies = screen.getByRole('heading', { name: 'Fireflies connection' }).closest('.mantine-Paper-root') as HTMLElement;
    await userEvent.click(within(fireflies).getByRole('button', { name: /save & verify/i }));

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true));
    const put = calls.find((c) => c.method === 'PUT')!;
    expect(put.url.pathname).toBe('/api/settings/credentials/fireflies');
    expect(put.body).toEqual({ apiKey: 'ff_key_1234567890', validate: true });
    expect(await screen.findByText('ff_k••••••••7890')).toBeInTheDocument();
  });

  it('configures the WhisperAI key separately from the other keys', async () => {
    const { calls } = mockFetch([
      route('GET /api/settings/credentials/granola', credential()),
      route('GET /api/settings/credentials/whisperai', credential({ configured: false, maskedKey: null, source: null, lastCheckSucceeded: null })),
      route('PUT /api/settings/credentials/whisperai', credential({ maskedKey: 'wai_••••••••7890' })),
    ]);
    renderWithProviders(<SettingsPage />);

    expect(await screen.findByRole('heading', { name: 'WhisperAI connection' })).toBeInTheDocument();
    const input = await screen.findByLabelText('API key');
    await userEvent.type(input, 'wai_key_1234567890');
    const whisperAi = screen.getByRole('heading', { name: 'WhisperAI connection' }).closest('.mantine-Paper-root') as HTMLElement;
    await userEvent.click(within(whisperAi).getByRole('button', { name: /save & verify/i }));

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true));
    const put = calls.find((c) => c.method === 'PUT')!;
    expect(put.url.pathname).toBe('/api/settings/credentials/whisperai');
    expect(put.body).toEqual({ apiKey: 'wai_key_1234567890', validate: true });
    expect(await screen.findByText('wai_••••••••7890')).toBeInTheDocument();
  });
});

describe('SaveMappingModal', () => {
  it('previews other matching meetings and requires confirmation before bulk apply', async () => {
    const p = project();
    const { calls } = mockFetch([
      route('POST /api/unassigned/m1/mappings', {
        evaluated: 3, wouldMatch: 2, wouldChange: 1,
        items: [{
          meetingId: 'm2', title: 'Dealer follow-up', meetingDate: '2026-10-27T10:00:00Z', currentStatus: 'Unassigned', currentProject: null,
          proposedStatus: 'Assigned', proposedProject: p.name, changes: true, isManual: false, reason: 'Verified speaker match',
        }],
      }),
      route(`POST /api/projects/${p.id}/apply-mapping`, { started: true, runId: 'r1', message: 'Synchronization started.' }, 202),
      route('GET /api/projects', [p]),
    ]);
    renderWithProviders(
      <SaveMappingModal meetingId="m1" meetingTitle="Client call" opened onClose={() => {}} initialKind="SpeakerName" initialValue="Alice Client" initialProjectId={p.id} />,
    );

    await screen.findByDisplayValue('Alice Client');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Save mapping' })).toBeEnabled());
    await userEvent.click(screen.getByRole('button', { name: 'Save mapping' }));

    expect(await screen.findByText('Dealer follow-up')).toBeInTheDocument();
    expect(calls.some((c) => c.url.pathname.endsWith('/apply-mapping'))).toBe(false);

    await userEvent.click(screen.getByRole('button', { name: /apply to 1 meeting/i }));
    expect(calls.some((c) => c.url.pathname.endsWith('/apply-mapping'))).toBe(false); // confirmation dialog first
    await userEvent.click(await screen.findByRole('button', { name: 'Apply mapping' }));

    await waitFor(() => {
      const apply = calls.find((c) => c.url.pathname.endsWith('/apply-mapping'));
      expect(apply?.body).toEqual({ meetingIds: ['m2'], confirm: true });
    });
  });
});
