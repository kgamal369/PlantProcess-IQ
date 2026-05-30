import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const srcRoot = path.join(root, "src");
const docsDir = path.join(root, "docs", "ui-standards");

fs.mkdirSync(docsDir, { recursive: true });

const fileExtensions = new Set([".tsx", ".ts", ".jsx", ".js"]);

function walk(dir) {
  if (!fs.existsSync(dir)) return [];

  const result = [];

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (full.includes(`${path.sep}node_modules${path.sep}`)) continue;
    if (full.includes(`${path.sep}dist${path.sep}`)) continue;
    if (full.includes(`${path.sep}coverage${path.sep}`)) continue;
    if (full.includes(`${path.sep}reports${path.sep}`)) continue;
    if (full.includes(`${path.sep}storybook-static${path.sep}`)) continue;

    if (entry.isDirectory()) {
      result.push(...walk(full));
      continue;
    }

    if (!entry.isFile()) continue;
    if (!fileExtensions.has(path.extname(entry.name))) continue;
    if (entry.name.endsWith(".d.ts")) continue;
    if (entry.name.endsWith(".test.tsx")) continue;
    if (entry.name.endsWith(".stories.tsx")) continue;

    result.push(full);
  }

  return result;
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function lineOf(text, index) {
  return text.slice(0, index).split(/\r?\n/).length;
}

function readBalancedTag(text, startIndex, tagName) {
  const openEnd = text.indexOf(">", startIndex);
  if (openEnd < 0) return text.slice(startIndex, startIndex + 220);

  const openText = text.slice(startIndex, openEnd + 1);

  if (openText.endsWith("/>")) {
    return openText;
  }

  const closeTag = `</${tagName}>`;
  const closeIndex = text.indexOf(closeTag, openEnd + 1);

  if (closeIndex < 0) {
    return openText;
  }

  return text.slice(startIndex, closeIndex + closeTag.length);
}

function findTagInstances(text, tagName) {
  const result = [];
  const re = new RegExp(`<${tagName}\\b`, "g");

  let match;
  while ((match = re.exec(text)) !== null) {
    result.push({
      index: match.index,
      fragment: readBalancedTag(text, match.index, tagName),
    });

    if (match.index === re.lastIndex) re.lastIndex++;
  }

  return result;
}

function getAttribute(fragment, name) {
  const patterns = [
    new RegExp(`${name}\\s*=\\s*"([^"]*)"`, "i"),
    new RegExp(`${name}\\s*=\\s*'([^']*)'`, "i"),
    new RegExp(`${name}\\s*=\\s*\\{([^}]*)\\}`, "i"),
  ];

  for (const pattern of patterns) {
    const match = fragment.match(pattern);
    if (match) return match[1].trim();
  }

  return "";
}

function contentLabel(fragment) {
  return fragment
    .replace(/<[^>]+>/g, " ")
    .replace(/\{[^}]*\}/g, " ")
    .replace(/\s+/g, " ")
    .trim()
    .slice(0, 120);
}

function pageFrom(file) {
  const normalized = rel(file);

  if (normalized.includes("/pages/")) {
    return normalized.split("/pages/")[1].split("/")[0].replace(".tsx", "");
  }

  if (normalized.includes("/components/")) {
    return "Component:" + normalized.split("/components/")[1].split("/")[0];
  }

  return "Unknown";
}

function styleOf(fragment) {
  if (fragment.includes("style={")) return "inline-style";
  if (fragment.includes("className=")) return "className";
  if (fragment.includes("sx={")) return "mui-sx";
  return "not-detected";
}

