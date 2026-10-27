// Decides which project a note belongs to.
//
// Each project rule can list speaker names, emails (or "@domain.com"), and Granola folder names.
// Evidence is scored per project; the highest score wins, ties go to the project listed first.
//   - identified transcript speaker name: 10 points + 1 per line they spoke (the strongest signal)
//   - attendee name / email match:        5 points
//   - calendar invitee email match:       3 points
//   - Granola folder name match:          20 points

const norm = (s) => (s || '').toString().trim().toLowerCase().replace(/\s+/g, ' ');

function emailMatches(email, patterns) {
  const e = norm(email);
  if (!e) return false;
  return patterns.some((p) => (p.startsWith('@') ? e.endsWith(p) : e === p));
}

function nameMatches(name, names) {
  const n = norm(name);
  return !!n && names.includes(n);
}

function compileRules(projects) {
  return projects.map((p) => ({
    name: p.name,
    folder: p.folder || p.name,
    speakers: (p.speakers || []).map(norm),
    emails: (p.emails || []).map(norm),
    granolaFolders: (p.granolaFolders || []).map(norm),
  }));
}

function speakerLineCounts(transcript) {
  const counts = new Map();
  for (const item of transcript || []) {
    const name = item.speaker && item.speaker.name;
    if (name) counts.set(name, (counts.get(name) || 0) + 1);
  }
  return counts;
}

function scoreProject(rule, note, speakerCounts, sources) {
  let score = 0;
  const reasons = [];

  if (sources.includes('speakers')) {
    for (const [name, lines] of speakerCounts) {
      if (nameMatches(name, rule.speakers)) {
        score += 10 + lines;
        reasons.push(`speaker "${name}" (${lines} lines)`);
      }
    }
  }
  if (sources.includes('attendees')) {
    for (const a of note.attendees || []) {
      if (nameMatches(a.name, rule.speakers) || emailMatches(a.email, rule.emails)) {
        score += 5;
        reasons.push(`attendee ${a.name || a.email}`);
      }
    }
  }
  if (sources.includes('invitees') && note.calendar_event) {
    for (const inv of note.calendar_event.invitees || []) {
      if (emailMatches(inv.email, rule.emails)) {
        score += 3;
        reasons.push(`invitee ${inv.email}`);
      }
    }
  }
  if (sources.includes('granolaFolders')) {
    for (const f of note.folder_membership || []) {
      if (rule.granolaFolders.includes(norm(f.name))) {
        score += 20;
        reasons.push(`Granola folder "${f.name}"`);
      }
    }
  }
  return { score, reasons };
}

// Returns { project: rule | null, reasons: string[] }
function resolveProject(note, rules, sources) {
  const speakerCounts = speakerLineCounts(note.transcript);
  let best = null;
  for (const rule of rules) {
    const result = scoreProject(rule, note, speakerCounts, sources);
    if (result.score > 0 && (!best || result.score > best.score)) best = { rule, ...result };
  }
  return best ? { project: best.rule, reasons: best.reasons } : { project: null, reasons: [] };
}

module.exports = { compileRules, resolveProject, speakerLineCounts };
