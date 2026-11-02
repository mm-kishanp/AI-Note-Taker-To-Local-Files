# Open Questions

Last reviewed: 2026-10-28

| ID | Question | Why it matters | Evidence already checked | Blocking? | Suggested next step |
| --- | --- | --- | --- | --- | --- |
| Q-001 | Is `master` the intended default branch (so CI's `push: branches: [main]` is wrong), and are CI checks required on pull requests? | Pushes to `master` currently run no CI (RF-002); merge gating unknown | `ci.yml:3-6`; `git status` shows `master...origin/master`; branch protection not visible | No | Owner confirms; then PIDA-002 |
| Q-002 | Were the `frontend/src/**/*.js` files, `vite.config.js` and `tsconfig.tsbuildinfo` committed on purpose? | They shadow the TypeScript sources (RF-001) | Commit `1bb6c87`; files are tsc output; no script emits them | No (fix is reversible) | Confirm, then PIDA-001 |
| Q-003 | Should `tools/node-cli` be deleted, frozen (read-only, with a deprecation note), or kept in sync? | Duplicate logic and a plain-text key path | `README.md:43`, sub-audit | No | Owner decides; PIDA-016 |
| Q-004 | Is it intentional that scheduled sync requires a Granola key (Fireflies-only users never auto-sync)? | Possible functional bug | `SynchronizationWorker.cs:64` vs `SyncEngine.cs:72-74` | No | Confirm; PIDA-004 |
| Q-005 | Is the Docker/server mode used in practice, and by whom (only the owner, or others on a network)? | Sets the priority of SEC-001, SEC-002, SEC-005 | `docker-compose.yml`, `docs/INSTALLATION.md` | No | Ask the owner |
| Q-006 | What is the supported upgrade/downgrade path given migrations auto-apply at start-up and there are no versions? | Safe rollback | `Program.cs:51`; no tags | No | Decide on versioning and pre-migration backup (PIDA-018) |
| Q-007 | Are `POST /api/sync/retry-failed`, `POST /api/projects/{id}/rules` and the `useGranolaFolders` hook planned UI features or dead code? Should projects/rules be deletable? | Dead code vs missing UI | Sub-audit grep; no delete endpoints | No | Owner decides |
| Q-008 | Is the app expected to stay single-user? | Multi-user would need auth, per-user data and secrets | `Middleware.cs`, no auth framework | No | Confirm scope |
| Q-009 | Can Granola note IDs and Fireflies transcript IDs ever collide? | The unique index and lookups ignore `Source` (RF-006) | `AppDbContext.cs:77` | No | Check ID formats in `docs/GRANOLA_API.md`/`docs/FIREFLIES_API.md`; PIDA-011 |
| Q-010 | Should old sync runs, run items and meetings deleted at the source ever be purged from the database? | The DB keeps full meeting content indefinitely | No purge code found | No | Decide retention policy |
| Q-011 | Should manual meeting actions wait for, or be blocked during, a running sync? | Possible race on the same meeting (RF-019) | `MeetingService.cs:210-220` | No | Decide; then add a guard or a test |
| Q-012 | Do real WhisperAI `utterances`/`sentences` items use `speaker`, `text`, `start`, `end` with times in milliseconds? Does `GET /transcript/{id}` ever return `created`? | The OpenAPI schema leaves these untyped; the mapper relies on the docs example (`"start": 240, "end": 26560`) | `whisperai-openapi.json` (`Transcript.utterances`, `SentencesResponse.sentences` are untyped objects); docs translation example | No | Sync one real transcript with a `wai_` key and compare the transcript Markdown timings with the audio |