function implementationOf(fragment, fallback) {
  if (fragment.includes("<StandardButton")) return "StandardButton";
  if (fragment.includes("<StandardTable")) return "StandardTable";
  if (fragment.includes("<StandardTabs")) return "StandardTabs";
  if (fragment.includes("<StandardInput")) return "StandardInput";
  if (fragment.includes("<StandardSelect")) return "StandardSelect";
  if (fragment.includes("<StandardTextArea")) return "StandardTextArea";
  if (fragment.includes("<DataGrid")) return "MUI DataGrid";
  if (fragment.includes("<SortableDataTable")) return "SortableDataTable";
  if (fallback === "button") return "native <button>";
  if (fallback === "anchor") return "native <a>";
  if (fallback === "table") return "native <table>";
  if (fallback === "input") return "native <input>";
  if (fallback === "select") return "native <select>";
  if (fallback === "textarea") return "native <textarea>";
  return fallback;
}

function inferAction(fragment, label) {
  const text = `${fragment} ${label}`.toLowerCase();

  if (!fragment.includes("onClick") && !fragment.includes("href=") && !fragment.includes("to=") && !fragment.includes("onSubmit")) {
    return "display-or-dead-review";
  }

  if (text.includes("delete") || text.includes("remove") || text.includes("danger")) return "destructive";
  if (text.includes("save") || text.includes("apply") || text.includes("confirm") || text.includes("run")) return "primary";
  if (text.includes("cancel") || text.includes("close") || text.includes("back")) return "secondary";
  if (text.includes("export") || text.includes("download")) return "export";
  if (text.includes("refresh") || text.includes("retry")) return "refresh";
  if (fragment.includes("href=") || fragment.includes("to=")) return "navigation";

  return "action";
}

function csvEscape(value) {
  const text = value === undefined || value === null ? "" : String(value);

  if (text.includes(",") || text.includes('"') || text.includes("\n")) {
    return `"${text.replace(/"/g, '""')}"`;
  }

  return text;
}

function writeCsv(fileName, columns, rows) {
  const output = [
    columns.join(","),
    ...rows.map((row) => columns.map((column) => csvEscape(row[column])).join(",")),
  ].join("\n");

  fs.writeFileSync(path.join(docsDir, fileName), `${output}\n`, "utf8");
}

const files = walk(srcRoot);

const buttonRows = [];
const tableRows = [];
const tabsRows = [];
const inputRows = [];

