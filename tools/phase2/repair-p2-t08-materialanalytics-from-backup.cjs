const fs = require("fs");
const path = require("path");
const childProcess = require("child_process");

const root = process.cwd();
const targetRel = "Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/MaterialAnalyticsPages.runtime.generated.tsx";
const cssRel = "Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/productModule56-standard.css";
const target = path.join(root, targetRel.replaceAll("/", path.sep));
const cssPath = path.join(root, cssRel.replaceAll("/", path.sep));

function fail(message) {
  console.error("[RED] P2-T08 MaterialAnalytics repair failed: " + message);
  process.exit(1);
}

function run(command, args, options = {}) {
  const result = childProcess.spawnSync(command, args, {
    cwd: options.cwd || root,
    encoding: "utf8",
    shell: process.platform === "win32",
  });

  return result;
}

function listFiles(dir) {
  const output = [];

  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const item = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      output.push(...listFiles(item));
    } else {
      output.push(item);
    }
  }

  return output;
}

function scoreCandidate(text) {
  let score = 0;

  if (text.includes("PPIQ_REALIZATION_T063_MATERIAL_ANALYTICS_SPLIT")) score += 1000;
  if (text.includes("function ChartPlaceholder({")) score += 1000;
  if (text.includes("function materialColumns")) score += 1000;
  if (text.includes("export function MaterialAnalyticsCommandDashboardPage")) score += 1000;
  if (text.includes("export function MaterialAnalyticsBrandIdentityPage")) score += 1000;
  if (text.includes("StandardDataState")) score += 500;
  if (text.includes("StandardModal")) score += 250;
  if (text.length > 30000) score += 500;
  if (text.length > 45000) score += 500;

  // Strongly punish obviously truncated/corrupted files.
  if (!text.includes("function materialColumns")) score -= 5000;
  if (!text.includes("export function MaterialAnalyticsBrandIdentityPage")) score -= 5000;
  if (text.length < 20000) score -= 5000;

  return score;
}

const candidates = [];

if (fs.existsSync(target)) {
  candidates.push({
    source: "current",
    file: target,
    text: fs.readFileSync(target, "utf8"),
  });
}

const backupRoot = path.join(root, ".phase2_backups");

for (const file of listFiles(backupRoot)) {
  const base = path.basename(file).toLowerCase();

  if (
    base === "materialanalyticspages.runtime.generated.tsx" ||
    base === "materialanalyticspages.runtime.generated.tsx.broken" ||
    base === "materialanalyticspages.runtime.generated.tsx.bak"
  ) {
    candidates.push({
      source: "backup",
      file,
      text: fs.readFileSync(file, "utf8"),
    });
  }
}

const gitResult = run("git", ["show", "HEAD:" + targetRel]);

if (gitResult.status === 0 && gitResult.stdout && gitResult.stdout.includes("MaterialAnalytics")) {
  candidates.push({
    source: "git-head",
    file: "git show HEAD:" + targetRel,
    text: gitResult.stdout,
  });
}

if (candidates.length === 0) {
  fail("No current, backup, or git-head candidate found.");
}

const ranked = candidates
  .map((candidate) => ({
    ...candidate,
    score: scoreCandidate(candidate.text),
  }))
  .sort((a, b) => b.score - a.score);

const best = ranked[0];

console.log("[P2-T08] Candidate count: " + ranked.length);
console.log("[P2-T08] Selected candidate: " + best.source + " :: " + best.file);
console.log("[P2-T08] Candidate score: " + best.score);

if (best.score < 3000) {
  console.log("[P2-T08] Top candidates:");
  for (const c of ranked.slice(0, 10)) {
    console.log(" - " + c.score + " :: " + c.source + " :: " + c.file + " :: length=" + c.text.length);
  }

  fail("No safe full MaterialAnalytics candidate found.");
}

let text = best.text;

const start = text.indexOf("function ChartPlaceholder({");
const end = text.indexOf("function materialColumns", start);

