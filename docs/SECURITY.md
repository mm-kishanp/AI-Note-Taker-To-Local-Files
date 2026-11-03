# Security

Meeting notes and transcripts are often confidential. The app is designed to run locally, for one user.

## Credentials

- **Windows desktop:** the Granola key (and the optional Anthropic key) is stored in **Windows Credential Manager** as generic credentials (`Chroniq:GranolaApiKey`), encrypted by the OS per user. You can see or remove it under Control Panel → Credential Manager → Windows Credentials.
- **Servers and containers:** the app cannot write secrets. Supply them as `GRANOLA_API_KEY` / `FIREFLIES_API_KEY` / `ANTHROPIC_API_KEY` environment variables, or through a secrets manager mapped to configuration.
- Keys are never stored in the database or in settings JSON, never sent to the browser (the UI shows only a masked form such as `grn_••••••••abcd`), and never logged: the file logger redacts `grn_…`, `sk-ant-…` and `Bearer …` patterns.
- A new key is validated against Granola before it is kept. If Granola rejects it with 401, the previous key is restored.
- Revoke a key in Granola (Settings → Connectors → API keys), then remove it in the app (Settings → Connections → Remove key).

> The earlier Node script (`tools/node-cli`) read the key from a plain-text `config.json`. That file is git-ignored. Once the key is stored in the app, delete the key from that file, or delete the file.

## Network exposure

- By default the app listens only on `127.0.0.1:5080`. Requests from other machines are refused unless `Security:ApiToken` is set (or `Security:AllowRemoteWithoutToken`, which is not recommended).
- With `Security:ApiToken` set, every `/api` call except `/api/health` needs `Authorization: Bearer <token>`. The token is compared in constant time. Use HTTPS (a reverse proxy) for anything beyond localhost.
- **CSRF:** every state-changing `/api` request must carry `X-Chroniq-Request: 1`. A malicious website cannot add that header without a CORS preflight, and CORS only allows the configured origins (by default, the local Vite dev server).
- `AllowedHosts` is restricted to `localhost` and `127.0.0.1`, which also blocks DNS-rebinding attacks.
- Errors use RFC 7807 problem details. Production responses carry no stack traces, and every response includes a correlation ID for log lookup.

## File system

- All export paths are resolved under the configured roots and checked before use. Templates are validated, and template values are sanitised into single path segments.
- The read-only browser endpoints (tree, file contents) and "open in editor/explorer" work only within the export roots. Only `.md` files can be read. Symbolic links and junctions are not followed when listing.
- "Open" starts Explorer, or the OS default handler, with the path passed as a single argument and never through a shell.
- The app never deletes your files. It writes only its own generated files (atomic temp-and-rename), refuses to overwrite files it did not create, keeps your edits, leaves the old root in place after a migration, and moves only unmodified generated files when a meeting is reassigned.
- The folder-picker endpoint lists folder **names** under a path you browse to. Like the rest of the API, it is available only to local callers (or token holders).
- Put the export root and `data/` somewhere only you can read. Both contain meeting content.

## Untrusted content

Transcripts and notes are treated as data, never as instructions:

- Markdown rendering escapes HTML and block markers in transcripts. The UI renders Markdown with raw HTML disabled.
- Nothing in a transcript can change settings, paths, credentials or endpoints. No code or commands from meetings are ever run.
- The optional AI classifier wraps meeting data in tags and tells the model to ignore instructions inside it. It accepts only a JSON answer that names an existing project ID or null.

## AI data sharing (opt-in)

Nothing is sent to an AI provider unless you enable AI-assisted classification and store an Anthropic key. When it is enabled, each meeting that no rule could classify sends:

- its title;
- participant names and emails;
- identified speaker names;
- Granola folder names;
- your project list (ID, name, client, description).

Granola's summary is sent only if *Include summary* is on. Transcripts are never sent.

## Reporting

The app has no telemetry. Logs stay in `data/logs` and are pruned after the retention period.
