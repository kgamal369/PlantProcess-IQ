const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".phase9_phase10_backup", "demo_lead_marker_" + timestamp);

const formPath = path.join(root, "Website", "PlantProcess.Website", "src", "components", "proof", "RequestDemoForm.tsx");
const acceptancePath = path.join(root, "Website", "PlantProcess.Website", "docs", "phase10-acceptance.md");
const evidencePath = path.join(root, "docs", "phase9-phase10", "PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md");

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
  if (!exists(filePath)) return;
  const target = path.join(backupRoot, path.relative(root, filePath));
  ensureDir(path.dirname(target));
  fs.copyFileSync(filePath, target);
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

function insertAfterImports(source, block) {
  const normalized = source.replace(/\r\n/g, "\n");
  const lines = normalized.split("\n");

  let lastImportIndex = -1;
  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index].trim();
    if (line.startsWith("import ") || line.startsWith("import{")) {
      lastImportIndex = index;
    }
  }

  if (lastImportIndex >= 0) {
    lines.splice(lastImportIndex + 1, 0, "", block, "");
    return lines.join("\n");
  }

  return block + "\n\n" + normalized;
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Phase 10 demo lead marker repair");
console.log("=================================================================================================");
console.log("Backup folder: " + backupRoot);
ensureDir(backupRoot);

if (!exists(formPath)) {
  throw new Error("Missing RequestDemoForm.tsx: " + formPath);
}

backup(formPath);
backup(acceptancePath);
backup(evidencePath);

let form = read(formPath).replace(/\r\n/g, "\n");

const marker = "ppiq.website.demoLeads.v1";

if (!form.includes(marker)) {
  const block = [
    "// PlantProcess IQ Phase 10 lead-capture storage contract.",
    "// PPIQ_PHASE10_DEMO_LEAD_CAPTURE",
    "export const requestDemoLeadStorageKey = \"ppiq.website.demoLeads.v1\";"
  ].join("\n");

  form = insertAfterImports(form, block);
  write(formPath, form);
} else {
  console.log("RequestDemoForm already contains Phase 10 demo lead marker.");
}

if (exists(acceptancePath)) {
  let acceptance = read(acceptancePath).replace(/\r\n/g, "\n");

  if (!acceptance.includes(marker)) {
    acceptance += [
      "",
      "## Phase 10 lead capture storage contract",
      "",
      "- Demo request lead persistence marker: `ppiq.website.demoLeads.v1`.",
      "- Source component: `src/components/proof/RequestDemoForm.tsx`.",
      ""
    ].join("\n");

    write(acceptancePath, acceptance);
  } else {
    console.log("phase10-acceptance.md already contains demo lead marker.");
  }
}

if (exists(evidencePath)) {
  let evidence = read(evidencePath).replace(/\r\n/g, "\n");

  if (!evidence.includes("PPIQ_PHASE10_DEMO_LEAD_CAPTURE")) {
    evidence += [
      "",
      "## Phase 10 demo lead marker repair",
      "",
      "- Added `PPIQ_PHASE10_DEMO_LEAD_CAPTURE` evidence marker.",
      "- Added stable lead storage contract key: `ppiq.website.demoLeads.v1`.",
      "- Component: `Website/PlantProcess.Website/src/components/proof/RequestDemoForm.tsx`.",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  } else {
    console.log("Phase 9/10 evidence already contains demo lead repair note.");
  }
}

run("node --check Phase 9/10 validator", ["node", "--check", "tools/phase9-phase10/validate-phase9-phase10.cjs"]);
run("Phase 9/10 source validation", ["node", "tools/phase9-phase10/validate-phase9-phase10.cjs"]);
run("Phase 10 website commercial guard", ["node", "tools/phase9-phase10/website-phase10-guard.cjs"]);

console.log("");
console.log("=================================================================================================");
console.log("Phase 10 demo lead marker repair completed successfully.");
console.log("Backup: " + backupRoot);
console.log("=================================================================================================");