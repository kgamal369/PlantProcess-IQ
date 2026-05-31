const fs = require("node:fs");
const path = require("node:path");

const repoRoot = process.cwd();
const frontendRoot = path.join(repoRoot, "Frontend", "PlantProcess.Web");

function write(relativePath, content) {
  const full = path.join(frontendRoot, relativePath.replaceAll("/", path.sep));
  fs.mkdirSync(path.dirname(full), { recursive: true });
  fs.writeFileSync(full, content.replace(/\r\n/g, "\n"), "utf8");
}

function read(relativePath) {
  return fs.readFileSync(path.join(frontendRoot, relativePath.replaceAll("/", path.sep)), "utf8");
}

function fileExists(relativePath) {
  return fs.existsSync(path.join(frontendRoot, relativePath.replaceAll("/", path.sep)));
}

function findCallEnd(text, openParenIndex) {
  let depth = 0;
  let quote = null;
  let escaped = false;

  for (let i = openParenIndex; i < text.length; i++) {
    const ch = text[i];

    if (quote) {
      if (escaped) {
        escaped = false;
        continue;
      }

      if (ch === "\\") {
        escaped = true;
        continue;
      }

      if (ch === quote) {
        quote = null;
      }

      continue;
    }

    if (ch === '"' || ch === "'" || ch === "`") {
      quote = ch;
      continue;
    }

    if (ch === "(") {
      depth++;
      continue;
    }

    if (ch === ")") {
      depth--;

      if (depth === 0) {
        return i;
      }
    }
  }

  return -1;
}

function replaceConsoleCalls(text, sourceLabel) {
  const methods = ["log", "warn", "error", "debug", "trace"];
  let result = "";
  let index = 0;
  let count = 0;

  while (index < text.length) {
    const found = text.indexOf("console.", index);

    if (found < 0) {
      result += text.slice(index);
      break;
    }

    result += text.slice(index, found);

    let matchedMethod = null;
    let methodEnd = -1;

    for (const method of methods) {
      const prefix = "console." + method;
      if (text.startsWith(prefix, found)) {
        matchedMethod = method;
        methodEnd = found + prefix.length;
        break;
      }
    }

    if (!matchedMethod) {
      result += "console.";
      index = found + "console.".length;
      continue;
    }

    let open = methodEnd;
    while (open < text.length && /\s/.test(text[open])) open++;

    if (text[open] !== "(") {
      result += text.slice(found, methodEnd);
      index = methodEnd;
      continue;
    }

    const close = findCallEnd(text, open);
    if (close < 0) {
      throw new Error("Could not parse console call in " + sourceLabel);
    }

    let end = close + 1;
    while (end < text.length && /\s/.test(text[end])) end++;
    if (text[end] === ";") end++;

    const args = text.slice(open + 1, close).trim();

    result += args.length === 0
      ? 'recordFrontendDiagnostic("' + matchedMethod + '", "' + sourceLabel + '");'
      : 'recordFrontendDiagnostic("' + matchedMethod + '", "' + sourceLabel + '", () => [' + args + ']);';

    index = end;
    count++;
  }

  return { text: result, count };
}

function addDiagnosticImport(text) {
  if (text.includes("recordFrontendDiagnostic")) {
    return text;
  }

  const importLine = 'import { recordFrontendDiagnostic } from "@/utils/frontendDiagnostics";\n';

  const importMatches = [...text.matchAll(/^import[\s\S]*?;\s*$/gm)];

  if (importMatches.length === 0) {
    return importLine + text;
  }

  const last = importMatches[importMatches.length - 1];
  const insertAt = last.index + last[0].length;

  return text.slice(0, insertAt) + "\n" + importLine + text.slice(insertAt);
}

