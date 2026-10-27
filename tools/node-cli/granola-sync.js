#!/usr/bin/env node
// Granola -> Markdown sync. Run `node granola-sync.js --help` for usage.

const { GranolaClient } = require('./src/api');
const { loadConfig } = require('./src/config');
const { sync, discoverPeople } = require('./src/sync');

const HELP = `
Usage: node granola-sync.js [options]

  --config <file>     Config file (default: config.json)
  --since <date>      Only notes created after this date (e.g. 2026-01-01)
  --note <not_id>     Sync a single note
  --force             Re-download every note, ignoring saved sync state
                      (also re-files notes into new folders after you change project rules)
  --dry-run           Show where files would go without writing anything
  --discover [n]      List speaker names and attendees from the latest n notes (default 20)
                      to help you fill in the "projects" section of the config
  --help              Show this help
`;

function parseArgs(argv) {
  const args = { config: 'config.json' };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    const next = () => argv[++i];
    if (a === '--config') args.config = next();
    else if (a === '--since') args.since = next();
    else if (a === '--note') args.noteId = next();
    else if (a === '--force') args.force = true;
    else if (a === '--dry-run') args.dryRun = true;
    else if (a === '--discover') {
      args.discover = true;
      if (argv[i + 1] && /^\d+$/.test(argv[i + 1])) args.discoverLimit = Number(next());
    } else if (a === '--help' || a === '-h') args.help = true;
    else throw new Error(`Unknown option: ${a}`);
  }
  return args;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  if (args.help) return console.log(HELP);

  const config = loadConfig(args.config);
  const client = new GranolaClient(config.apiKey);

  if (args.discover) {
    return discoverPeople(client, { since: args.since, limit: args.discoverLimit });
  }

  console.log(`Notes       -> ${config.output.saveNotes ? config.output.notesDir : '(disabled)'}`);
  console.log(`Transcripts -> ${config.output.saveTranscripts ? config.output.transcriptsDir : '(disabled)'}\n`);
  const stats = await sync(client, config, args);
  console.log(
    `\nDone. checked ${stats.checked}, written ${stats.written}, unchanged ${stats.skipped}, ` +
      `unassigned ${stats.unassigned}, failed ${stats.failed}`
  );
  if (stats.failed) process.exitCode = 1;
}

main().catch((err) => {
  console.error(err.message);
  process.exitCode = 1;
});
