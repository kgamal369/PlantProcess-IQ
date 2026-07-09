// ============================================================
// M1-08 STRICT GATE - Naming Golden Rule, no allowlist.
// "Zero phase tokens in any canonical route, period."
//
// Redirects ARE permitted to name the old /phaseN/* paths: that is precisely
// their job (keeping old links alive). A canonical route is any <Route> whose
// element is not a <Navigate>.
//
// Parsing note: this gate splits App.tsx on "<Route" so each chunk is exactly
// one route. An earlier version used a fixed 240-char lookahead and silently
// missed whitespace-heavy routes; do not reintroduce that.
// ============================================================
import { describe, expect, it } from "vitest";
import { readFileSync, existsSync } from "node:fs";
import { resolve } from "node:path";

const webRoot = resolve(__dirname, "../../..");
const read = (rel: string): string => {
  const p = resolve(webRoot, rel);
  return existsSync(p) ? readFileSync(p, "utf8") : "";
};

const PHASE_IN_PATH = /\/phase\d+\//i;

describe("M1-08 Naming Golden Rule: zero phase tokens on the demo path", () => {
  it("the assistant api file is not phase-named", () => {
    expect(existsSync(resolve(webRoot, "src/api/assistantApi.ts"))).toBe(true);
    expect(existsSync(resolve(webRoot, "src/api/phase8Assistant.ts"))).toBe(false);
  });

  it("no canonical route path contains a phase token", () => {
    const app = read("src/App.tsx");
    expect(app.length).toBeGreaterThan(0);

    const offenders: string[] = [];
    // Each chunk after splitting on "<Route" is exactly one route element, so a
    // <Navigate> inside the chunk means this route IS a redirect.
    for (const chunk of app.split("<Route").slice(1)) {
      const pathMatch = /path="([^"]+)"/.exec(chunk);
      if (!pathMatch) continue;
      const routePath = pathMatch[1];
      const isRedirect = chunk.includes("<Navigate");
      if (!isRedirect && PHASE_IN_PATH.test(routePath)) {
        offenders.push(`canonical route "${routePath}" still carries a phase token`);
      }
    }
    expect(offenders, `M1-08:\n  ${offenders.join("\n  ")}`).toHaveLength(0);
  });

  it("no loading label announces a phase number", () => {
    const app = read("src/App.tsx");
    const bad = app.match(/"Phase\s+\d+[^"]*"/g) ?? [];
    expect(bad, `M1-08 phase-named loading labels: ${bad.join(", ")}`).toHaveLength(0);
  });

  it("no nav target or nav label carries a phase token", () => {
    const layout = read("src/components/AppLayout.tsx");
    expect(layout.length).toBeGreaterThan(0);

    const badTargets: string[] = [];
    const toRe = /to:\s*"([^"]+)"/g;
    let m: RegExpExecArray | null;
    while ((m = toRe.exec(layout)) !== null) {
      if (PHASE_IN_PATH.test(m[1])) badTargets.push(`nav target "${m[1]}"`);
    }
    expect(badTargets, `M1-08: ${badTargets.join(", ")}`).toHaveLength(0);

    const badLabels: string[] = [];
    const labelRe = /label:\s*"([^"]+)"/g;
    while ((m = labelRe.exec(layout)) !== null) {
      if (/\bP\d{2}\b|phase\s*\d+/i.test(m[1])) badLabels.push(`nav label "${m[1]}"`);
    }
    expect(badLabels, `M1-08: ${badLabels.join(", ")}`).toHaveLength(0);
  });

  it("the legacy phase paths still redirect (no broken bookmarks)", () => {
    const app = read("src/App.tsx");
    const mustRedirect = [
      ["/phase8/assistant", "/assistant"],
      ["/phase8/assistant-config", "/assistant/configuration"],
      ["/phase8/suggestions", "/suggestions"],
      ["/phase9/executive", "/executive"],
      ["/phase9/access", "/access-matrix"],
      ["/phase15/honesty-certification", "/advisory/honesty-certification"],
      ["/phase15/benchmarking", "/advisory/benchmarking"],
      ["/phase15/roi-cfo-dashboard", "/advisory/roi-cfo-dashboard"],
      ["/phase15/value-realization", "/advisory/value-realization"],
      ["/phase15/recommendations", "/advisory/recommendations"],
      ["/phase15/scenario-simulation", "/advisory/scenario-simulation"],
    ];
    const missing: string[] = [];
    for (const [from, to] of mustRedirect) {
      const re = new RegExp(`path="${from}"[\\s\\S]{0,160}?to="${to}"`);
      if (!re.test(app)) missing.push(`${from} -> ${to}`);
    }
    expect(missing, `M1-08 broken redirects: ${missing.join(", ")}`).toHaveLength(0);
  });

  it("the canonical descriptive routes exist", () => {
    const app = read("src/App.tsx");
    for (const p of ["/assistant", "/assistant/configuration", "/suggestions", "/access-matrix", "/advisory/benchmarking"]) {
      expect(app, `missing canonical route ${p}`).toContain(`path="${p}"`);
    }
  });
});

