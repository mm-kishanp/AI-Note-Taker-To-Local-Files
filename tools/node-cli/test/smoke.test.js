// Offline smoke test: runs the sync against a fake client and checks the files it writes.
const assert = require('assert');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { sync } = require('../src/sync');

const t = (s) => `2026-10-20T10:00:${String(s).padStart(2, '0')}Z`;
const line = (name, text, s, attribution = 'them') => ({
  speaker: { source: attribution === 'me' ? 'microphone' : 'speaker', attribution, ...(name && { name }) },
  text, start_time: t(s), end_time: t(s + 1),
});

const NOTES = [
  {
    id: 'not_aaaaaaaaaaaaaa', title: 'Incent sprint review', created_at: '2026-10-20T10:00:00Z', updated_at: 'u1',
    owner: { name: 'K Parmar', email: 'k@nuuvia.com' }, attendees: [], folder_membership: [], calendar_event: null,
    summary_markdown: '## Decisions\n- Ship v2', private_notes_markdown: 'ask about budget',
    transcript: [line(null, 'Hi all', 0, 'me'), line('John Smith', 'Hello', 2), line('John Smith', 'Ready to demo.', 4)],
  },
  {
    id: 'not_bbbbbbbbbbbbbb', title: 'Website: kickoff?', created_at: '2026-09-02T09:00:00Z', updated_at: 'u1',
    owner: { name: 'K Parmar', email: 'k@nuuvia.com' }, attendees: [{ name: 'Alice B', email: 'alice@example.com' }],
    folder_membership: [], calendar_event: null, summary_text: 'Kickoff', transcript: [line(null, 'Let us start', 0)],
  },
  {
    id: 'not_cccccccccccccc', title: 'Random chat', created_at: '2026-09-03T09:00:00Z', updated_at: 'u1',
    owner: { name: 'K Parmar', email: 'k@nuuvia.com' }, attendees: [], folder_membership: [], calendar_event: null,
    summary_text: 'Nothing', transcript: [line('Stranger', 'hey', 0)],
  },
];

const client = {
  calls: 0,
  async *listNotes() { for (const n of NOTES) yield { id: n.id, updated_at: n.updated_at, deleted_at: null }; },
  async getNoteWithTranscript(id) { this.calls++; return structuredClone(NOTES.find((n) => n.id === id)); },
};

(async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'granola-test-'));
  const config = {
    output: {
      notesDir: path.join(root, 'Notes'), transcriptsDir: path.join(root, 'Transcripts'),
      unassignedFolder: '_Unassigned', pathTemplate: '{project}/{yyyy}/{MM}',
      fileNameTemplate: '{yyyy}-{MM}-{dd} {title}', saveNotes: true, saveTranscripts: true,
    },
    matching: { sources: ['speakers', 'attendees', 'invitees', 'granolaFolders'] },
    sync: { stateFile: path.join(root, 'state.json') },
    projects: [
      { name: 'Incent', folder: 'Clients/Incent', speakers: ['john smith'] },
      { name: 'Website', emails: ['@example.com'] },
    ],
  };
  const quiet = () => {};
  const exists = (...p) => fs.existsSync(path.join(root, ...p));

  let stats = await sync(client, config, { log: quiet });
  assert.deepStrictEqual([stats.written, stats.unassigned, stats.failed], [3, 1, 0]);
  assert(exists('Notes/Clients/Incent/2026/10/2026-10-20 Incent sprint review.md'), 'speaker routing');
  assert(exists('Transcripts/Clients/Incent/2026/10/2026-10-20 Incent sprint review.md'));
  assert(exists('Notes/Website/2026/09/2026-09-02 Website kickoff.md'), 'attendee email routing + sanitised name');
  assert(exists('Notes/_Unassigned/2026/09/2026-09-03 Random chat.md'), 'unassigned fallback');

  const transcript = fs.readFileSync(path.join(root, 'Transcripts/Clients/Incent/2026/10/2026-10-20 Incent sprint review.md'), 'utf8');
  assert(transcript.includes('**K Parmar** `00:00`'), 'owner label');
  assert(transcript.includes('**John Smith** `00:02`  \nHello Ready to demo.'), 'merged consecutive lines');
  const notes = fs.readFileSync(path.join(root, 'Notes/Clients/Incent/2026/10/2026-10-20 Incent sprint review.md'), 'utf8');
  assert(notes.includes('## My Notes') && notes.includes('project: "Incent"'));

  // Second run: nothing changed, nothing re-downloaded.
  client.calls = 0;
  stats = await sync(client, config, { log: quiet });
  assert.strictEqual(client.calls, 0, 'unchanged notes are skipped');

  // Add a rule for "Stranger" and force: the note moves out of _Unassigned (no duplicate left behind).
  config.projects.push({ name: 'Misc', speakers: ['Stranger'] });
  stats = await sync(client, config, { force: true, log: quiet });
  assert(exists('Notes/Misc/2026/09/2026-09-03 Random chat.md'));
  assert(!exists('Notes/_Unassigned/2026/09/2026-09-03 Random chat.md'), 're-filed, not duplicated');

  // Same folder for notes and transcripts: transcript gets its own file name.
  config.output.transcriptsDir = config.output.notesDir;
  await sync(client, config, { force: true, log: quiet });
  assert(exists('Notes/Misc/2026/09/2026-09-03 Random chat - Transcript.md'));

  fs.rmSync(root, { recursive: true, force: true });
  console.log('All smoke tests passed.');
})().catch((e) => { console.error(e); process.exit(1); });
