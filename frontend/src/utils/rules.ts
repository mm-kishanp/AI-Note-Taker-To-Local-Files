import type { RuleKind } from '../types/api';

export interface RuleKindInfo {
  label: string;
  placeholder: string;
  help: string;
}

export const RULE_KIND_INFO: Record<RuleKind, RuleKindInfo> = {
  Email: {
    label: 'Email address',
    placeholder: 'jane@client.com',
    help: 'Matches a participant or calendar invitee with this exact email address. Most reliable signal.',
  },
  EmailDomain: {
    label: 'Email domain',
    placeholder: '@client.com',
    help: 'Matches any participant whose email ends with this domain. Good for external clients; avoid your own company domain.',
  },
  ParticipantName: {
    label: 'Participant name',
    placeholder: 'Jane Doe',
    help: 'Matches an attendee/invitee by display name (case-insensitive), and also a transcript speaker with that name. Use when emails are not available.',
  },
  SpeakerName: {
    label: 'Speaker name',
    placeholder: 'Jane Doe',
    help: 'Matches a speaker identified in the transcript with at least the minimum number of spoken segments.',
  },
  GranolaFolder: {
    label: 'Granola folder',
    placeholder: 'Client X',
    help: 'Matches meetings filed in this Granola folder (exact folder name or folder id).',
  },
  TitleKeyword: {
    label: 'Title keyword',
    placeholder: 'Project Phoenix',
    help: 'Matches when the meeting or calendar title contains this text.',
  },
  Keyword: {
    label: 'Content keyword',
    placeholder: 'phoenix',
    help: 'Matches when this text appears in the title or in the Granola summary. Weakest signal — use as a tie-breaker.',
  },
};

export const TEMPLATE_VARIABLES = [
  '{ProjectSlug}', '{ProjectName}', '{ClientName}', '{Year}', '{Month}', '{Day}', '{MeetingDate}', '{MeetingId}', '{MeetingTitle}',
] as const;
