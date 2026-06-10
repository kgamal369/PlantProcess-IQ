const fs = require("fs");
const path = require("path");

const root = process.cwd();
const webRoot = fs.existsSync(path.join(root, "Frontend", "PlantProcess.Web"))
  ? path.join(root, "Frontend", "PlantProcess.Web")
  : root;

const srcRoot = path.join(webRoot, "src");
const failMode = process.argv.includes("--fail");
const marker = "PPIQ_P2_T011_TABS_NAVIGATION_STANDARDIZATION";

function relFromWeb(file) {
  return path.relative(webRoot, file).replaceAll(path.sep, "/");
}

function walk(dir, predicate) {
  const output = [];

  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const item = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (
        entry.name === "node_modules" ||
        entry.name === "dist" ||
        entry.name === "coverage" ||
        entry.name === "playwright-report" ||
        entry.name === "test-results" ||
        entry.name === "__snapshots__"
      ) {
        continue;
      }

      output.push(...walk(item, predicate));
    } else if (!predicate || predicate(item)) {
      output.push(item);
    }
  }

  return output;
}

function isAuditedSource(file) {
  const rel = relFromWeb(file);

  if (!rel.startsWith("src/pages/") && !rel.startsWith("src/components/")) return false;
  if (rel.startsWith("src/components/standard/")) return false;
  if (rel.includes("/__tests__/")) return false;
  if (rel.endsWith(".test.tsx") || rel.endsWith(".test.ts") || rel.endsWith(".stories.tsx") || rel.endsWith(".stories.ts")) return false;

  return rel.endsWith(".tsx");
}

function countPattern(text, regex) {
  let count = 0;
  regex.lastIndex = 0;
  while (regex.exec(text)) count += 1;
  return count;
}

function detectPage(rel) {
  const file = rel.split("/").pop() ?? rel;
  return file.replace(/\.(implementation\.)?tsx$/, "");
}

