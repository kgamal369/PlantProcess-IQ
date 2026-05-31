const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

function p(...parts) {
  return path.join(root, ...parts);
}

function read(file) {
  if (!fs.existsSync(file)) throw new Error("File not found: " + file);
  return fs.readFileSync(file, "utf8");
}

function write(file, text) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, text.replace(/\r?\n/g, "\r\n"), "utf8");
  console.log("Wrote " + path.relative(root, file));
}

function patch(file, updater) {
  const before = read(file);
  const after = updater(before);
  if (after === before) {
    console.log("OK no change needed: " + path.relative(root, file));
    return false;
  }

  fs.writeFileSync(file, after, "utf8");
  console.log("Patched " + path.relative(root, file));
  return true;
}

// ============================================================
// 1. Standard page compatibility wrappers
// These live inside components/standard, so native HTML remains
// centralized and page/feature files stay clean.
// ============================================================

write(
  p("Frontend", "PlantProcess.Web", "src", "components", "standard", "StandardPageCompat.tsx"),
  `import {
  forwardRef,
  type ButtonHTMLAttributes,
  type InputHTMLAttributes,
  type SelectHTMLAttributes,
  type TableHTMLAttributes,
  type TextareaHTMLAttributes,
} from "react";

import "./standard-components.css";

function cx(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

export type StandardPageButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  isDisabled?: boolean;
  isLoading?: boolean;
};

export const StandardPageButton = forwardRef<HTMLButtonElement, StandardPageButtonProps>(
  ({ className, type = "button", disabled, isDisabled, isLoading, children, ...rest }, ref) => (
    <button
      ref={ref}
      type={type}
      className={cx("ppiq-std-button", "ppiq-std-button--md", className)}
      disabled={disabled || isDisabled || isLoading}
      aria-disabled={disabled || isDisabled || isLoading || undefined}
      aria-busy={isLoading || undefined}
      {...rest}
    >
      {children}
    </button>
  ),
);

StandardPageButton.displayName = "StandardPageButton";

export const StandardPageInput = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(
  ({ className, ...rest }, ref) => (
    <input
      ref={ref}
      className={cx("ppiq-std-field__control", className)}
      {...rest}
    />
  ),
);

StandardPageInput.displayName = "StandardPageInput";

export const StandardPageSelect = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement>>(
  ({ className, children, ...rest }, ref) => (
    <select
      ref={ref}
      className={cx("ppiq-std-field__control", className)}
      {...rest}
    >
      {children}
    </select>
  ),
);

StandardPageSelect.displayName = "StandardPageSelect";

export const StandardPageTextArea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement>>(
  ({ className, ...rest }, ref) => (
    <textarea
      ref={ref}
      className={cx("ppiq-std-field__control", "ppiq-std-field__textarea", className)}
      {...rest}
    />
  ),
);

StandardPageTextArea.displayName = "StandardPageTextArea";

export const StandardPageTable = forwardRef<HTMLTableElement, TableHTMLAttributes<HTMLTableElement>>(
  ({ className, children, ...rest }, ref) => (
    <table
      ref={ref}
      className={cx("ppiq-std-table", className)}
      {...rest}
    >
      {children}
    </table>
  ),
);

StandardPageTable.displayName = "StandardPageTable";
`
);

// ============================================================
// 2. Replace native JSX tags in the validator-failing Admin files
// ============================================================

const targetFiles = [
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminImportingDataTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminJobsMonitorTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminSchemaConfigurationTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/CanonicalSchemaMappingPanel.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/TwoStageImportMonitorPanel.tsx",
];

