const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

const scorecardJsonPath = path.join(root, "docs", "task-closure", "T001_T071_TASK_CLOSURE_SCORECARD.json");
const scorecardMdPath = path.join(root, "docs", "task-closure", "T001_T071_TASK_CLOSURE_SCORECARD.md");
const bridgedJsonPath = path.join(root, "docs", "task-closure", "T001_T071_TASK_CLOSURE_SCORECARD.PACK_A_BRIDGED.json");
const bridgedMdPath = path.join(root, "docs", "task-closure", "T001_T071_TASK_CLOSURE_SCORECARD.PACK_A_BRIDGED.md");

function exists(file) { return fs.existsSync(file); }
function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }
function read(file) { return fs.readFileSync(file, "utf8"); }
function write(file, content) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + path.relative(root, file).split(path.sep).join("/"));
}

function runCheck(label, command, args) {
  try {
    cp.execFileSync(command, args, { cwd: root, stdio: "pipe", shell: false });
    return { key: label, ok: true };
  } catch (error) {
    return {
      key: label,
      ok: false,
      error: (error.stdout ? error.stdout.toString() : "") + (error.stderr ? error.stderr.toString() : "") || error.message
    };
  }
}

function getRows(scorecard) {
  if (Array.isArray(scorecard)) return scorecard;
  if (Array.isArray(scorecard.tasks)) return scorecard.tasks;
  if (Array.isArray(scorecard.rows)) return scorecard.rows;
  if (Array.isArray(scorecard.scorecard)) return scorecard.scorecard;
  if (Array.isArray(scorecard.items)) return scorecard.items;
  return [];
}

function taskCode(row) { return String(row.task || row.taskCode || row.code || row.id || row.Task || "").trim(); }
function taskPack(row) { return String(row.pack || row.phase || row.group || row.Pack || "").trim(); }
function taskTitle(row) { return String(row.title || row.description || row.name || row.taskTitle || row.Title || "").trim(); }

function scoreOf(row) {
  const keys = ["score", "percent", "percentage", "completion", "completionPercent", "completion_score", "scorePercent"];
  for (const key of keys) {
    if (row[key] === undefined || row[key] === null) continue;
    if (typeof row[key] === "number") return row[key];
    const parsed = Number(String(row[key]).replace("%", "").trim());
    if (Number.isFinite(parsed)) return parsed;
  }
  return 0;
}

function setDone(row, score, note) {
  row.score = score;
  row.percent = score;
  row.completionPercent = score;
  row.percentage = score;
  row.status = "DONE";
  row.state = "DONE";
  row.result = "DONE";
  row.isGreen = true;
  row.isDone = true;
  row.done = true;
  row.below90 = false;
  row.evidenceBridge = note;
}

function rowLine(row) {
  const code = taskCode(row);
  const pack = taskPack(row) || (["T-007", "T-010", "T-028", "T-035"].includes(code) ? "A" : "");
  const score = scoreOf(row);
  const status = String(row.status || row.state || row.result || "").trim() || (score >= 90 ? "DONE" : "OPEN");
  const title = taskTitle(row);
  return code + " [" + pack + "] " + score + "% " + status + " - " + title;
}

const archiveCheck = runCheck("T-010 archive validator", "node", ["tools/pack-a/validate-pack-a-run-once-archive.cjs"]);
const ciCheck = runCheck("T-028 CI certification validator", "node", ["tools/pack-a/validate-pack-a-ci-certification.cjs"]);

const evidence = {
  generatedAtUtc: new Date().toISOString(),
  marker: "PPIQ_PACK_A3B_TASK_CLOSURE_EVIDENCE_BRIDGE",
  checks: {
    t010ArchiveValidator: archiveCheck,
    t028CiValidator: ciCheck,
    t010ReportExists: isFile(path.join(root, "docs", "pack-a", "PACK_A2_RUN_ONCE_TOOLING_ARCHIVE_REPORT.json")),
    t010ArchiveIndexExists: isFile(path.join(root, "tools", "_archive", "landed-tooling", "ARCHIVE_INDEX.md")),
    t028ReportExists: isFile(path.join(root, "docs", "pack-a", "PACK_A3_CI_CERTIFICATION_WIRING_REPORT.json")),
    t028GateReportExists: isFile(path.join(root, "docs", "ci", "gate-report.json"))
  }
};

if (!isFile(scorecardJsonPath)) {
  console.error("Missing scorecard JSON: " + scorecardJsonPath);
  process.exit(1);
}

const scorecard = JSON.parse(read(scorecardJsonPath));
const rows = getRows(scorecard);

if (!rows.length) {
  console.error("Could not locate task rows inside scorecard JSON.");
  process.exit(1);
}

const t010Green = archiveCheck.ok && evidence.checks.t010ReportExists && evidence.checks.t010ArchiveIndexExists;
const t028Green = ciCheck.ok && evidence.checks.t028ReportExists && evidence.checks.t028GateReportExists;

for (const row of rows) {
  const code = taskCode(row);
  if (code === "T-010" && t010Green) {
    setDone(row, 100, "Pack A-2 evidence bridge: archive validator passed, archive report exists, archive index exists.");
  }
  if (code === "T-028" && t028Green) {
    setDone(row, 100, "Pack A-3 evidence bridge: CI validator passed, Jenkins certification wiring exists, gate report exists.");
  }
}

scorecard.packAEvidenceBridge = evidence;

write(scorecardJsonPath, JSON.stringify(scorecard, null, 2) + "\n");
write(bridgedJsonPath, JSON.stringify(scorecard, null, 2) + "\n");

const below90 = rows.filter((row) => scoreOf(row) < 90);

const md = [];
md.push("# T001-T071 Task Closure Scorecard — Pack A Evidence Bridged");
md.push("");
md.push("Generated: " + evidence.generatedAtUtc);
md.push("");
md.push("Marker: PPIQ_PACK_A3B_TASK_CLOSURE_EVIDENCE_BRIDGE");
md.push("");
md.push("## Evidence Bridge Result");
md.push("");
md.push("| Task | Evidence | Result |");
md.push("|---|---|---|");
md.push("| T-010 | Pack A-2 run-once archive validator + archive report + archive index | " + (t010Green ? "**DONE**" : "**NOT GREEN**") + " |");
md.push("| T-028 | Pack A-3 CI validator + Jenkins wiring + gate report | " + (t028Green ? "**DONE**" : "**NOT GREEN**") + " |");
md.push("");
md.push("## Tasks below 90% after Pack A bridge");
md.push("");
md.push("Tasks below 90%: " + below90.length);
md.push("");
for (const row of below90) md.push(rowLine(row));
md.push("");
write(bridgedMdPath, md.join("\n") + "\n");

if (isFile(scorecardMdPath)) {
  let currentMd = read(scorecardMdPath).replace(/\r\n/g, "\n");
  if (!currentMd.includes("PPIQ_PACK_A3B_TASK_CLOSURE_EVIDENCE_BRIDGE")) {
    currentMd += "\n\n" + md.join("\n") + "\n";
    write(scorecardMdPath, currentMd);
  }
}

console.log("");
console.log("Pack A task-closure evidence bridge applied.");
console.log("T-010 bridge result: " + (t010Green ? "DONE" : "NOT GREEN"));
console.log("T-028 bridge result: " + (t028Green ? "DONE" : "NOT GREEN"));
console.log("");
console.log("Tasks below 90% after Pack A bridge: " + below90.length);
for (const row of below90) console.log(rowLine(row));
