const fs = require("fs");
const path = require("path");

const root = process.cwd();
const stamp = new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".phase3_backups", "P3-T14_ROUTE_REPAIR_JS_" + stamp);

function full(rel) {
  return path.join(root, rel.replaceAll("/", path.sep));
}

function exists(rel) {
  return fs.existsSync(full(rel));
}

function read(rel) {
  return fs.readFileSync(full(rel), "utf8");
}

function write(rel, text) {
  const target = full(rel);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, text.replace(/\r?\n/g, "\r\n"), "utf8");
  console.log("[P3-T14-ROUTE] wrote " + rel);
}

function backup(rel) {
  if (!exists(rel)) return;

  const source = full(rel);
  const target = path.join(backupRoot, rel.replaceAll("/", path.sep));
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.copyFileSync(source, target);
}

function findRouteFile() {
  const candidates = [
    "Frontend/PlantProcess.Web/src/AppRoutes.generated.tsx",
    "Frontend/PlantProcess.Web/src/AppRoutes.tsx",
    "Frontend/PlantProcess.Web/src/App.implementation.tsx",
    "Frontend/PlantProcess.Web/src/App.tsx",
  ];

  for (const rel of candidates) {
    if (!exists(rel)) continue;

    const text = read(rel);

    if (
      text.includes("<Route") &&
      (text.includes("</Routes>") || text.includes("</Route>")) &&
      !/^export\s+\*/m.test(text.trim())
    ) {
      return rel;
    }
  }

  throw new Error("Could not find the real route file. Checked: " + candidates.join(", "));
}

function insertAfterLastImport(text, line) {
  if (text.includes(line.trim())) return text;

  const importMatches = [...text.matchAll(/^import .*?;\s*$/gm)];
  if (importMatches.length === 0) {
    return line + "\n" + text;
  }

  const last = importMatches[importMatches.length - 1];
  const insertAt = last.index + last[0].length;
  return text.slice(0, insertAt) + "\n" + line + text.slice(insertAt);
}

function insertBeforeComponentStart(text, block) {
  if (text.includes("ValueExecutiveDashboardPage")) return text;

  const anchors = [
    "export default function App",
    "export function App",
    "function App",
    "export default function AppRoutes",
    "export function AppRoutes",
    "function AppRoutes",
    "const App =",
    "const AppRoutes =",
  ];

  for (const anchor of anchors) {
    const idx = text.indexOf(anchor);
    if (idx >= 0) {
      return text.slice(0, idx) + block + "\n" + text.slice(idx);
    }
  }

  const importMatches = [...text.matchAll(/^import .*?;\s*$/gm)];
  if (importMatches.length > 0) {
    const last = importMatches[importMatches.length - 1];
    const insertAt = last.index + last[0].length;
    return text.slice(0, insertAt) + "\n\n" + block + text.slice(insertAt);
  }

  return block + "\n" + text;
}

function insertRoute(text, routeBlock) {
  if (text.includes('path="/value/executive"')) {
    return text;
  }

  const defaultCommentAnchor = /(\s*\{\/\*\s*Default\s*\*\/\}\s*\r?\n\s*<Route\s*\r?\n\s*path="\*")/;
  if (defaultCommentAnchor.test(text)) {
    return text.replace(defaultCommentAnchor, "\n" + routeBlock + "\n$1");
  }

  const multilineDefaultAnchor = /(\s*<Route\s*\r?\n\s*path="\*")/;
  if (multilineDefaultAnchor.test(text)) {
    return text.replace(multilineDefaultAnchor, "\n" + routeBlock + "\n$1");
  }

  const inlineDefaultAnchor = /(\s*<Route\s+path="\*")/;
  if (inlineDefaultAnchor.test(text)) {
    return text.replace(inlineDefaultAnchor, "\n" + routeBlock + "\n$1");
  }

  if (text.includes("</Routes>")) {
    return text.replace("</Routes>", routeBlock + "\n                </Routes>");
  }

  if (text.includes("</Route>")) {
    return text.replace("</Route>", routeBlock + "\n                </Route>");
  }

  throw new Error("Could not find a safe route insertion anchor.");
}

function patchRouteFile() {
  const routeRel = findRouteFile();
  backup(routeRel);

  let text = read(routeRel);
  const usesLazy = text.includes("lazy(() =>") || /import\s+\{[^}]*lazy[^}]*\}\s+from\s+["']react["']/.test(text);

  if (!text.includes("ValueExecutiveDashboardPage")) {
    if (usesLazy) {
      const lazyBlock = [
        "const ValueExecutiveDashboardPage = lazy(() =>",
        "  import(\"./pages/ValueExecutive/ValueExecutiveDashboardPage\").then((m) => ({",
        "    default: m.ValueExecutiveDashboardPage,",
        "  }))",
        ");",
        ""
      ].join("\n");

      text = insertBeforeComponentStart(text, lazyBlock);
    } else {
      text = insertAfterLastImport(
        text,
        'import ValueExecutiveDashboardPage from "./pages/ValueExecutive/ValueExecutiveDashboardPage";\n'
      );
    }
  }

  const routeBlock = text.includes("withPageBoundary(")
    ? [
        "                    {/* P3-T14 value executive surface */}",
        "                    <Route",
        "                      path=\"/value/executive\"",
        "                      element={withPageBoundary(",
        "                        \"/value/executive\",",
        "                        \"Value executive dashboard is refreshing\",",
        "                        <ValueExecutiveDashboardPage />",
        "                      )}",
        "                    />",
        ""
      ].join("\n")
    : '                  <Route path="/value/executive" element={<ValueExecutiveDashboardPage />} />';

  text = insertRoute(text, routeBlock);

  write(routeRel, text);
  return routeRel;
}

function patchValidator() {
  const validatorRel = "tools/phase3/validate-p3-t14-value-executive.cjs";
  if (!exists(validatorRel)) {
    throw new Error("Missing validator: " + validatorRel);
  }

  backup(validatorRel);

  let text = read(validatorRel);

  if (!text.includes("AppRoutes.generated.tsx")) {
    text = text.replace(
      'const routeCandidates = [',
      'const routeCandidates = [\n  "Frontend/PlantProcess.Web/src/AppRoutes.generated.tsx",'
    );
  }

  write(validatorRel, text);
}

function patchOptionalNavigation() {
  const layoutRel = "Frontend/PlantProcess.Web/src/components/AppLayout.tsx";
  if (!exists(layoutRel)) return;

  let text = read(layoutRel);
  if (text.includes("/value/executive")) return;

  backup(layoutRel);

  const navItem = '  { to: "/value/executive", label: "Value Exec", desc: "Bounded EUR ROI", icon: BarChart3 },';

  if (text.includes('to: "/value/scenario"')) {
    text = text.replace(
      /(\s*\{[^\n]*to:\s*"\/value\/scenario"[\s\S]*?\},)/,
      "$1\n" + navItem
    );
    write(layoutRel, text);
    return;
  }

  if (text.includes("const NAV_INTELLIGENCE = [")) {
    text = text.replace("const NAV_INTELLIGENCE = [", "const NAV_INTELLIGENCE = [\n" + navItem);
    write(layoutRel, text);
    return;
  }

  console.warn("[P3-T14-ROUTE] navigation was not patched; route still works by URL.");
}

const routeFile = patchRouteFile();
patchValidator();
patchOptionalNavigation();

console.log("[GREEN] P3-T14 route repaired in " + routeFile);