const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".pack_b_backup", "pack_b2_widget_content_v2_" + timestamp);

const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");
const targetPath = path.join(
  frontendRoot,
  "src",
  "components",
  "dashboard",
  "widget-builder",
  "WidgetBuilderWizardContent.implementation.tsx"
);

const contentDir = path.join(
  frontendRoot,
  "src",
  "components",
  "dashboard",
  "widget-builder",
  "content"
);

const evidencePath = path.join(root, "docs", "pack-b", "PACK_B_IMPLEMENTATION_EVIDENCE.md");

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(file) {
  return fs.existsSync(file);
}

function isFile(file) {
  return exists(file) && fs.statSync(file).isFile();
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function write(file, content) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + rel(file));
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function backup(file) {
  if (!isFile(file)) return;
  const target = path.join(backupRoot, path.relative(root, file));
  ensureDir(path.dirname(target));
  fs.copyFileSync(file, target);
}

function lineCount(file) {
  return isFile(file) ? read(file).replace(/\r\n/g, "\n").split("\n").length : 0;
}

function run(name, args, cwd = root, optional = false) {
  console.log("");
  console.log("---- " + name);

  try {
    cp.execFileSync(args[0], args.slice(1), {
      cwd,
      stdio: "inherit",
      shell: false
    });
    return { ok: true };
  } catch (error) {
    if (optional) {
      console.warn("[WARN] Optional step failed: " + name);
      console.warn(error.message || String(error));
      return { ok: false, error: error.message || String(error) };
    }

    throw error;
  }
}

function npmArgs(...args) {
  return process.platform === "win32" ? ["cmd", "/c", "npm", ...args] : ["npm", ...args];
}

function normalize(text) {
  return text.replace(/\r\n/g, "\n");
}

function updateState(line, state) {
  for (let i = 0; i < line.length; i += 1) {
    const ch = line[i];
    const next = line[i + 1];

    if (state.inBlockComment) {
      if (ch === "*" && next === "/") {
        state.inBlockComment = false;
        i += 1;
      }
      continue;
    }

    if (state.inString) {
      if (state.escape) {
        state.escape = false;
        continue;
      }

      if (ch === "\\") {
        state.escape = true;
        continue;
      }

      if (ch === state.inString) {
        state.inString = null;
      }

      continue;
    }

    if (ch === "/" && next === "*") {
      state.inBlockComment = true;
      i += 1;
      continue;
    }

    if (ch === "'" || ch === '"' || ch === "`") {
      state.inString = ch;
      continue;
    }

    if (ch === "{") state.depth += 1;
    if (ch === "}") state.depth = Math.max(0, state.depth - 1);
  }
}

function findImportEnd(lines) {
  let last = -1;
  let inImport = false;

  for (let i = 0; i < lines.length; i += 1) {
    const line = lines[i];

    if (/^\s*import\b/.test(line)) {
      inImport = true;
      last = i;
      if (line.includes(";")) inImport = false;
      continue;
    }

    if (inImport) {
      last = i;
      if (line.includes(";")) inImport = false;
      continue;
    }

    if (line.trim() === "") {
      if (last >= 0) last = i;
      continue;
    }

    if (last >= 0) break;
  }

  return Math.max(0, last);
}

function findTopLevelDeclarations(lines, startIndex) {
  const declarations = [];
  const state = {
    depth: 0,
    inBlockComment: false,
    inString: null,
    escape: false
  };

  const declRegex = /^\s*(export\s+)?(default\s+)?(async\s+)?(function|const|let|var|class|interface|type|enum)\s+([A-Za-z0-9_]+)/;

  for (let i = startIndex; i < lines.length; i += 1) {
    const beforeDepth = state.depth;
    const line = lines[i];
    const match = beforeDepth === 0 ? line.match(declRegex) : null;

    if (match) {
      declarations.push({
        name: match[5],
        kind: match[4],
        start: i
      });
    }

    updateState(line, state);
  }

  for (let i = 0; i < declarations.length; i += 1) {
    declarations[i].end =
      i + 1 < declarations.length ? declarations[i + 1].start - 1 : lines.length - 1;
  }

  return declarations;
}

