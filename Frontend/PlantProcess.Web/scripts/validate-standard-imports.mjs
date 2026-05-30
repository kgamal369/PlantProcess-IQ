import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const src = path.join(root, "src");

const failures = [];

function walk(dir, result = []) {
  if (!fs.existsSync(dir)) return result;

  for (const entry of fs.readdirSync(dir)) {
    const full = path.join(dir, entry);
    const rel = path.relative(root, full).replaceAll("\\", "/");

    if (
      rel.includes("node_modules/") ||
      rel.includes("dist/") ||
      rel.includes("coverage/") ||
      rel.includes("__tests__/") ||
      rel.endsWith(".stories.tsx") ||
      rel.endsWith(".stories.ts")
    ) {
      continue;
    }

    const stat = fs.statSync(full);
    if (stat.isDirectory()) walk(full, result);
    else result.push(full);
  }

  return result;
}

const files = walk(src).filter((file) => /\.(ts|tsx)$/.test(file));

const nativeUiPattern = /<(button|input|select|textarea|table)\b/i;
const restrictedImportPatterns = [
  /from\s+["']@\/hardening\//,
  /from\s+["']@\/components\/hardening\//,
  /from\s+["']@\/components\/table\/StandardTable["']/,
];

for (const file of files) {
  const rel = path.relative(root, file).replaceAll("\\", "/");
  const text = fs.readFileSync(file, "utf8");

  const isPageOrFeature =
    rel.startsWith("src/pages/") ||
    rel.startsWith("src/features/");

  if (isPageOrFeature && nativeUiPattern.test(text)) {
    failures.push({
      file: rel,
      reason:
        "Native UI element found in page/feature. Use StandardButton, StandardInput, StandardSelect, StandardTextArea and StandardTable.",
    });
  }

  for (const pattern of restrictedImportPatterns) {
    if (pattern.test(text)) {
      failures.push({
        file: rel,
        reason:
          "Restricted legacy/hardening import found. Use canonical src/components/standard/* or src/components/ErrorBoundary.",
      });
    }
  }
}

if (failures.length > 0) {
  console.error("");
  console.error("❌ PPIQ-T205 standard import/UI gate failed.");
  console.error("");

  for (const failure of failures) {
    console.error(`- ${failure.file}: ${failure.reason}`);
  }

  process.exit(1);
}

console.log("✅ PPIQ-T205 standard import/UI gate passed.");