// ============================================================
// STEP 2 - PAGE CONTENT assertions.
// Scoped to the 8 customer-facing pages. /license and /access-matrix are
// reachable pages that still carry demo/phase wording and have NOT been ruled
// on; widening this scope before that decision would fail on unauthorised work.
// ============================================================
const PAGE_FILES = [
  "src/pages/Advisory/HonestyCertificationPage.tsx",
  "src/pages/Advisory/BenchmarkingPage.tsx",
  "src/pages/Advisory/RoiCfoDashboardPage.tsx",
  "src/pages/Advisory/ValueRealizationPage.tsx",
  "src/pages/Advisory/RecommendationsPage.tsx",
  "src/pages/Advisory/ScenarioSimulationPage.tsx",
  "src/pages/EdgeCollector/EdgeCollectorPage.tsx",
  "src/pages/HistorianConnector/HistorianConnectorPage.tsx",
];

describe("Step 2: the 8 customer-facing pages carry no phase or pack tokens", () => {
  it("no <h1> announces a phase number", () => {
    const bad: string[] = [];
    for (const rel of PAGE_FILES) {
      const src = read(rel);
      expect(src.length, `${rel} is missing`).toBeGreaterThan(0);
      const m = src.match(/<h1>[^<]*Phase\s*\d+[^<]*<\/h1>/gi);
      if (m) bad.push(`${rel}: ${m.join(", ")}`);
    }
    expect(bad, `phase-named page titles:\n  ${bad.join("\n  ")}`).toHaveLength(0);
  });

  it("no data-testid carries a phase token", () => {
    const bad: string[] = [];
    for (const rel of PAGE_FILES) {
      const m = read(rel).match(/data-testid="[^"]*phase\d[^"]*"/gi);
      if (m) bad.push(`${rel}: ${m.join(", ")}`);
    }
    expect(bad, `phase-named test-ids:\n  ${bad.join("\n  ")}`).toHaveLength(0);
  });

  it("no Pack/T-id kicker survives", () => {
    const bad: string[] = [];
    for (const rel of PAGE_FILES) {
      const m = read(rel).match(/Pack\s+[A-G][^\n]*T-\d+/g);
      if (m) bad.push(`${rel}: ${m.join(", ")}`);
    }
    expect(bad, `Pack/T-id kickers still shipping:\n  ${bad.join("\n  ")}`).toHaveLength(0);
  });

  it("the advisory pages no longer live under a PhaseNN directory", () => {
    expect(existsSync(resolve(webRoot, "src/pages/Advisory"))).toBe(true);
    expect(existsSync(resolve(webRoot, "src/pages/Phase15"))).toBe(false);
    expect(existsSync(resolve(webRoot, "src/api/advisoryApi.ts"))).toBe(true);
    expect(existsSync(resolve(webRoot, "src/api/phase15Advisory.ts"))).toBe(false);
  });
});

// ============================================================
// STEP 3b - REPO-WIDE page-content gate.
// /license and /access-matrix are now settled, so this is no longer scoped to
// the 8 advisory pages. Note the token pattern also catches "Pack G-3", which
// the earlier `Pack [A-G] ... T-\d+` pattern let through.
// ============================================================
import { readdirSync, statSync } from "node:fs";
import { join } from "node:path";

const PAGES_ROOT = resolve(webRoot, "src/pages");
const TOKEN = /(?:\bPack\s+[A-G]\b|\bP\d{2}\b)[^\n]{0,80}?T-\d+|\bPack\s+[A-G]-\d/;

function allPageFiles(dir: string, out: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (/node_modules|__tests__|\.stories\./.test(full)) continue;
    if (statSync(full).isDirectory()) allPageFiles(full, out);
    else if (full.endsWith(".tsx")) out.push(full);
  }
  return out;
}

