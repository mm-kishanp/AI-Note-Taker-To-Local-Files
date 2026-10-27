const fs = require('fs');
const path = require('path');
const { compileRules, resolveProject } = require('./match');
const { renderNotes, renderTranscript } = require('./markdown');

// Windows-safe file/folder name segment.
function safeSegment(s) {
  const cleaned = String(s || '')
    .replace(/[<>:"/\\|?*\x00-\x1f]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/[. ]+$/, '');
  return (cleaned || 'Untitled').slice(0, 120);
}

function fillTemplate(template, note, projectFolder) {
  const d = new Date(note.created_at);
  const pad = (n) => String(n).padStart(2, '0');
  const tokens = {
    project: projectFolder,
    yyyy: String(d.getFullYear()),
    MM: pad(d.getMonth() + 1),
    dd: pad(d.getDate()),
    title: note.title || 'Untitled',
    id: note.id,
  };
  // Each "/" separated part becomes its own folder; tokens are sanitised but a project folder
  // like "Clients/Acme" is allowed to create nested folders.
  return template
    .split('/')
    .flatMap((part) => {
      if (part === '{project}') return projectFolder.split(/[\\/]/).map(safeSegment);
      return [safeSegment(part.replace(/\{(\w+)\}/g, (m, k) => (k in tokens ? tokens[k] : m)))];
    })
    .filter(Boolean);
}

function loadState(file) {
  try {
    return JSON.parse(fs.readFileSync(file, 'utf8'));
  } catch {
    return { lastSyncAt: null, notes: {} };
  }
}

function saveState(file, state) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, JSON.stringify(state, null, 2));
}

// Relative path (from the notes/transcripts root) for this note, avoiding collisions with other notes.
function relativeFilePath(note, projectFolder, output, state) {
  const dirParts = fillTemplate(output.pathTemplate, note, projectFolder);
  const base = fillTemplate(output.fileNameTemplate, note, projectFolder).join(' ');
  const taken = new Set(
    Object.entries(state.notes).filter(([id]) => id !== note.id).map(([, s]) => s.relPath)
  );
  let rel = path.join(...dirParts, `${base}.md`);
  if (taken.has(rel)) rel = path.join(...dirParts, `${base} (${note.id.slice(-6)}).md`);
  return rel;
}

// Writes `content` to root/rel. If the note was previously written to a different path
// (e.g. its project changed), the old file is moved first so no duplicates are left behind.
function writeFile(root, rel, prevRel, content, dryRun) {
  const target = path.join(root, rel);
  const prev = prevRel ? path.join(root, prevRel) : null;
  const moved = prev && prev !== target && fs.existsSync(prev);
  if (dryRun) return { target, moved };
  fs.mkdirSync(path.dirname(target), { recursive: true });
  if (moved) fs.renameSync(prev, target);
  fs.writeFileSync(target, content, 'utf8');
  return { target, moved };
}

async function sync(client, config, opts = {}) {
  const { force = false, dryRun = false, since, noteId, log = console.log } = opts;
  const { output } = config;
  const rules = compileRules(config.projects);
  const state = loadState(config.sync.stateFile);
  const startedAt = new Date().toISOString();
  const stats = { checked: 0, written: 0, skipped: 0, unassigned: 0, failed: 0 };

  let summaries;
  if (noteId) {
    summaries = [{ id: noteId, updated_at: null }];
  } else {
    const filters = {};
    if (since) filters.created_after = since;
    else if (!force && state.lastSyncAt) filters.updated_after = state.lastSyncAt;
    else if (config.sync.createdAfter) filters.created_after = config.sync.createdAfter;
    summaries = client.listNotes(filters);
  }

  for await (const summary of summaries) {
    stats.checked++;
    if (summary.deleted_at) continue;
    const prev = state.notes[summary.id];
    if (!force && prev && summary.updated_at && prev.updated_at === summary.updated_at) {
      stats.skipped++;
      continue;
    }

    try {
      const note = await client.getNoteWithTranscript(summary.id);
      const { project, reasons } = resolveProject(note, rules, config.matching.sources);
      const projectName = project ? project.name : output.unassignedFolder;
      const projectFolder = project ? project.folder : output.unassignedFolder;
      if (!project) stats.unassigned++;

      const rel = relativeFilePath(note, projectFolder, output, state);
      const written = [];
      if (output.saveNotes) {
        const r = writeFile(output.notesDir, rel, prev && prev.relPath, renderNotes(note, project, { matchReasons: reasons }), dryRun);
        written.push(`notes${r.moved ? ' (moved)' : ''}`);
      }
      if (output.saveTranscripts) {
        // When notes and transcripts share one folder, give the transcript its own file name.
        const sameDir = path.resolve(output.notesDir) === path.resolve(output.transcriptsDir);
        const tr = (p) => (p && sameDir ? p.replace(/\.md$/, ' - Transcript.md') : p);
        const r = writeFile(output.transcriptsDir, tr(rel), tr(prev && prev.relPath), renderTranscript(note, project), dryRun);
        written.push(`transcript${r.moved ? ' (moved)' : ''}`);
      }

      log(`${dryRun ? '[dry-run] ' : ''}${projectName.padEnd(20)} ${rel}  [${written.join(', ')}]` +
        (reasons.length ? `\n${' '.repeat(21)}matched by: ${reasons.join('; ')}` : ''));

      if (!dryRun) {
        state.notes[note.id] = { updated_at: note.updated_at, project: projectName, relPath: rel };
        saveState(config.sync.stateFile, state);
      }
      stats.written++;
    } catch (err) {
      stats.failed++;
      log(`FAILED ${summary.id}: ${err.message}`);
    }
  }

  if (!dryRun && !noteId && stats.failed === 0) {
    state.lastSyncAt = startedAt;
    saveState(config.sync.stateFile, state);
  }
  return stats;
}

// Lists the speaker names / attendees seen in recent notes so they can be copied into project rules.
async function discoverPeople(client, { since, limit = 20, log = console.log } = {}) {
  const speakers = new Map();
  const attendees = new Map();
  let n = 0;
  for await (const summary of client.listNotes(since ? { created_after: since } : {})) {
    if (n++ >= limit) break;
    const note = await client.getNoteWithTranscript(summary.id);
    for (const item of note.transcript || []) {
      if (item.speaker && item.speaker.name) speakers.set(item.speaker.name, (speakers.get(item.speaker.name) || 0) + 1);
    }
    for (const a of note.attendees || []) {
      const key = `${a.name || ''} <${a.email}>`;
      attendees.set(key, (attendees.get(key) || 0) + 1);
    }
  }
  log(`Scanned ${Math.min(n, limit)} notes.\n\nIdentified transcript speakers (lines spoken):`);
  if (!speakers.size) log('  (none - Granola has not identified named speakers in these transcripts; use attendees/emails)');
  for (const [k, v] of [...speakers].sort((a, b) => b[1] - a[1])) log(`  ${k}  (${v})`);
  log('\nAttendees (meetings):');
  for (const [k, v] of [...attendees].sort((a, b) => b[1] - a[1])) log(`  ${k}  (${v})`);
}

module.exports = { sync, discoverPeople, fillTemplate, safeSegment };