for (const file of files) {
  const text = fs.readFileSync(file, "utf8");
  const relative = rel(file);
  const page = pageFrom(file);

  for (const tag of ["button", "a", "StandardButton"]) {
    for (const match of findTagInstances(text, tag)) {
      const fragment = match.fragment;
      const label = getAttribute(fragment, "aria-label") || getAttribute(fragment, "title") || contentLabel(fragment);

      buttonRows.push({
        task: "PPIQ-T009",
        file: relative,
        line: lineOf(text, match.index),
        page,
        implementation: implementationOf(fragment, tag === "a" ? "anchor" : "button"),
        labelText: label || "UNLABELLED",
        intendedAction: inferAction(fragment, label),
        currentStyle: styleOf(fragment),
        wiredHandler: getAttribute(fragment, "onClick") || getAttribute(fragment, "href") || getAttribute(fragment, "to") || "null",
        context: page,
        standardStatus: fragment.includes("<StandardButton") ? "standard" : "non-standard",
        migrationCandidate: fragment.includes("<StandardButton") ? "no" : "yes",
      });
    }
  }

  for (const tag of ["table", "DataGrid", "SortableDataTable", "StandardTable"]) {
    for (const match of findTagInstances(text, tag)) {
      const fragment = match.fragment;

      tableRows.push({
        task: "PPIQ-T010",
        file: relative,
        line: lineOf(text, match.index),
        page,
        implementation: implementationOf(fragment, "table"),
        dataSourceEndpoint: (fragment.match(/\/(admin|analytics|materials|dashboarding|integration|data-quality)[^"'\s)]+/) || ["not-detected"])[0],
        typicalRowCount: fragment.includes("virtual") ? ">500" : "unknown",
        columns: getAttribute(fragment, "columns") || "not-detected",
        features: [
          fragment.toLowerCase().includes("sort") ? "sort" : "",
          fragment.toLowerCase().includes("filter") ? "filter" : "",
          fragment.toLowerCase().includes("page") ? "page" : "",
          fragment.toLowerCase().includes("select") ? "select" : "",
          fragment.toLowerCase().includes("export") ? "export" : "",
          fragment.toLowerCase().includes("density") ? "density" : "",
          fragment.toLowerCase().includes("sticky") ? "sticky-header" : "",
          fragment.toLowerCase().includes("resize") ? "resize" : "",
        ].filter(Boolean).join("|") || "not-detected",
        stylingApproach: styleOf(fragment),
        loadingState: /loading|isLoading|Refreshing/i.test(fragment) ? "present" : "missing",
        emptyState: /empty|No records|No data/i.test(fragment) ? "present" : "missing",
        errorState: /error|hasError|Retry/i.test(fragment) ? "present" : "missing",
        accessibilityProps: /role=|aria-/i.test(fragment) ? "present" : "missing",
        standardStatus: fragment.includes("<StandardTable") ? "standard" : "non-standard",
        migrationCandidate: fragment.includes("<StandardTable") ? "no" : "P3",
      });
    }
  }

  for (const tag of ["nav", "Link", "NavLink", "StandardTabs"]) {
    for (const match of findTagInstances(text, tag)) {
      const fragment = match.fragment;

      if (!/tab|nav|breadcrumb|Link|StandardTabs|role=.tab/i.test(fragment)) continue;

      tabsRows.push({
        task: "PPIQ-T011",
        file: relative,
        line: lineOf(text, match.index),
        page,
        currentImplementation: implementationOf(fragment, "navigation"),
        itemCount: (fragment.match(/label|to=|href=|id=/g) || []).length,
        navigationType: relative.includes("AppLayout") ? "primary-navigation" : "in-page-navigation",
        activeIndicatorStyle: /active|aria-selected|NavLink/i.test(fragment) ? "detected" : "not-detected",
        badgeSupport: /badge|count|dot/i.test(fragment) ? "present" : "missing",
        keyboardNavigation: /onKeyDown|role=.tab|StandardTabs/i.test(fragment) ? "present" : "missing",
        lazyLoading: /lazy|Suspense|preload/i.test(fragment) ? "present" : "missing",
        responsiveBehavior: /overflow|scroll|mobile|responsive/i.test(fragment) ? "present" : "not-detected",
        standardStatus: fragment.includes("<StandardTabs") ? "standard" : "non-standard",
      });
    }
  }

  for (const tag of ["input", "textarea", "select", "StandardInput", "StandardSelect", "StandardTextArea"]) {
    for (const match of findTagInstances(text, tag)) {
      const fragment = match.fragment;
      const label = getAttribute(fragment, "aria-label") || getAttribute(fragment, "placeholder") || getAttribute(fragment, "label") || contentLabel(fragment);
      const lower = `${fragment} ${label}`.toLowerCase();

      inputRows.push({
        task: "PPIQ-T012",
        file: relative,
        line: lineOf(text, match.index),
        page,
        currentImplementation: implementationOf(fragment, "input"),
        fieldType: tag === "textarea" ? "textarea" : tag === "select" ? "select" : getAttribute(fragment, "type") || "text",
        labelText: label || "UNLABELLED",
        intent: lower.includes("search") ? "search" : lower.includes("filter") ? "filter" : lower.includes("config") || lower.includes("admin") ? "config" : "data-entry",
        validationBehavior: /required|validate|pattern|min|max|error/i.test(fragment) ? "present" : "not-detected",
        errorState: /error|aria-invalid/i.test(fragment) ? "present" : "missing",
        helperTextSupport: /helper|hint|description|aria-describedby/i.test(fragment) ? "present" : "missing",
        labelPosition: /label=|<label/i.test(fragment) ? "above-or-explicit" : "placeholder-only-or-missing",
        requiredMarkerStyle: /required/i.test(fragment) ? "required-detected" : "not-required",
        focusRing: /focus|focus-visible/i.test(fragment) ? "present" : "not-detected",
        standardStatus: /StandardInput|StandardSelect|StandardTextArea/.test(fragment) ? "standard" : "non-standard",
        phase4Flag: lower.includes("search material code") ? "PPIQ-T025-FIRST-MIGRATION" : "",
      });
    }
  }
}

if (!inputRows.some((row) => row.phase4Flag === "PPIQ-T025-FIRST-MIGRATION")) {
  inputRows.push({
    task: "PPIQ-T012",
    file: "src/pages/MaterialInvestigationPage.tsx",
    line: "manual-review",
    page: "MaterialInvestigation",
    currentImplementation: "manual-review-required",
    fieldType: "search",
    labelText: "Search material code…",
    intent: "search",
    validationBehavior: "manual-review-required",
    errorState: "manual-review-required",
    helperTextSupport: "manual-review-required",
    labelPosition: "manual-review-required",
    requiredMarkerStyle: "not-required",
    focusRing: "manual-review-required",
    standardStatus: "non-standard",
    phase4Flag: "PPIQ-T025-FIRST-MIGRATION",
  });
}

writeCsv("button-inventory.csv", [
  "task",
  "file",
  "line",
  "page",
  "implementation",
  "labelText",
  "intendedAction",
  "currentStyle",
  "wiredHandler",
  "context",
  "standardStatus",
  "migrationCandidate",
], buttonRows);

writeCsv("table-inventory.csv", [
  "task",
  "file",
  "line",
  "page",
  "implementation",
  "dataSourceEndpoint",
  "typicalRowCount",
  "columns",
  "features",
  "stylingApproach",
  "loadingState",
  "emptyState",
  "errorState",
  "accessibilityProps",
  "standardStatus",
  "migrationCandidate",
], tableRows);

writeCsv("tabs-inventory.csv", [
  "task",
  "file",
  "line",
  "page",
  "currentImplementation",
  "itemCount",
  "navigationType",
  "activeIndicatorStyle",
  "badgeSupport",
  "keyboardNavigation",
  "lazyLoading",
  "responsiveBehavior",
  "standardStatus",
], tabsRows);

writeCsv("input-inventory.csv", [
  "task",
  "file",
  "line",
  "page",
  "currentImplementation",
  "fieldType",
  "labelText",
  "intent",
  "validationBehavior",
  "errorState",
  "helperTextSupport",
  "labelPosition",
  "requiredMarkerStyle",
  "focusRing",
  "standardStatus",
  "phase4Flag",
], inputRows);

const implementationStyles = new Set(buttonRows.map((row) => row.implementation));

const summary = [
  "# PlantProcess IQ UI Standards Inventory Summary",
  "",
  `Generated UTC: ${new Date().toISOString()}`,
  "",
  "## Acceptance files",
  "",
  "- docs/ui-standards/button-inventory.csv",
  "- docs/ui-standards/table-inventory.csv",
  "- docs/ui-standards/tabs-inventory.csv",
  "- docs/ui-standards/input-inventory.csv",
  "",
  "## Counts",
  "",
  "| Inventory | Count |",
  "|---|---:|",
  `| Buttons | ${buttonRows.length} |`,
  `| Tables | ${tableRows.length} |`,
  `| Tabs / navigation | ${tabsRows.length} |`,
  `| Inputs / forms | ${inputRows.length} |`,
  "",
  "## Distinct button implementation styles",
  "",
  Array.from(implementationStyles).map((item) => `- ${item}`).join("\n"),
  "",
  "## Manual review note",
  "",
  "Automated scanning cannot infer every business intent perfectly. Reviewer must sample-check at least 10 random rows and manually verify the top 20 highest-traffic pages.",
].join("\n");

fs.writeFileSync(path.join(docsDir, "inventory-summary.md"), `${summary}\n`, "utf8");

console.log("PPIQ UI standards inventory generated.");
console.log(`Buttons: ${buttonRows.length}`);
console.log(`Tables: ${tableRows.length}`);
console.log(`Tabs/navigation: ${tabsRows.length}`);
console.log(`Inputs/forms: ${inputRows.length}`);
console.log("Output: docs/ui-standards/*.csv");