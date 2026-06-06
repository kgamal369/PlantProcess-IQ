const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

function read(relativePath) {
  const file = path.join(root, relativePath);
  if (!fs.existsSync(file)) throw new Error("Missing file: " + relativePath);
  return fs.readFileSync(file, "utf8");
}

function has(relativePath, marker) {
  const text = read(relativePath);
  if (!text.toLowerCase().includes(marker.toLowerCase())) {
    throw new Error(relativePath + " missing marker: " + marker);
  }
}

function command(name, args) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(args[0], args.slice(1), { cwd: root, stdio: "inherit", shell: false });
}

has("docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.json", "PPIQ_GROUND_TRUTH_VALIDATION_SCORECARD");
has("docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.md", "Phase scorecard");
has("docs/ground-truth/GROUND_TRUTH_HOTFIX_EVIDENCE.md", "Ground Truth Hotfix Evidence");

const scorecard = JSON.parse(read("docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.json"));

if (!scorecard.phases || scorecard.phases.length < 10) {
  throw new Error("Ground-truth scorecard has too few phases.");
}

if (scorecard.overclaims && scorecard.overclaims.length > 0) {
  throw new Error("Unsafe commercial/AI overclaims still exist: " + JSON.stringify(scorecard.overclaims, null, 2));
}

if (scorecard.godFiles && scorecard.godFiles.unknown && scorecard.godFiles.unknown.length > 0) {
  throw new Error("Unknown active god-files exist: " + JSON.stringify(scorecard.godFiles.unknown, null, 2));
}

if (fs.existsSync(path.join(root, "tools", "phase3-phase4", "validate-phase3-phase4-source.cjs"))) {
  command("Phase 3/4 source validation", ["node", "tools/phase3-phase4/validate-phase3-phase4-source.cjs"]);
}

if (fs.existsSync(path.join(root, "tools", "phase9-phase10", "website-phase10-guard.cjs"))) {
  command("Phase 10 website commercial guard", ["node", "tools/phase9-phase10/website-phase10-guard.cjs"]);
}

console.log("Ground truth validation report passed.");