if (start < 0 || end < 0 || end <= start) {
  fail("Selected candidate does not contain a valid ChartPlaceholder -> materialColumns block.");
}

const replacement = `function ChartPlaceholder({
  title,
  note,
  matrix = false,
}: {
  title: string;
  note: string;
  matrix?: boolean;
}) {
  return (
    <div className="productModule56-chart-box" role="img" aria-label={title}>
      {matrix ? (
        <div className="productModule56-matrix">
          {Array.from({ length: 64 }).map((_, index) => (
            <span
              key={index}
              className={"productModule56-matrix-cell productModule56-matrix-cell--" + ((index % 8) + Math.floor(index / 8))}
            />
          ))}
        </div>
      ) : (
        <div>
          <strong>{title}</strong>
          <p>{note}</p>
        </div>
      )}
    </div>
  );
}

`;

const repaired = text.slice(0, start) + replacement + text.slice(end);

if (!repaired.includes("function materialColumns")) fail("Repair removed materialColumns.");
if (!repaired.includes("export function MaterialAnalyticsCommandDashboardPage")) fail("Repair removed command dashboard page.");
if (!repaired.includes("export function MaterialAnalyticsBrandIdentityPage")) fail("Repair removed brand identity page.");
if (repaired.includes("style={{ \"--i\"")) fail("Repair still contains old matrix inline style.");

const stamp = new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14);
const repairBackupDir = path.join(root, ".phase2_backups", "P2-T08_FinalMaterialAnalyticsRestore_" + stamp);
fs.mkdirSync(repairBackupDir, { recursive: true });

if (fs.existsSync(target)) {
  fs.copyFileSync(target, path.join(repairBackupDir, "MaterialAnalyticsPages.runtime.generated.tsx.before-final-repair"));
}

fs.writeFileSync(target, repaired.replace(/\r?\n/g, "\r\n"), "utf8");

if (fs.existsSync(cssPath)) {
  fs.copyFileSync(cssPath, path.join(repairBackupDir, "productModule56-standard.css.before-final-repair"));

  let css = fs.readFileSync(cssPath, "utf8");

  css = css.replace(
    /background:\s*rgba\(0,\s*212,\s*255,\s*calc\(0\.08\s*\+\s*var\(--i\)\s*\*\s*0\.018\)\);/g,
    "background: rgba(0, 212, 255, 0.18);"
  );

  if (!css.includes(".productModule56-matrix-cell--14")) {
    css += `

.productModule56-matrix-cell--0 { opacity: 0.28; }
.productModule56-matrix-cell--1 { opacity: 0.34; }
.productModule56-matrix-cell--2 { opacity: 0.40; }
.productModule56-matrix-cell--3 { opacity: 0.46; }
.productModule56-matrix-cell--4 { opacity: 0.52; }
.productModule56-matrix-cell--5 { opacity: 0.58; }
.productModule56-matrix-cell--6 { opacity: 0.64; }
.productModule56-matrix-cell--7 { opacity: 0.70; }
.productModule56-matrix-cell--8 { opacity: 0.76; }
.productModule56-matrix-cell--9 { opacity: 0.82; }
.productModule56-matrix-cell--10 { opacity: 0.88; }
.productModule56-matrix-cell--11 { opacity: 0.92; }
.productModule56-matrix-cell--12 { opacity: 0.96; }
.productModule56-matrix-cell--13 { opacity: 1; }
.productModule56-matrix-cell--14 { opacity: 1; }
`;
  }

  fs.writeFileSync(cssPath, css.replace(/\r?\n/g, "\r\n"), "utf8");
}

const validate = run("node", ["tools/phase2/validate-p2-t08-standard-rollout.cjs"]);
process.stdout.write(validate.stdout || "");
process.stderr.write(validate.stderr || "");

if (validate.status !== 0) {
  fail("Static validation failed after final MaterialAnalytics restore.");
}

console.log("[GREEN] P2-T08 MaterialAnalytics restored from safe candidate and repaired.");
console.log("Backup: " + repairBackupDir);