function block(lines, declaration) {
  return lines.slice(declaration.start, declaration.end + 1).join("\n").trimEnd();
}

function requireDecl(map, name) {
  if (!map.has(name)) throw new Error("Missing declaration: " + name);
  return map.get(name);
}

function replaceExportKeywords(text) {
  return text
    .replace(/^export\s+function\s+/gm, "function ")
    .replace(/^export\s+const\s+/gm, "const ")
    .replace(/^export\s+interface\s+/gm, "interface ")
    .replace(/^export\s+type\s+/gm, "type ");
}

function forceExportFunction(text, name) {
  const re = new RegExp("(^|\\n)function\\s+" + name + "\\s*\\(", "m");
  return text.replace(re, "$1export function " + name + "(");
}

function forceExportConst(text, name) {
  const re = new RegExp("(^|\\n)const\\s+" + name + "\\b", "m");
  return text.replace(re, "$1export const " + name);
}

function forceExportType(text, keyword, name) {
  const re = new RegExp("(^|\\n)" + keyword + "\\s+" + name + "\\b", "m");
  return text.replace(re, "$1export " + keyword + " " + name);
}

function convertRelativeImports(importBlock) {
  return importBlock
    .replace(/from\s+"\.\/\.\/\.\/api\/productApiClient"/g, 'from "../../../../api/productApiClient"')
    .replace(/from\s+"\.\/\.\/api\/productApiClient"/g, 'from "../../../../api/productApiClient"')
    .replace(/from\s+"\.\/api\/productApiClient"/g, 'from "../../../../api/productApiClient"')
    .replace(/from\s+"\.\/WidgetScriptStep"/g, 'from "../WidgetScriptStep"')
    .replace(/from\s+"\.\/WidgetScriptBuilderPanel"/g, 'from "../WidgetScriptBuilderPanel"');
}

function findFunctionSignatureEnd(lines) {
  for (let i = 0; i < lines.length; i += 1) {
    if (/\)\s*\{\s*$/.test(lines[i])) {
      return i;
    }
  }

  throw new Error("Could not locate WidgetBuilderWizardContent signature end.");
}