function classifyNavigation(text, rel) {
  if (rel.includes("AppLayout")) return "primary-navigation";
  if (/breadcrumb|Breadcrumb|aria-current=["']page["']/.test(text)) return "breadcrumb";
  if (/tab|tabs|Tab|Tabs|segment|Segment/.test(text)) return "in-page-tabs";
  if (/nav|navigation|NavLink|Link/.test(text)) return "sub-navigation";
  return "navigation";
}

function statusFor(text) {
  if (text.includes("StandardTabs") || text.includes("data-ppiq-tabs-standardization")) return "standard";
  return "non-standard";
}

function hasKeyboardSupport(text) {
  return /onKeyDown|ArrowRight|ArrowLeft|ArrowUp|ArrowDown|Home|End|role=["']tablist["']/.test(text);
}

function hasActiveIndicator(text) {
  return /active|selected|aria-selected|aria-current|isActive|NavLink/.test(text);
}

function hasBadge(text) {
  return /badge|Badge|count|Counter|notification/i.test(text);
}

function hasLazyLoading(text) {
  return /lazy|Suspense|React\.lazy|mountMode|active-only/i.test(text);
}

function hasResponsive(text) {
  return /overflow-x|mobile|@media|responsive|collapsed|sidebar/i.test(text);
}

const files = walk(srcRoot, isAuditedSource);
const inventory = [];
const findings = [];

for (const file of files) {
  const rel = relFromWeb(file);
  const text = fs.readFileSync(file, "utf8");

  const navCount = countPattern(text, /<(nav|NavLink|Link)\b/g);
  const tabRoleCount = countPattern(text, /role=["']tab(list|)["']/g);
  const standardTabsCount = countPattern(text, /<StandardTabs\b/g);
  const localTabWords = countPattern(text, /\b(tabs?|Tabs?|segment(?:ed)?|breadcrumb|Breadcrumb)\b/g);

  const shouldInventory =
    navCount > 0 ||
    tabRoleCount > 0 ||
    standardTabsCount > 0 ||
    (localTabWords > 2 && /active|selected|set[A-Z]|useState/.test(text));

  if (!shouldInventory) continue;

  const itemCountCandidates = [
    countPattern(text, /\{[^}]*\.map\(/g),
    countPattern(text, /items\s*[:=]\s*\[/g),
    countPattern(text, /tabs\s*[:=]\s*\[/gi),
    countPattern(text, /navItems\s*[:=]\s*\[/gi),
  ];

  const itemCount =
    standardTabsCount > 0
      ? "dynamic-or-standard"
      : itemCountCandidates.some((x) => x > 0)
        ? "dynamic"
        : String(Math.max(navCount, tabRoleCount, 1));

  const row = {
    task: "PPIQ-T011",
    file: rel,
    line: 1,
    page: detectPage(rel),
    currentImplementation: standardTabsCount > 0 ? "StandardTabs" : navCount > 0 ? "navigation" : "tabs-or-segmented-control",
    itemCount,
    navigationType: classifyNavigation(text, rel),
    activeIndicatorStyle: hasActiveIndicator(text) ? "detected" : "missing",
    badgeSupport: hasBadge(text) ? "present" : "missing",
    keyboardNavigation: hasKeyboardSupport(text) ? "present" : "missing",
    lazyLoading: hasLazyLoading(text) ? "present" : "missing",
    responsiveBehavior: hasResponsive(text) ? "present" : "not-detected",
    standardStatus: statusFor(text),
  };

  inventory.push(row);
}

const standardTabsPath = path.join(webRoot, "src", "components", "standard", "StandardTabs.tsx");
const standardCssPath = path.join(webRoot, "src", "components", "standard", "standard-components.css");
const standardIndexPath = path.join(webRoot, "src", "components", "standard", "index.ts");

const standardTabsText = fs.existsSync(standardTabsPath)
  ? fs.readFileSync(standardTabsPath, "utf8")
  : "";

const cssText = fs.existsSync(standardCssPath)
  ? fs.readFileSync(standardCssPath, "utf8")
  : "";

const indexText = fs.existsSync(standardIndexPath)
  ? fs.readFileSync(standardIndexPath, "utf8")
  : "";

for (const token of [
  marker,
  "StandardTabItem",
  "orientation",
  "variant",
  "badge",
  "disabled",
  "href",
  "syncSearchParam",
  "mountMode",
  "ArrowRight",
  "ArrowLeft",
  "Home",
  "End",
  "aria-selected",
  "aria-orientation",
  "role=\"tablist\"",
]) {
  if (!standardTabsText.includes(token)) {
    findings.push({
      file: "src/components/standard/StandardTabs.tsx",
      line: 0,
      kind: "standard-tabs-contract",
      message: "StandardTabs missing token: " + token,
      snippet: "",
    });
  }
}

for (const token of [
  marker,
  "ppiq-std-tabs__list",
  "ppiq-std-tabs__item--active",
  "ppiq-std-tabs__badge",
  "focus-visible",
  "@media",
]) {
  if (!cssText.includes(token)) {
    findings.push({
      file: "src/components/standard/standard-components.css",
      line: 0,
      kind: "standard-tabs-css-contract",
      message: "StandardTabs CSS missing token: " + token,
      snippet: "",
    });
  }
}

if (!indexText.includes("StandardTabs")) {
  findings.push({
    file: "src/components/standard/index.ts",
    line: 0,
    kind: "standard-index-export",
    message: "standard index must export StandardTabs",
    snippet: "",
  });
}

if (inventory.length < 4) {
  findings.push({
    file: "docs/ui-standards/tabs-inventory.csv",
    line: 0,
    kind: "inventory-coverage",
    message: "tabs/navigation inventory should include at least known AppLayout/Admin navigation instances",
    snippet: "inventoryCount=" + inventory.length,
  });
}

const header = [
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
];

function csvCell(value) {
  const text = String(value ?? "");
  if (/[",\n\r]/.test(text)) return '"' + text.replaceAll('"', '""') + '"';
  return text;
}

const csv = [
  header.join(","),
  ...inventory.map((row) => header.map((key) => csvCell(row[key])).join(",")),
].join("\n") + "\n";

const uiStandardsDir = path.join(webRoot, "docs", "ui-standards");
fs.mkdirSync(uiStandardsDir, { recursive: true });
fs.writeFileSync(path.join(uiStandardsDir, "tabs-inventory.csv"), csv, "utf8");

const outDir = path.join(root, "Documentation", "P2-T011_TabsNavigation_Latest");
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(path.join(outDir, "tabs-navigation-audit.json"), JSON.stringify({
  marker,
  generatedAtUtc: new Date().toISOString(),
  inventoryCount: inventory.length,
  nonStandardCount: inventory.filter((row) => row.standardStatus !== "standard").length,
  findingCount: findings.length,
  inventory,
  findings,
}, null, 2));

const md = [
  "# P2-T011 Tabs / Navigation Audit",
  "",
  "Marker: " + marker,
  "",
  "- Inventory rows: " + inventory.length,
  "- Non-standard rows: " + inventory.filter((row) => row.standardStatus !== "standard").length,
  "- Findings: " + findings.length,
  "",
  findings.length === 0
    ? "## Result\n\nGREEN — StandardTabs contract exists and tabs/navigation inventory was generated."
    : "## Findings\n\n" + findings.map((x) => "- " + x.file + ":" + x.line + " [" + x.kind + "] " + x.message + (x.snippet ? " — " + x.snippet : "")).join("\n"),
  "",
].join("\n");

fs.writeFileSync(path.join(outDir, "tabs-navigation-audit.md"), md, "utf8");

console.log(JSON.stringify({
  marker,
  inventoryCount: inventory.length,
  nonStandardCount: inventory.filter((row) => row.standardStatus !== "standard").length,
  findingCount: findings.length,
}, null, 2));

if (failMode && findings.length > 0) {
  console.error("[RED] P2-T011 tabs/navigation audit failed. See Documentation/P2-T011_TabsNavigation_Latest/tabs-navigation-audit.md");
  process.exit(1);
}

console.log("[GREEN] P2-T011 tabs/navigation audit passed.");