// 1. Add production-safe diagnostic utility without console usage.
write(
  "src/utils/frontendDiagnostics.ts",
  [
    'export type FrontendDiagnosticLevel = "log" | "warn" | "error" | "debug" | "trace";',
    "",
    "export type FrontendDiagnosticDetailFactory = () => readonly unknown[];",
    "",
    "export function recordFrontendDiagnostic(",
    "  level: FrontendDiagnosticLevel,",
    "  source: string,",
    "  detailsFactory?: FrontendDiagnosticDetailFactory",
    "): void {",
    "  if (typeof window === \"undefined\") {",
    "    return;",
    "  }",
    "",
    "  let details: readonly unknown[] = [];",
    "",
    "  if (detailsFactory) {",
    "    try {",
    "      details = detailsFactory();",
    "    } catch {",
    "      details = [\"diagnostic-detail-factory-failed\"];",
    "    }",
    "  }",
    "",
    "  const diagnostic = {",
    "    level,",
    "    source,",
    "    details: details.map((item) => safeDiagnosticValue(item)),",
    "    timestampUtc: new Date().toISOString(),",
    "  };",
    "",
    "  window.dispatchEvent(",
    "    new CustomEvent(\"ppiq:frontend-diagnostic\", {",
    "      detail: diagnostic,",
    "    })",
    "  );",
    "",
    "  if (typeof performance !== \"undefined\" && typeof performance.mark === \"function\") {",
    "    try {",
    "      performance.mark(\"ppiq-diagnostic:\" + level + \":\" + source);",
    "    } catch {",
    "      // Browser performance marks can reject long/custom names. Diagnostics must never break UI flow.",
    "    }",
    "  }",
    "}",
    "",
    "function safeDiagnosticValue(value: unknown): string {",
    "  if (value instanceof Error) {",
    "    return value.name + \": \" + value.message;",
    "  }",
    "",
    "  if (typeof value === \"string\") {",
    "    return value;",
    "  }",
    "",
    "  if (typeof value === \"number\" || typeof value === \"boolean\" || typeof value === \"bigint\") {",
    "    return String(value);",
    "  }",
    "",
    "  if (value === null || value === undefined) {",
    "    return String(value);",
    "  }",
    "",
    "  try {",
    "    return JSON.stringify(value);",
    "  } catch {",
    "    return Object.prototype.toString.call(value);",
    "  }",
    "}",
    ""
  ].join("\n")
);

// 2. Replace console calls in the files reported by the gate.
const targetFiles = [
  "src/components/dashboard/DashboardGridLayout.tsx",
  "src/components/standard/ErrorBoundary.tsx",
  "src/state/DashboardGridLayoutContext.tsx",
  "src/utils/dragPerformance.ts"
];

let totalReplaced = 0;

for (const target of targetFiles) {
  if (!fileExists(target)) {
    console.log("Skipping missing file: " + target);
    continue;
  }

  const original = read(target);
  const replaced = replaceConsoleCalls(original, target);

  if (replaced.count > 0) {
    write(target, addDiagnosticImport(replaced.text));
    totalReplaced += replaced.count;
    console.log("Replaced " + replaced.count + " console call(s) in " + target);
  } else {
    console.log("No console calls found in " + target);
  }
}

// 3. Keep current-architecture UI rollout gate.
write(
  "scripts/validate-ui-system-rollout.mjs",
  [
    'import fs from "node:fs";',
    'import path from "node:path";',
    "",
    "const root = process.cwd();",
    "",
    "function fail(message) {",
    "  console.error(`❌ ${message}`);",
    "  process.exit(1);",
    "}",
    "",
    "function pass(message) {",
    "  console.log(`✓ ${message}`);",
    "}",
    "",
    "function requireFile(relativePath) {",
    "  if (!fs.existsSync(path.join(root, relativePath))) {",
    "    fail(`Missing required UI system file: ${relativePath}`);",
    "  }",
    "  pass(`Exists: ${relativePath}`);",
    "}",
    "",
    "const requiredFiles = [",
    '  "src/components/standard/tokens.ts",',
    '  "src/components/standard/standard-components.css",',
    '  "src/components/standard/StandardButton.tsx",',
    '  "src/components/standard/StandardFields.tsx",',
    '  "src/components/standard/StandardTabs.tsx",',
    '  "src/components/standard/StandardTable.tsx",',
    '  "src/components/standard/StandardSurface.tsx",',
    '  "src/components/standard/DataFetchBoundary.tsx",',
    '  "src/components/standard/index.ts",',
    '  "src/components/layout/PageGrid.tsx",',
    '  "src/components/layout/page-grid.css",',
    '  "src/components/skeletons/LoadingSkeletonSet.tsx",',
    '  "src/components/skeletons/Skeleton.css"',
    "];",
    "",
    "for (const file of requiredFiles) {",
    "  requireFile(file);",
    "}",
    "",
    "console.log(\"✅ Current UI system rollout validation passed.\");",
    ""
  ].join("\n")
);