function findMainReturnStart(lines, signatureEnd) {
  const state = {
    depth: 1,
    inBlockComment: false,
    inString: null,
    escape: false
  };

  for (let i = signatureEnd + 1; i < lines.length; i += 1) {
    const beforeDepth = state.depth;
    const line = lines[i];

    if (
      beforeDepth === 1 &&
      (/^\s*return\s*\(/.test(line) || /^\s*return\s*</.test(line))
    ) {
      return i;
    }

    updateState(line, state);
  }

  throw new Error("Could not locate main JSX return block.");
}

function extractNamesFromPreReturn(preReturn) {
  const names = new Set([
    "isOpen",
    "dashboardDefinitionId",
    "existingWidget",
    "onClose",
    "onWidgetSaved",
    "shouldRender"
  ]);

  const lines = preReturn.split("\n");
  let depth = 0;

  for (const line of lines) {
    const beforeDepth = depth;
    const trimmed = line.trim();

    if (beforeDepth === 0) {
      let match = trimmed.match(/^const\s+\[([A-Za-z0-9_]+)\s*,\s*([A-Za-z0-9_]+)\]/);
      if (match) {
        names.add(match[1]);
        names.add(match[2]);
      }

      match = trimmed.match(/^const\s+([A-Za-z0-9_]+)\s*=/);
      if (match) names.add(match[1]);

      match = trimmed.match(/^let\s+([A-Za-z0-9_]+)\s*=/);
      if (match) names.add(match[1]);

      match = trimmed.match(/^function\s+([A-Za-z0-9_]+)\s*\(/);
      if (match) names.add(match[1]);

      match = trimmed.match(/^const\s+\{\s*$/);
      if (match) {
        // Multiline destructuring is uncommon here; keep handled by explicit extraction below.
      }

      match = trimmed.match(/^const\s+\{\s*([^}]+)\s*\}\s*=/);
      if (match) {
        for (const part of match[1].split(",")) {
          const clean = part.trim().split(":")[0]?.trim();
          if (clean && /^[A-Za-z0-9_]+$/.test(clean)) names.add(clean);
        }
      }
    }

    for (const ch of line) {
      if (ch === "{") depth += 1;
      if (ch === "}") depth = Math.max(0, depth - 1);
    }
  }

  return Array.from(names).sort();
}

function splitMainComponent(mainBlock) {
  const lines = mainBlock.split("\n");
  const signatureEnd = findFunctionSignatureEnd(lines);
  const returnStart = findMainReturnStart(lines, signatureEnd);

  let preReturn = lines.slice(signatureEnd + 1, returnStart).join("\n").trimEnd();
  let returnBlock = lines.slice(returnStart, lines.length - 1).join("\n").trimEnd();

  preReturn = preReturn.replace(/^\s*if\s*\(!isOpen\)\s*return\s+null;\s*$/m, "  const shouldRender = isOpen;");

  if (!preReturn.includes("shouldRender")) {
    preReturn += "\n  const shouldRender = isOpen;";
  }

  const names = extractNamesFromPreReturn(preReturn);
  const returnObject = names.map((name) => "    " + name + ",").join("\n");

  return {
    preReturn,
    returnBlock,
    names,
    hookSource:
      "export function useWidgetBuilderWizardContentModel(props: WidgetBuilderWizardProps) {\n" +
      "  const { isOpen, dashboardDefinitionId, existingWidget, onClose, onWidgetSaved } = props;\n" +
      preReturn +
      "\n\n" +
      "  return {\n" +
      returnObject +
      "\n  };\n" +
      "}\n"
  };
}

function writeMainReports(report) {
  write(
    path.join(root, "docs", "pack-b", "PACK_B2_WIDGET_CONTENT_SPLIT_REPORT.json"),
    JSON.stringify(report, null, 2) + "\n"
  );

  let evidence = isFile(evidencePath)
    ? normalize(read(evidencePath))
    : "# PlantProcess IQ Pack B Evidence\n";

  if (!evidence.includes("PPIQ_PACK_B2_WIDGET_BUILDER_CONTENT_SPLIT_V2")) {
    evidence += [
      "",
      "## Pack B-2 WidgetBuilderWizardContent split v2",
      "",
      "- Marker: `PPIQ_PACK_B2_WIDGET_BUILDER_CONTENT_SPLIT_V2`.",
      "- Fixed parser for multiline destructured component signature.",
      "- Replaced `WidgetBuilderWizardContent.implementation.tsx` with a thin wrapper.",
      "- Extracted types, helpers, step components, model hook, view, and orchestrator.",
      "- Generated report: `docs/pack-b/PACK_B2_WIDGET_CONTENT_SPLIT_REPORT.json`.",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

function main() {
  console.log("=================================================================================================");
  console.log("PlantProcess IQ Pack B-2 v2 — WidgetBuilderWizardContent split");
  console.log("=================================================================================================");
  console.log("Backup folder: " + backupRoot);

  ensureDir(backupRoot);
  ensureDir(contentDir);

  if (!isFile(targetPath)) {
    throw new Error("Missing target file: " + rel(targetPath));
  }

  backup(targetPath);

  const original = normalize(read(targetPath));
  const lines = original.split("\n");
  const importEnd = findImportEnd(lines);
  const importBlock = lines.slice(0, importEnd + 1).join("\n");
  const commonImports = convertRelativeImports(importBlock);

  const declarations = findTopLevelDeclarations(lines, importEnd + 1);
  const byName = new Map(declarations.map((d) => [d.name, d]));

  const typeNames = [
    "WidgetBuilderWizardProps",
    "WizardStep",
    "RelativeDateUnit",
    "WidgetBuilderState",
    "ValidationIssue"
  ];

  const typeBlocks = typeNames
    .filter((name) => byName.has(name))
    .map((name) => {
      const declaration = requireDecl(byName, name);
      const text = replaceExportKeywords(block(lines, declaration));
      if (declaration.kind === "interface") return forceExportType(text, "interface", name);
      if (declaration.kind === "type") return forceExportType(text, "type", name);
      return text;
    })
    .join("\n\n");

  write(
    path.join(contentDir, "WidgetBuilderWizardContent.types.ts"),
    [
      "import type {",
      "  DashboardWidgetDefinitionRecord,",
      "  DashboardWidgetFilters,",
      "} from \"../../../../api/productApiClient\";",
      "",
      typeBlocks,
      ""
    ].join("\n")
  );

  const helperNames = [
    "stepOrder",
    "stepLabels",
    "defaultState",
    "generateWidgetCode",
    "parseJson",
    "toInputDateTime",
    "fromInputDateTime",
    "relativeFromUtc",
    "formatError",
    "mapValidationIssues",
    "isCompatible",
    "inferCategoryKey",
    "inferValueKey",
    "selectFieldForDimension"
  ];

  const helperBlocks = helperNames
    .filter((name) => byName.has(name))
    .map((name) => {
      const declaration = requireDecl(byName, name);
      let text = replaceExportKeywords(block(lines, declaration));
      if (declaration.kind === "function") text = forceExportFunction(text, name);
      if (declaration.kind === "const") text = forceExportConst(text, name);
      return text;
    })
    .join("\n\n");

  write(
    path.join(contentDir, "WidgetBuilderWizardContent.helpers.ts"),
    [
      commonImports,
      "import type { ValidationIssue, WidgetBuilderState, WizardStep } from \"./WidgetBuilderWizardContent.types\";",
      "",
      helperBlocks,
      ""
    ].join("\n")
  );

  const wizardSection = byName.has("WizardSection")
    ? forceExportFunction(replaceExportKeywords(block(lines, requireDecl(byName, "WizardSection"))), "WizardSection")
    : "";

  function stepModule(fileName, exportNames) {
    const parts = [];

    for (const name of exportNames) {
      if (!byName.has(name)) continue;
      parts.push(forceExportFunction(replaceExportKeywords(block(lines, requireDecl(byName, name))), name));
    }

    write(
      path.join(contentDir, fileName),
      [
        commonImports,
        "import type { WidgetBuilderState } from \"./WidgetBuilderWizardContent.types\";",
        "import { mapValidationIssues, selectFieldForDimension } from \"./WidgetBuilderWizardContent.helpers\";",
        "",
        wizardSection,
        "",
        parts.join("\n\n"),
        ""
      ].join("\n")
    );
  }

  stepModule("WidgetBuilderWizardContent.data-steps.tsx", ["PurposeStep", "ChartTypeStep", "DataStep"]);
  stepModule("WidgetBuilderWizardContent.filter-step.tsx", ["FilterStep"]);
  stepModule("WidgetBuilderWizardContent.preview-steps.tsx", ["ScriptStep", "PreviewStep", "PreviewTable"]);

  const mainDecl = requireDecl(byName, "WidgetBuilderWizardContent");
  const mainBlock = replaceExportKeywords(block(lines, mainDecl));
  const split = splitMainComponent(mainBlock);

  write(
    path.join(contentDir, "WidgetBuilderWizardContent.model.ts"),
    [
      commonImports,
      "import type { WidgetBuilderWizardProps, WidgetBuilderState } from \"./WidgetBuilderWizardContent.types\";",
      "import {",
      "  defaultState,",
      "  formatError,",
      "  fromInputDateTime,",
      "  generateWidgetCode,",
      "  inferCategoryKey,",
      "  inferValueKey,",
      "  isCompatible,",
      "  mapValidationIssues,",
      "  relativeFromUtc,",
      "  stepOrder,",
      "  toInputDateTime,",
      "} from \"./WidgetBuilderWizardContent.helpers\";",
      "",
      split.hookSource,
      ""
    ].join("\n")
  );

  write(
    path.join(contentDir, "WidgetBuilderWizardContent.view.tsx"),
    [
      commonImports,
      "import { PurposeStep, ChartTypeStep, DataStep } from \"./WidgetBuilderWizardContent.data-steps\";",
      "import { FilterStep } from \"./WidgetBuilderWizardContent.filter-step\";",
      "import { ScriptStep, PreviewStep } from \"./WidgetBuilderWizardContent.preview-steps\";",
      "import { mapValidationIssues, stepLabels, stepOrder } from \"./WidgetBuilderWizardContent.helpers\";",
      "",
      "export function WidgetBuilderWizardContentView({ vm }: { vm: Record<string, any> }) {",
      "  const {",
      split.names.map((name) => "    " + name + ",").join("\n"),
      "  } = vm;",
      "",
      "  if (!shouldRender) return null;",
      "",
      split.returnBlock,
      "}",
      ""
    ].join("\n")
  );

  write(
    path.join(contentDir, "WidgetBuilderWizardContent.orchestrator.tsx"),
    [
      "import type { WidgetBuilderWizardProps } from \"./WidgetBuilderWizardContent.types\";",
      "import { useWidgetBuilderWizardContentModel } from \"./WidgetBuilderWizardContent.model\";",
      "import { WidgetBuilderWizardContentView } from \"./WidgetBuilderWizardContent.view\";",
      "",
      "export function WidgetBuilderWizardContent(props: WidgetBuilderWizardProps) {",
      "  const vm = useWidgetBuilderWizardContentModel(props);",
      "  return <WidgetBuilderWizardContentView vm={vm} />;",
      "}",
      "",
      "export { WidgetBuilderWizardContent as WidgetBuilderWizard };",
      "export default WidgetBuilderWizardContent;",
      ""
    ].join("\n")
  );

  write(
    targetPath,
    [
      "export { WidgetBuilderWizardContent, WidgetBuilderWizard } from \"./content/WidgetBuilderWizardContent.orchestrator\";",
      "export { default } from \"./content/WidgetBuilderWizardContent.orchestrator\";",
      ""
    ].join("\n")
  );

  const generatedFiles = [
    "WidgetBuilderWizardContent.types.ts",
    "WidgetBuilderWizardContent.helpers.ts",
    "WidgetBuilderWizardContent.data-steps.tsx",
    "WidgetBuilderWizardContent.filter-step.tsx",
    "WidgetBuilderWizardContent.preview-steps.tsx",
    "WidgetBuilderWizardContent.model.ts",
    "WidgetBuilderWizardContent.view.tsx",
    "WidgetBuilderWizardContent.orchestrator.tsx"
  ].map((name) => {
    const file = path.join(contentDir, name);
    return { path: rel(file), lines: lineCount(file) };
  });

  const report = {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_PACK_B2_WIDGET_BUILDER_CONTENT_SPLIT_V2",
    target: rel(targetPath),
    targetLines: lineCount(targetPath),
    generatedFiles
  };

  writeMainReports(report);

  console.log("");
  console.log("Generated file line counts:");
  for (const item of generatedFiles) {
    console.log(" - " + item.path + ": " + item.lines);
  }

  run("Frontend npm build", npmArgs("run", "build"), frontendRoot);

  if (isFile(path.join(root, "tools", "phase56", "validate-phase56.cjs"))) {
    run("Phase 5/6 validation", ["node", "tools/phase56/validate-phase56.cjs"]);
  }

  run("Pack B closure gate preview", ["node", "tools/pack-b/validate-pack-b-p05-closure.cjs"], root, true);

  console.log("");
  console.log("=================================================================================================");
  console.log("Pack B-2 v2 WidgetBuilderWizardContent split completed.");
  console.log("Target now: " + rel(targetPath) + " (" + lineCount(targetPath) + " lines)");
  console.log("Backup: " + backupRoot);
  console.log("=================================================================================================");
}

main();