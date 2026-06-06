const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".phase9_phase10_backup", "website_overclaim_repair_" + timestamp);
const websiteRoot = path.join(root, "Website", "PlantProcess.Website");

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(filePath) {
  return fs.existsSync(filePath);
}

function read(filePath) {
  return fs.readFileSync(filePath, "utf8");
}

function write(filePath, content) {
  ensureDir(path.dirname(filePath));
  fs.writeFileSync(filePath, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + path.relative(root, filePath).split(path.sep).join("/"));
}

function backup(filePath) {
  if (!exists(filePath) || !fs.statSync(filePath).isFile()) return;
  const target = path.join(backupRoot, path.relative(root, filePath));
  ensureDir(path.dirname(target));
  fs.copyFileSync(filePath, target);
}

function walk(dir) {
  if (!exists(dir)) return [];

  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    return entry.isDirectory() ? walk(full) : [full];
  });
}

function run(name, args, cwd = root) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(args[0], args.slice(1), {
    cwd,
    stdio: "inherit",
    shell: false
  });
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Phase 10 website overclaim repair");
console.log("=================================================================================================");
console.log("Backup folder: " + backupRoot);

ensureDir(backupRoot);

const replacements = [
  {
    pattern: /guaranteed root cause/gi,
    replacement: "evidence-backed root-cause investigation"
  },
  {
    pattern: /guaranteed savings/gi,
    replacement: "quantified savings estimate"
  },
  {
    pattern: /replaces MES/gi,
    replacement: "complements MES"
  },
  {
    pattern: /replaces SCADA/gi,
    replacement: "complements SCADA"
  },
  {
    pattern: /replaces PLC/gi,
    replacement: "complements PLC"
  }
];

const targetFiles = walk(websiteRoot).filter((file) =>
  /\.(ts|tsx|js|jsx|md|html)$/i.test(file)
);

const changed = [];

for (const file of targetFiles) {
  const before = read(file).replace(/\r\n/g, "\n");
  let after = before;

  for (const replacement of replacements) {
    after = after.replace(replacement.pattern, replacement.replacement);
  }

  if (after !== before) {
    backup(file);
    write(file, after);
    changed.push(path.relative(root, file).split(path.sep).join("/"));
  }
}

if (changed.length === 0) {
  console.log("No website overclaim replacements were needed.");
} else {
  console.log("");
  console.log("Updated website files:");
  for (const file of changed) console.log(" - " + file);
}

const evidencePath = path.join(root, "docs", "phase9-phase10", "PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md");
backup(evidencePath);

let evidence = exists(evidencePath) ? read(evidencePath).replace(/\r\n/g, "\n") : "# PlantProcess IQ Phase 9 + Phase 10 Evidence\n";

if (!evidence.includes("PPIQ_PHASE10_WEBSITE_OVERCLAIM_REPAIR")) {
  evidence += [
    "",
    "## Phase 10 website overclaim repair",
    "",
    "- Marker: `PPIQ_PHASE10_WEBSITE_OVERCLAIM_REPAIR`.",
    "- Removed unsafe absolute commercial claims such as `guaranteed root cause`.",
    "- Replaced them with evidence-backed, decision-support language.",
    "- Guard: `tools/phase9-phase10/website-phase10-guard.cjs`.",
    ""
  ].join("\n");

  write(evidencePath, evidence);
}

run("Phase 10 website commercial guard", ["node", "tools/phase9-phase10/website-phase10-guard.cjs"]);
run("Phase 9/10 source validation", ["node", "tools/phase9-phase10/validate-phase9-phase10.cjs"]);

console.log("");
console.log("=================================================================================================");
console.log("Phase 10 website overclaim repair completed successfully.");
console.log("Backup: " + backupRoot);
console.log("=================================================================================================");