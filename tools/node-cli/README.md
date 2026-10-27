# Granola → Markdown sync

Pulls meeting notes and transcripts from the [Granola API](https://docs.granola.ai/introduction) and writes them as Markdown files. Each meeting is filed into a project folder, chosen from who spoke in the meeting.

Needs Node.js 18 or later and no npm packages.

## Setup

1. Create an API key in Granola: **Settings → Connectors → API keys → Create new key**. Tick the *Personal notes* scope.
2. Copy `config.example.json` to `config.json`.
3. Provide the key, either way:
   - set the environment variable `GRANOLA_API_KEY=grn_...` (preferred), or
   - put it in `"apiKey"` in `config.json`.
4. Find the exact speaker names Granola uses:
   ```
   node granola-sync.js --discover 30
   ```
   Copy those names and emails into `projects` in `config.json`.
5. Preview, then sync:
   ```
   node granola-sync.js --dry-run
   node granola-sync.js
   ```

## How a meeting gets its project

Each project rule is scored against the meeting, and the highest score wins. On a tie, the project listed first wins. If no rule matches, the meeting goes to `_Unassigned`.

| Evidence | Config field | Points |
|---|---|---|
| Identified transcript speaker | `speakers` | 10 + 1 per line spoken |
| Meeting attendee (name or email) | `speakers` / `emails` | 5 |
| Calendar invitee email | `emails` | 3 |
| Note is in a Granola folder | `granolaFolders` | 20 |

`emails` accepts exact addresses or whole domains (`"@acme.com"`). Name matching ignores case.

> Granola only fills in transcript speaker names when it can identify the speaker. Some meetings, especially from iOS or older recordings, only label speakers as "me" and "them". For those meetings the attendee and email rules decide the project, so list emails as well as names.

## Output

```
Notes/                                  Transcripts/
  Clients/Incent/2026/10/                 Clients/Incent/2026/10/
    2026-10-20 Sprint review.md             2026-10-20 Sprint review.md
  _Unassigned/2026/09/...                 _Unassigned/2026/09/...
```

- The `output` section of the config sets `notesDir` and `transcriptsDir`. They can be any two folders, absolute or relative to the config file. If both point at the same folder, transcripts are saved as `... - Transcript.md`.
- `pathTemplate` and `fileNameTemplate` accept `{project} {yyyy} {MM} {dd} {title} {id}`. A project's `folder` can be nested, e.g. `Clients/Incent`.
- Set `saveNotes` or `saveTranscripts` to `false` to turn either one off.
- Each file starts with YAML front matter: `granola_id`, project, date, attendees, Granola link, and `matched_by`, which says why the project was chosen. Each file therefore makes sense on its own after you move it.

## Re-running

- A normal run is incremental. It asks only for notes updated since the last run and skips notes that haven't changed. The state is kept in `.granola-sync-state.json`.
- After you change the project rules, run `node granola-sync.js --force`. Notes whose project changed are **moved** to the new folder, so no duplicates are left behind.
- Other options: `--since 2026-01-01` and `--note not_XXXXXXXXXXXXXX`.

To run it on a schedule, use Windows Task Scheduler with `node C:\workspace\Discussions\Granola\granola-sync.js`, working directory set to this folder.

`npm test` runs an offline smoke test.