describe("Step 3b: repo-wide page content carries no phase, pack or task tokens", () => {
  it("no page displays a Pack/P-nn task kicker", () => {
    const bad: string[] = [];
    for (const file of allPageFiles(PAGES_ROOT)) {
      const m = readFileSync(file, "utf8").match(TOKEN);
      if (m) bad.push(`${file.replace(webRoot, "")}: ${m[0]}`);
    }
    expect(bad, `task kickers still shipping:\n  ${bad.join("\n  ")}`).toHaveLength(0);
  });

  it("no page title announces a phase number", () => {
    const bad: string[] = [];
    for (const file of allPageFiles(PAGES_ROOT)) {
      const m = readFileSync(file, "utf8").match(/<h1>[^<]*Phase\s*\d+[^<]*<\/h1>/i);
      if (m) bad.push(`${file.replace(webRoot, "")}: ${m[0]}`);
    }
    expect(bad, `phase-named titles:\n  ${bad.join("\n  ")}`).toHaveLength(0);
  });

  it("the /license demo route and page are gone; the real license page remains", () => {
    const app = read("src/App.tsx");
    expect(app).not.toContain('path="/license"');
    expect(app).not.toContain("LicenseDemoPage");
    expect(app).toContain('path="/commercial/license"');
    expect(existsSync(resolve(webRoot, "src/pages/Phase10/Phase10LicenseDemoPage.tsx"))).toBe(false);
  });

  it("the recommendation approver is the logged-in user, never a fabricated identity", () => {
    const page = read("src/pages/Advisory/RecommendationsPage.tsx");
    expect(page).not.toContain("demo-approver");
    expect(page).toContain("user?.userName");
  });

  it("sample data is disclosed where a canned request is loaded", () => {
    for (const rel of ["src/pages/Advisory/RecommendationsPage.tsx", "src/pages/Advisory/ValueRealizationPage.tsx"]) {
      expect(read(rel), `${rel} must disclose sample data`).toContain("ppiq-std-sample-badge");
    }
  });
});

// ============================================================
// USER-VISIBLE STRINGS.
// The repo-wide checks above only fire on a phase/pack token FOLLOWED BY a
// T-id, or on an <h1>. That let a literal "P08" render on /i18n-rtl and an
// aria-label read "Phase 7 and Phase 8 readiness cards". A gate that scans
// every file but not every pattern is a gate that manufactures confidence.
//
// Scope note: className="phase9-*" / data-testid="phase8-*" are NOT asserted
// here. They are internal, and each is bound to a stylesheet that has to be
// renamed in the same commit. That is the directory-rename task. No allowlist.
// ============================================================
describe("user-visible strings carry no phase tokens", () => {
  it("no rendered text node contains a standalone phase token (P06-P15)", () => {
    const bad: string[] = [];
    for (const file of allPageFiles(PAGES_ROOT)) {
      const src = readFileSync(file, "utf8");
      // A JSX text node is the span between > and <.
      // Range-limited to P06-P15 on purpose: BenchmarkingPage legitimately renders
      // the percentile headers P25 / P50 / P75, and a bare \bP\d{2}\b would fail a
      // correct page. PPIQ's phase numbering never left 6..15.
      const matches = src.match(/>[^<>{}\n]*\bP(?:0[6-9]|1[0-5])\b[^<>{}\n]*</g);
      if (matches) bad.push(`${file.replace(webRoot, "")}: ${matches.join(" | ")}`);
    }
    expect(bad, `phase tokens rendered to screen:\n  ${bad.join("\n  ")}`).toHaveLength(0);
  });

  it("no aria-label, title or placeholder announces a phase number", () => {
    const bad: string[] = [];
    for (const file of allPageFiles(PAGES_ROOT)) {
      const src = readFileSync(file, "utf8");
      const matches = src.match(/(?:aria-label|title|placeholder)="[^"]*Phase\s*\d+[^"]*"/gi);
      if (matches) bad.push(`${file.replace(webRoot, "")}: ${matches.join(" | ")}`);
    }
    expect(bad, `phase tokens in accessible names:\n  ${bad.join("\n  ")}`).toHaveLength(0);
  });
});

// ============================================================
// STRING LITERALS.
// The checks above read App.tsx route paths and AppLayout nav labels. A phase
// token sitting inside a DATA MAP was invisible to all of them - which is how
// src/security/roleAccess.ts kept keying on "/phase8/assistant-config" after
// M1-08 renamed that route, and kept showing a customer an access matrix for
// routes that no longer exist.
//
// App.tsx is excluded: its <Navigate> declarations legitimately name the old
// paths, and the canonical-route test above already governs it.
// ============================================================
// allPageFiles() collects .tsx only - it was written for pages. roleAccess.ts is
// a .ts file, so reusing it here would have skipped the exact file this check
// exists to catch. Walk both extensions.
function allSourceFiles(dir: string, out: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (/node_modules|_phase9_standardbutton_dedupe_backup/.test(full)) continue;
    if (statSync(full).isDirectory()) allSourceFiles(full, out);
    else if (/\.tsx?$/.test(entry)) out.push(full);
  }
  return out;
}

describe("no phase token survives inside a string literal", () => {
  it("no src file keys on a /phaseN/ path", () => {
    const offenders: string[] = [];
    for (const file of allSourceFiles(resolve(webRoot, "src"))) {
      if (file.endsWith("App.tsx")) continue;
      if (/[\\/]test[\\/]architecture[\\/]/.test(file)) continue; // this suite documents them
      const src = readFileSync(file, "utf8");
      const matches = src.match(/["'`]\/phase\d+\/[^"'`]*["'`]/g);
      if (matches) offenders.push(`${file.replace(webRoot, "")}: ${matches.join(", ")}`);
    }
    expect(offenders, `phase paths in string literals:\n  ${offenders.join("\n  ")}`).toHaveLength(0);
  });
});