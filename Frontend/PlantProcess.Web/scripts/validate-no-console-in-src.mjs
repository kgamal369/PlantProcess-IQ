import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const srcRoot = path.join(root, "src");

const allowedFiles = new Set([
  path.normalize("src/components/ErrorBoundary.tsx"),
  path.normalize("src/components/hardening/AppErrorBoundary.tsx"),
  path.normalize("src/api/http/apiClient.ts"),
]);

const allowedPatterns = [
  /console\.error\("\[ErrorBoundary\]"/,
  /console\.error\("RouteErrorBoundary"/,
  /console\.error\(/,
];

const blockedConsolePattern = /\bconsole\.(log|warn|error|debug|trace)\s*\(/;

function walk(dir) {
  const files = [];

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (
        entry.name === "node_modules" ||
        entry.name === "dist" ||
        entry.name === "coverage"
      ) {
        continue;
      }

      files.push(...walk(full));
    } else if (/\.(ts|tsx|js|jsx)$/.test(entry.name)) {
      files.push(full);
    }
  }

  return files;
}

function toRelative(file) {
  return path.normalize(path.relative(root, file));
}

const violations = [];

for (const file of walk(srcRoot)) {
  const relative = toRelative(file);
  const content = fs.readFileSync(file, "utf8");

  if (!blockedConsolePattern.test(content)) continue;

  if (allowedFiles.has(relative)) {
    const allowed = allowedPatterns.some((pattern) => pattern.test(content));

    if (allowed) {
      console.log(`✓ Allowed diagnostic console usage: ${relative}`);
      continue;
    }
  }

  violations.push(relative);
}

if (violations.length > 0) {
  console.error("❌ Console usage found in production source:");
  for (const violation of violations) {
    console.error(` - ${violation}`);
  }
  process.exit(1);
}

console.log("✓ No unsafe console usage found in frontend source");