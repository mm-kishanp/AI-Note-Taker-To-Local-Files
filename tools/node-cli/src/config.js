const fs = require('fs');
const path = require('path');

const DEFAULTS = {
  apiKey: '',
  output: {
    notesDir: './output/Notes',
    transcriptsDir: './output/Transcripts',
    unassignedFolder: '_Unassigned',
    pathTemplate: '{project}/{yyyy}/{MM}',
    fileNameTemplate: '{yyyy}-{MM}-{dd} {title}',
    saveNotes: true,
    saveTranscripts: true,
  },
  matching: {
    sources: ['speakers', 'attendees', 'invitees', 'granolaFolders'],
  },
  sync: {
    createdAfter: null,
    stateFile: './.granola-sync-state.json',
  },
  projects: [],
};

function loadConfig(configPath) {
  const abs = path.resolve(configPath);
  if (!fs.existsSync(abs)) {
    throw new Error(`Config not found: ${abs}\nCopy config.example.json to config.json and edit it.`);
  }
  const raw = JSON.parse(fs.readFileSync(abs, 'utf8'));
  const baseDir = path.dirname(abs);
  const resolveDir = (p) => path.resolve(baseDir, p);

  const config = {
    apiKey: process.env.GRANOLA_API_KEY || raw.apiKey || DEFAULTS.apiKey,
    output: { ...DEFAULTS.output, ...raw.output },
    matching: { ...DEFAULTS.matching, ...raw.matching },
    sync: { ...DEFAULTS.sync, ...raw.sync },
    projects: raw.projects || [],
  };
  config.output.notesDir = resolveDir(config.output.notesDir);
  config.output.transcriptsDir = resolveDir(config.output.transcriptsDir);
  config.sync.stateFile = resolveDir(config.sync.stateFile);

  for (const p of config.projects) {
    if (!p.name) throw new Error('Every project in config.projects needs a "name".');
  }
  return config;
}

module.exports = { loadConfig };
