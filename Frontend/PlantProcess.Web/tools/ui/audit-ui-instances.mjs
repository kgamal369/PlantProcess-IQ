import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const src = path.join(root, "src");
const outDir = path.join(root, "reports", "ui-audit");

const groups = [
  {
    task: "PPIQ-T009",
    group: "button",
    canonical: "StandardButton",
    patterns: [
      ["native-button", /<button\b/g],
      ["role-button", /role=["']button["']/g],
      ["button-class", /className=\{?["'`][^"'`]*(btn|button|cta|action)[^"'`]*["'`]\}?/gi],
      ["standard-button", /<StandardButton\b/g],
    ],
  },
  {
    task: "PPIQ-T010",
    group: "table",
    canonical: "StandardTable",
    patterns: [
      ["native-table", /<table\b/g],
      ["table-tags", /<(thead|tbody|tfoot|tr|th|td)\b/g],
      ["grid-role", /role=["'](table|grid|row|columnheader|cell)["']/g],
      ["standard-table", /<StandardTable\b/g],
    ],
  },
  {
    task: "PPIQ-T011",
    group: "navigation",
    canonical: "StandardTabs",
    patterns: [
      ["nav", /<nav\b/g],
      ["router-link", /<(NavLink|Link)\b/g],
      ["tab-role", /role=["'](tab|tablist|tabpanel)["']/g],
      ["tab-class", /className=\{?["'`][^"'`]*(tab|tabs|segment|segmented|nav)[^"'`]*["'`]\}?/gi],
      ["standard-tabs", /<StandardTabs\b/g],
    ],
  },
  {
    task: "PPIQ-T012",
    group: "form",
    canonical: "StandardInput / StandardSelect / StandardTextArea",
    patterns: [
      ["form", /<form\b/g],
      ["input", /<input\b/g],
      ["select", /<select\b/g],
      ["textarea", /<textarea\b/g],
      ["label", /<label\b/g],
      ["standard-field", /<(StandardInput|StandardSelect|StandardTextArea)\b/g],
    ],
  },
];

function walk(dir) {
  const result = [];
  if (!fs.existsSync(dir)) return result;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (full.includes(`${path.sep}node_modules${path.sep}`)) continue;
    if (full.includes(`${path.sep}dist${path.sep}`)) continue;
    if (full.includes(`${path.sep}reports${path.sep}`)) continue;

    if (entry.isDirectory()) {
      result.push(...walk(full));
      continue;
    }

    if (!entry.isFile()) continue;
    if (!/\.(tsx|ts|jsx|js)$/.test(entry.name)) continue;
    if (entry.name.endsWith(".d.ts")) continue;

    result.push(full);
  }

  return result;
}

function rel(file) {
  return path.relative(root, file).replaceAll(path.sep, "/");
}

function lineOf(text, index) {
  return text.slice(0, index).split(/\r?\n/).length;
}

const findings = [];

for (const file of walk(src)) {
  const text = fs.readFileSync(file, "utf8");

  for (const group of groups) {
    for (const [patternName, regex] of group.patterns) {
      regex.lastIndex = 0;
      let match;

      while ((match = regex.exec(text))) {
        findings.push({
          task: group.task,
          group: group.group,
          canonical: group.canonical,
          file: rel(file),
          line: lineOf(text, match.index),
          pattern: patternName,
          evidence: match[0].replace(/\s+/g, " ").slice(0, 160),
        });

        if (match.index === regex.lastIndex) regex.lastIndex++;
      }
    }
  }
}

fs.mkdirSync(outDir, { recursive: true });

const byTask = findings.reduce((acc, item) => {
  acc[item.task] = (acc[item.task] ?? 0) + 1;
  return acc;
}, {});

const byFile = findings.reduce((acc, item) => {
  acc[item.file] ??= { file: item.file, total: 0, button: 0, table: 0, navigation: 0, form: 0 };
  acc[item.file].total++;
  acc[item.file][item.group]++;
  return acc;
}, {});

const summary = {
  generatedAtUtc: new Date().toISOString(),
  totalFindings: findings.length,
  byTask,
  byFile: Object.values(byFile).sort((a, b) => b.total - a.total || a.file.localeCompare(b.file)),
};

const md = [
  "# PlantProcess IQ Phase 2 UI Audit",
  "",
  `Generated UTC: \`${summary.generatedAtUtc}\``,
  "",
  "## Summary",
  "",
  `Total findings: **${summary.totalFindings}**`,
  "",
  "| Task | Area | Count | Canonical target |",
  "|---|---|---:|---|",
  `| PPIQ-T009 | Buttons | ${byTask["PPIQ-T009"] ?? 0} | StandardButton |`,
  `| PPIQ-T010 | Tables | ${byTask["PPIQ-T010"] ?? 0} | StandardTable |`,
  `| PPIQ-T011 | Tabs / navigation | ${byTask["PPIQ-T011"] ?? 0} | StandardTabs |`,
  `| PPIQ-T012 | Forms / fields | ${byTask["PPIQ-T012"] ?? 0} | StandardInput / Select / TextArea |`,
  "",
  "## Highest impact files",
  "",
  "| File | Total | Buttons | Tables | Navigation | Forms |",
  "|---|---:|---:|---:|---:|---:|",
  ...summary.byFile.slice(0, 50).map((f) => `| \`${f.file}\` | ${f.total} | ${f.button} | ${f.table} | ${f.navigation} | ${f.form} |`),
  "",
  "## Detailed findings",
  "",
  "| Task | Group | File | Line | Pattern | Evidence |",
  "|---|---|---|---:|---|---|",
  ...findings.map((f) => `| ${f.task} | ${f.group} | \`${f.file}\` | ${f.line} | ${f.pattern} | \`${f.evidence.replaceAll("|", "\\|")}\` |`),
  "",
  "## Rollout recommendation",
  "",
  "Start replacing high-use shared components first, then dashboard/admin buttons, then tables, then tabs/navigation, then forms.",
  "",
  "## Product guard",
  "",
  "These standards are generic manufacturing UI primitives. Do not hard-code steel-only vocabulary into base UI components.",
].join("\n");

fs.writeFileSync(path.join(outDir, "phase2-ui-audit.json"), JSON.stringify({ summary, findings }, null, 2), "utf8");
fs.writeFileSync(path.join(outDir, "phase2-ui-audit.md"), md, "utf8");

console.log("PPIQ Phase 2 UI audit completed.");
console.log(`Findings: ${findings.length}`);
console.log("Report: reports/ui-audit/phase2-ui-audit.md");