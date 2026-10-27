// Renders a Granola note into two Markdown documents: notes (summary + private notes) and transcript.

function yamlValue(v) {
  if (v === null || v === undefined) return '""';
  if (Array.isArray(v)) return `[${v.map(yamlValue).join(', ')}]`;
  return JSON.stringify(String(v));
}

function frontMatter(fields) {
  const lines = Object.entries(fields).map(([k, v]) => `${k}: ${yamlValue(v)}`);
  return `---\n${lines.join('\n')}\n---\n`;
}

function speakerLabel(speaker, note) {
  if (!speaker) return 'Unknown';
  if (speaker.name) return speaker.name;
  if (speaker.diarization_label) return speaker.diarization_label;
  if (speaker.attribution === 'me' || (!speaker.attribution && speaker.source === 'microphone')) {
    return (note.owner && note.owner.name) || 'Me';
  }
  return 'Them';
}

function clock(ms) {
  const total = Math.max(0, Math.floor(ms / 1000));
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const pad = (n) => String(n).padStart(2, '0');
  return h ? `${h}:${pad(m)}:${pad(s)}` : `${pad(m)}:${pad(s)}`;
}

function attendeeList(note) {
  return (note.attendees || []).map((a) => a.name || a.email);
}

function commonFields(note, project, kind) {
  return {
    granola_id: note.id,
    type: kind,
    title: note.title || 'Untitled',
    project: project ? project.name : 'Unassigned',
    date: note.created_at,
    updated: note.updated_at,
    owner: note.owner ? note.owner.name || note.owner.email : '',
    attendees: attendeeList(note),
    granola_url: note.web_url || '',
  };
}

function renderNotes(note, project, { matchReasons = [] } = {}) {
  const parts = [frontMatter({ ...commonFields(note, project, 'notes'), matched_by: matchReasons })];
  parts.push(`# ${note.title || 'Untitled'}\n`);

  const meta = [`**Date:** ${note.created_at}`];
  const people = attendeeList(note);
  if (people.length) meta.push(`**Attendees:** ${people.join(', ')}`);
  if (note.web_url) meta.push(`**Granola:** ${note.web_url}`);
  parts.push(meta.join('  \n') + '\n');

  parts.push('## Summary\n');
  parts.push((note.summary_markdown || note.summary_text || '_No summary._').trim() + '\n');

  if (note.private_notes_markdown || note.private_notes_text) {
    parts.push('## My Notes\n');
    parts.push((note.private_notes_markdown || note.private_notes_text).trim() + '\n');
  }

  for (const att of note.attachments || []) {
    parts.push(`![attachment](${att.url})\n`);
  }
  return parts.join('\n');
}

// Consecutive lines from the same speaker are merged into one paragraph.
function renderTranscript(note, project) {
  const parts = [frontMatter(commonFields(note, project, 'transcript'))];
  parts.push(`# Transcript: ${note.title || 'Untitled'}\n`);

  const items = note.transcript || [];
  if (!items.length) {
    parts.push('_No transcript available._\n');
    return parts.join('\n');
  }

  const start = Date.parse(items[0].start_time) || 0;
  let current = null;
  const blocks = [];
  for (const item of items) {
    const label = speakerLabel(item.speaker, note);
    if (current && current.label === label) {
      current.text.push(item.text.trim());
    } else {
      current = { label, at: (Date.parse(item.start_time) || start) - start, text: [item.text.trim()] };
      blocks.push(current);
    }
  }
  for (const b of blocks) {
    parts.push(`**${b.label}** \`${clock(b.at)}\`  \n${b.text.join(' ')}\n`);
  }
  return parts.join('\n');
}

module.exports = { renderNotes, renderTranscript, speakerLabel };
