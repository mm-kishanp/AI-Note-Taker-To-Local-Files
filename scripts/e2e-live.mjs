// Live end-to-end check against a running API and a real Granola account (read-only on Granola).
// Usage: node scripts/e2e-live.mjs <exportRoot> [days=10]
// Requires a Granola key already stored (Settings page or PUT /api/settings/credentials/granola).
// Creates two test projects prefixed "E2E" and writes Markdown under <exportRoot> only.
import { readFileSync, existsSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { join, relative } from "node:path";

const B = process.env.GPI_URL ?? "http://127.0.0.1:5080/api";
const H = { "Content-Type": "application/json", "X-GPI-Request": "1" };
const root = process.argv[2];
const days = Number(process.argv[3] ?? 10);
if (!root) throw new Error("usage: node scripts/e2e-live.mjs <exportRoot> [days]");

const call = async (path, opts = {}) => {
  const r = await fetch(B + path, opts);
  const text = await r.text();
  let body; try { body = JSON.parse(text); } catch { body = text; }
  return { status: r.status, body };
};
const post = (p, b) => call(p, { method: "POST", headers: H, body: JSON.stringify(b ?? {}) });
const put = (p, b) => call(p, { method: "PUT", headers: H, body: JSON.stringify(b) });
const wait = async () => { for (;;) { const s = (await call("/sync/status")).body; if (!s.isRunning) return s; await new Promise(r => setTimeout(r, 1000)); } };
const lastRun = async () => (await call("/sync/history?pageSize=1")).body.items[0];
const files = (dir) => !existsSync(dir) ? [] : readdirSync(dir).flatMap(n => { const p = join(dir, n); return statSync(p).isDirectory() ? files(p) : [p]; });
let failures = 0;
const check = (ok, msg) => { console.log(`${ok ? "PASS" : "FAIL"}  ${msg}`); if (!ok) failures++; };

const health = (await call("/health")).body;
check(health.credentialsConfigured, "Granola key configured");
check((await post("/granola/test-connection")).body.success, "Connection test succeeds");

const cfg = (await call("/settings")).body;
const since = new Date(Date.now() - days * 864e5).toISOString();
check((await put("/settings", { export: { ...cfg.export, rootDirectory: root }, sync: { ...cfg.sync, initialImportFrom: since } })).status === 200, "Settings saved");

await post("/sync/start", { mode: "Full" }); await wait();
let run = await lastRun();
check(run.status === "Succeeded", `Full sync: ${run.status}, ${run.notesDiscovered} discovered, ${run.notesExported} notes, ${run.transcriptsExported} transcripts`);
const unassignedNotes = files(join(root, "Unassigned", "Notes"));
check(unassignedNotes.length > 0 && files(join(root, "Unassigned", "Transcripts")).length > 0, "Separate notes and transcript files in Unassigned");
check(existsSync(join(root, "Unassigned", "ReviewQueue.md")), "ReviewQueue.md written");

// Second sync: incremental, nothing should be re-written.
await post("/sync/start", { mode: "Incremental" }); await wait();
run = await lastRun();
check(run.status === "Succeeded" && run.filesUpdated === 0 && run.notesExported === 0, `Incremental sync writes nothing new (processed ${run.meetingsProcessed}, skipped ${run.meetingsSkipped})`);

// Build a project from a real identified speaker in the review queue.
const review = (await call("/unassigned?pageSize=100")).body.items;
const withSpeaker = review.find(i => i.identifiedSpeakers.some(s => s.name !== i.participants.find(p => p.isOwner)?.name));
check(!!withSpeaker, "Found an unassigned meeting with an identified speaker");
const speaker = withSpeaker.identifiedSpeakers.find(s => !withSpeaker.participants.some(p => p.isOwner && p.name === s.name)).name;
const proj = (await post("/projects", { name: "E2E Speaker Project", status: "Active", defaultClassificationPriority: 100, rules: [{ kind: "SpeakerName", value: speaker }] })).body;
const preview = (await post(`/projects/${proj.id}/test-mapping`, { limit: 500 })).body;
check(preview.items.some(i => i.meetingId === withSpeaker.meeting.id), `Mapping preview includes the meeting (${preview.wouldChange} would change)`);

const apply = await post(`/projects/${proj.id}/apply-mapping`, { meetingIds: preview.items.filter(i => i.changes).map(i => i.meetingId), confirm: true });
check(apply.status === 202, "Apply mapping accepted"); await wait();
run = await lastRun();
check(run.status === "Succeeded", `Re-export after mapping: ${run.status}`);
const projectNotes = files(join(root, "Projects", proj.slug, "Notes"));
check(projectNotes.length > 0, `Notes moved to Projects/${proj.slug}/Notes (${projectNotes.length})`);
check(files(join(root, "Projects", proj.slug, "Transcripts")).length > 0, "Transcripts moved to the project folder");
check(existsSync(join(root, "Projects", proj.slug, "PROJECT_CONTEXT.md")) && existsSync(join(root, "Projects", proj.slug, "README.md")), "README.md and PROJECT_CONTEXT.md created");
check(files(join(root, "Unassigned", "Notes")).length === unassignedNotes.length - projectNotes.length, "No duplicate left behind in Unassigned");

// Manual override to Shared, then back to automatic.
const mid = withSpeaker.meeting.id;
let detail = (await post(`/meetings/${mid}/assign-project`, { kind: "Shared" })).body;
check(detail.meeting.classificationStatus === "Shared" && files(join(root, "Shared", "Notes")).length === 1, "Manual assignment to Shared moves the files");
detail = (await post(`/meetings/${mid}/assign-project`, { kind: "None" })).body;
check(detail.meeting.projectId === proj.id, "Clearing the manual assignment restores rule-based project");

// User edit + source change => conflict, never overwritten.
const notesRec = detail.exports.find(e => e.kind === "Notes" && e.status !== "Superseded");
const edited = readFileSync(notesRec.absolutePath, "utf8") + "\n\nMy local edit.\n";
writeFileSync(notesRec.absolutePath, edited);
await put(`/projects/${proj.id}`, { ...proj, name: "E2E Speaker Project Renamed", rules: proj.rules.map(r => ({ kind: r.kind, value: r.value, enabled: r.enabled })) });
detail = (await post(`/meetings/${mid}/reclassify`)).body;
const after = detail.exports.find(e => e.id === notesRec.id);
check(after.status === "Conflict", `Edited file + changed output => ${after.status}`);
check(readFileSync(notesRec.absolutePath, "utf8") === edited, "Local edits preserved");
detail = (await post(`/meetings/${mid}/exports/${notesRec.id}/resolve`, { resolution: "SaveConflictCopy" })).body;
check(detail.exports.find(e => e.id === notesRec.id).status === "KeptLocal", "Conflict resolved by saving a .conflict copy");

const dash = (await call("/dashboard")).body;
check(dash.totalMeetings > 0 && dash.projectsConfigured >= 1, `Dashboard: ${dash.totalMeetings} meetings, ${dash.unassignedMeetings} unassigned, ${dash.conflicts} conflicts`);

console.log(`\n${failures === 0 ? "ALL CHECKS PASSED" : failures + " CHECK(S) FAILED"}  (export root: ${relative(process.cwd(), root) || root})`);
process.exit(failures === 0 ? 0 : 1);