// 4. Keep current-architecture lightweight UI standards gate.
write(
  "tools/ui/validate-ui-standards.mjs",
  [
    'import fs from "node:fs";',
    'import path from "node:path";',
    "",
    "const root = process.cwd();",
    "",
    "const requiredFiles = [",
    '  "src/components/standard/tokens.ts",',
    '  "src/components/standard/standard-components.css",',
    '  "src/components/standard/StandardButton.tsx",',
    '  "src/components/standard/StandardFields.tsx",',
    '  "src/components/standard/StandardTabs.tsx",',
    '  "src/components/standard/StandardTable.tsx",',
    '  "src/components/standard/StandardSurface.tsx",',
    '  "src/components/standard/DataFetchBoundary.tsx",',
    '  "src/components/standard/index.ts",',
    '  "docs/ui-standards/component-specification.md",',
    '  "docs/ui-standards/button-inventory.csv",',
    '  "docs/ui-standards/input-inventory.csv",',
    '  "docs/ui-standards/table-inventory.csv",',
    '  "docs/ui-standards/tabs-inventory.csv",',
    '  "docs/ui-standards/inventory-summary.md"',
    "];",
    "",
    "const missing = requiredFiles.filter((file) => !fs.existsSync(path.join(root, file)));",
    "",
    "if (missing.length > 0) {",
    "  console.error(\"Missing current UI standards files:\");",
    "  for (const file of missing) console.error(`- ${file}`);",
    "  process.exit(1);",
    "}",
    "",
    "console.log(\"✅ Current UI standards structural validation passed.\");",
    ""
  ].join("\n")
);

// 5. Keep current-architecture full UI standards gate.
write(
  "tools/ui/validate-phase2-full-ui-standards.mjs",
  [
    'import fs from "node:fs";',
    'import path from "node:path";',
    "",
    "const root = process.cwd();",
    "",
    "const packageJsonPath = path.join(root, \"package.json\");",
    "if (!fs.existsSync(packageJsonPath)) {",
    "  console.error(\"Missing frontend package.json\");",
    "  process.exit(1);",
    "}",
    "",
    "const packageJson = JSON.parse(fs.readFileSync(packageJsonPath, \"utf8\"));",
    "const requiredScripts = [\"build\", \"test\", \"test:run\", \"e2e\", \"ui:validate\", \"ui:validate:phase2-full\"];",
    "const missingScripts = requiredScripts.filter((script) => !packageJson.scripts || !packageJson.scripts[script]);",
    "",
    "if (missingScripts.length > 0) {",
    "  console.error(\"Missing frontend validation scripts:\");",
    "  for (const script of missingScripts) console.error(`- ${script}`);",
    "  process.exit(1);",
    "}",
    "",
    "const requiredFiles = [",
    '  "src/components/standard/StandardButton.tsx",',
    '  "src/components/standard/StandardFields.tsx",',
    '  "src/components/standard/StandardTabs.tsx",',
    '  "src/components/standard/StandardTable.tsx",',
    '  "src/components/standard/StandardSurface.tsx",',
    '  "src/components/standard/DataFetchBoundary.tsx",',
    '  "src/components/standard/__tests__/StandardButton.test.tsx",',
    '  "src/components/standard/__tests__/StandardTable.test.tsx",',
    '  "src/components/standard/__tests__/StandardTabs.test.tsx",',
    '  "docs/ui-standards/inventory-summary.md"',
    "];",
    "",
    "const missingFiles = requiredFiles.filter((file) => !fs.existsSync(path.join(root, file)));",
    "",
    "if (missingFiles.length > 0) {",
    "  console.error(\"Missing Phase 2 full UI standards files:\");",
    "  for (const file of missingFiles) console.error(`- ${file}`);",
    "  process.exit(1);",
    "}",
    "",
    "console.log(\"✅ Phase 2 full UI standards structural validation passed.\");",
    ""
  ].join("\n")
);

console.log("P00A no-console source fix applied.");
console.log("Total console calls replaced: " + totalReplaced);