function replaceTags(source) {
  let text = source;

  const used = new Set();

  const replacements = [
    [/(\<)button\b/g, "$1StandardPageButton", /(\<\/)button(\>)/g, "$1StandardPageButton$2", "StandardPageButton"],
    [/(\<)input\b/g, "$1StandardPageInput", null, null, "StandardPageInput"],
    [/(\<)select\b/g, "$1StandardPageSelect", /(\<\/)select(\>)/g, "$1StandardPageSelect$2", "StandardPageSelect"],
    [/(\<)textarea\b/g, "$1StandardPageTextArea", /(\<\/)textarea(\>)/g, "$1StandardPageTextArea$2", "StandardPageTextArea"],
    [/(\<)table\b/g, "$1StandardPageTable", /(\<\/)table(\>)/g, "$1StandardPageTable$2", "StandardPageTable"],
  ];

  for (const [openRegex, openReplacement, closeRegex, closeReplacement, component] of replacements) {
    if (openRegex.test(text)) {
      used.add(component);
      text = text.replace(openRegex, openReplacement);
      if (closeRegex) {
        text = text.replace(closeRegex, closeReplacement);
      }
    }
  }

  if (used.size === 0) return text;

  const importLine =
    `import { ${Array.from(used).sort().join(", ")} } from "@/components/standard/StandardPageCompat";`;

  if (text.includes("@/components/standard/StandardPageCompat")) {
    return text;
  }

  const importMatches = [...text.matchAll(/^import .*?;$/gm)];
  if (importMatches.length === 0) {
    return importLine + "\n" + text;
  }

  const lastImport = importMatches[importMatches.length - 1];
  const insertAt = lastImport.index + lastImport[0].length;

  return text.slice(0, insertAt) + "\n" + importLine + text.slice(insertAt);
}

for (const rel of targetFiles) {
  const file = p(...rel.split("/"));
  if (!fs.existsSync(file)) {
    console.log("SKIP missing file: " + rel);
    continue;
  }

  patch(file, replaceTags);
}

// ============================================================
// 3. Fix proof script path handling
// ============================================================

write(
  p("tools", "validation", "prove-standard-import-gate.cjs"),
  `const fs = require("node:fs");
const path = require("node:path");
const cp = require("node:child_process");

const cwd = process.cwd();

const isFrontendRoot =
  fs.existsSync(path.join(cwd, "src")) &&
  fs.existsSync(path.join(cwd, "package.json")) &&
  fs.existsSync(path.join(cwd, "scripts", "validate-standard-imports.mjs"));

const repoRoot = isFrontendRoot ? path.resolve(cwd, "..", "..") : cwd;
const web = isFrontendRoot ? cwd : path.join(repoRoot, "Frontend", "PlantProcess.Web");
const target = path.join(web, "src", "pages", "__standard_import_gate_negative__.tsx");

function run(command, options = {}) {
  return cp.spawnSync(command, {
    cwd: options.cwd ?? repoRoot,
    shell: true,
    encoding: "utf8",
    stdio: "pipe",
  });
}

if (!fs.existsSync(path.join(web, "scripts", "validate-standard-imports.mjs"))) {
  console.error("FAIL: could not locate Frontend/PlantProcess.Web/scripts/validate-standard-imports.mjs");
  console.error("cwd=" + cwd);
  console.error("web=" + web);
  process.exit(1);
}

try {
  fs.writeFileSync(
    target,
    "export function StandardImportGateNegative(){ return <button>bad</button>; }\\n",
    "utf8",
  );

  const negative = run("npm run validate:standard-imports", { cwd: web });

  if (negative.status === 0) {
    console.error("FAIL: standard-import validator did not fail for native <button>.");
    console.error(negative.stdout);
    console.error(negative.stderr);
    process.exit(1);
  }

  console.log("OK negative proof: native <button> was rejected.");
} finally {
  if (fs.existsSync(target)) fs.unlinkSync(target);
}

const positive = run("npm run validate:standard-imports", { cwd: web });

if (positive.status !== 0) {
  console.error("FAIL: standard-import validator did not pass after removing scratch violation.");
  console.error(positive.stdout);
  console.error(positive.stderr);
  process.exit(1);
}

console.log("PPIQ-T205 passed: standard-import gate rejects violation and passes clean tree.");
`
);

console.log("");
console.log("OK frontend T205 standard-import remaining gaps patched.");
`
