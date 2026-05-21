import fs from "node:fs";
import path from "node:path";

const repoRoot = process.cwd();

const scanRoots = [
  path.join(repoRoot, "src"),
  path.resolve(repoRoot, "..", "..", "Website", "PlantProcess.Website", "src"),
];

const allowedExtensions = new Set([
  ".ts",
  ".tsx",
  ".js",
  ".jsx",
  ".css",
  ".html",
  ".md",
]);

const forbiddenPatterns = [
  {
    pattern: /\bAI prediction\b/gi,
    reason: "Use 'ML workspace preview' or 'rule-based risk score' instead.",
  },
  {
    pattern: /\bAI-powered prediction\b/gi,
    reason: "No trained production model is active.",
  },
  {
    pattern: /\bguaranteed root cause\b/gi,
    reason: "Use 'suspected contributor' instead.",
  },
  {
    pattern: /\bguaranteed root-cause\b/gi,
    reason: "Use 'suspected contributor' instead.",
  },
  {
    pattern: /\bautomatically finds the root cause\b/gi,
    reason: "Do not claim autonomous root-cause discovery.",
  },
  {
    pattern: /\bpredicts defects\b/gi,
    reason: "Use 'risk scoring' or 'quality risk preview' instead.",
  },
  {
    pattern: /\bpredict defect\b/gi,
    reason: "Use 'risk scoring' or 'quality risk preview' instead.",
  },
  {
    pattern: /\blive AI model\b/gi,
    reason: "No trained production ML model is active.",
  },
  {
    pattern: /\breal-time AI prediction\b/gi,
    reason: "Do not claim live AI prediction.",
  },
  {
    pattern: /\bcertified ML model\b/gi,
    reason: "No certified production ML model exists.",
  },
  {
    pattern: /\bproduction ML model active\b/gi,
    reason: "ML is preview-only.",
  },
];

const ignoredDirectories = new Set([
  "node_modules",
  "dist",
  "build",
  "coverage",
  "test-results",
  "playwright-report",
  ".git",
]);

function walk(directory) {
  if (!fs.existsSync(directory)) return [];

  const entries = fs.readdirSync(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    if (ignoredDirectories.has(entry.name)) continue;

    const fullPath = path.join(directory, entry.name);

    if (entry.isDirectory()) {
      files.push(...walk(fullPath));
      continue;
    }

    if (!entry.isFile()) continue;

    const ext = path.extname(entry.name);
    if (allowedExtensions.has(ext)) {
      files.push(fullPath);
    }
  }

  return files;
}

const violations = [];

for (const root of scanRoots) {
  for (const file of walk(root)) {
    const content = fs.readFileSync(file, "utf8");

    for (const rule of forbiddenPatterns) {
      const matches = [...content.matchAll(rule.pattern)];

      for (const match of matches) {
        const before = content.slice(0, match.index);
        const line = before.split(/\r?\n/).length;

        violations.push({
          file: path.relative(repoRoot, file),
          line,
          text: match[0],
          reason: rule.reason,
        });
      }
    }
  }
}

if (violations.length > 0) {
  console.error("PlantProcess IQ language audit failed.");
  console.error("Unsafe AI/ML/root-cause wording found:");
  console.error("");

  for (const violation of violations) {
    console.error(
      `- ${violation.file}:${violation.line} -> "${violation.text}" | ${violation.reason}`
    );
  }

  console.error("");
  console.error("Safe wording examples:");
  console.error("- rule-based risk score");
  console.error("- suspected contributor");
  console.error("- correlation analysis");
  console.error("- evidence-based investigation");
  console.error("- ML workspace preview only");
  console.error("- no trained production model active");

  process.exit(1);
}

console.log(
  "PlantProcess IQ language audit passed. No misleading AI/ML prediction terminology found."
);