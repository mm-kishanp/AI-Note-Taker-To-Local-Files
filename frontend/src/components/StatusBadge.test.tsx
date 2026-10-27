import { MantineProvider } from '@mantine/core';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { statusColor, statusLabel, statusTone } from '../utils/status';
import { StatusBadge } from './StatusBadge';

describe('status mapping', () => {
  it.each([
    ['classification', 'Assigned', 'success'],
    ['classification', 'NeedsReview', 'warning'],
    ['export', 'Conflict', 'danger'],
    ['export', 'LocallyModified', 'warning'],
    ['content', 'Failed', 'danger'],
    ['sync', 'PartiallySucceeded', 'warning'],
    ['certainty', 'Certain', 'success'],
  ] as const)('%s %s -> %s', (kind, value, tone) => {
    expect(statusTone(kind, value)).toBe(tone);
  });

  it('falls back to neutral for unknown or empty values', () => {
    expect(statusTone('export', 'SomethingNew')).toBe('neutral');
    expect(statusColor('export', null)).toBe('gray');
  });

  it('uses readable labels', () => {
    expect(statusLabel('NeedsReview')).toBe('Needs review');
    expect(statusLabel('KeptLocal')).toBe('Kept local');
  });

  it('renders the text so color is never the only signal', () => {
    render(
      <MantineProvider>
        <StatusBadge kind="export" value="Conflict" srLabel="Export status" />
      </MantineProvider>,
    );
    expect(screen.getByText('Conflict')).toBeInTheDocument();
    expect(screen.getByLabelText('Export status: Conflict')).toHaveAttribute('data-tone', 'danger');
  });
});
