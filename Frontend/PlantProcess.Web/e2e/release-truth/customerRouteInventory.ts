// ============================================================================
// Customer route inventory.
//
// Backlog origin: T-203   Release: M2   Owner: Worker 2 (Release Truth)
//
// The inventory is DERIVED from the product's own route table, never
// hand-maintained. src/App.tsx is the route authority; this module parses it
// and every live route must carry an explicit classification below.
//
// A newly added route has no classification, so the coverage assertion fails.
// Drift is loud by construction. There is no silent default.
// ============================================================================

import fs from "node:fs";
import path from "node:path";

export type RouteClass =
  | "customer"          // reachable by a customer in the Release-1 shell; gated
  | "internal"          // administrative/internal-only; classified, not gated as customer
  | "requires-instance" // parameterised; needs a real id, gated only when one is supplied
  | "redirect";         // <Navigate>; asserted to land, not invariant-gated

export type RouteEntry = {
  path: string;
  routeClass: RouteClass;
  reason: string;
};

// Declared classification. Reviewed by the tech lead, not inferred by the gate.
export const ROUTE_CLASSIFICATION: Record<string, { routeClass: RouteClass; reason: string }> = {
  "/dashboard":                        { routeClass: "customer", reason: "Day-1 landing surface" },
  "/materials":                        { routeClass: "customer", reason: "material investigation entry" },
  "/risk":                             { routeClass: "customer", reason: "risk surface" },
  "/data-quality":                     { routeClass: "customer", reason: "data quality surface" },
  "/correlations":                     { routeClass: "customer", reason: "correlation surface" },
  "/ml-readiness":                     { routeClass: "customer", reason: "readiness surface" },
  "/mapping-health":                   { routeClass: "customer", reason: "mapping health surface" },
  "/suggestions":                      { routeClass: "customer", reason: "assistant suggestions" },
  "/assistant/configuration":          { routeClass: "customer", reason: "assistant configuration" },
  "/executive":                        { routeClass: "customer", reason: "executive summary" },
  "/value/executive":                  { routeClass: "customer", reason: "value surface" },
  "/value/scenario":                   { routeClass: "customer", reason: "value surface" },
  "/advisory/benchmarking":            { routeClass: "customer", reason: "advisory surface" },
  "/advisory/honesty-certification":   { routeClass: "customer", reason: "advisory surface" },
  "/advisory/recommendations":         { routeClass: "customer", reason: "advisory surface" },
  "/advisory/roi-cfo-dashboard":       { routeClass: "customer", reason: "advisory surface" },
  "/advisory/scenario-simulation":     { routeClass: "customer", reason: "advisory surface" },
  "/advisory/value-realization":       { routeClass: "customer", reason: "advisory surface" },
  "/analysis/toolbox":                 { routeClass: "customer", reason: "analysis authoring" },
  "/investigate/advanced":             { routeClass: "customer", reason: "investigation surface" },
  "/investigate/analysis-jobs":        { routeClass: "customer", reason: "job monitoring" },
  "/investigate/inspect":              { routeClass: "customer", reason: "investigation surface" },
  "/prep/canvas":                      { routeClass: "customer", reason: "Canvas authoring" },
  "/data-integration":                 { routeClass: "customer", reason: "DB link / intake shell" },
  "/data-integration/connections":     { routeClass: "customer", reason: "DB link" },
  "/data-integration/registry":        { routeClass: "customer", reason: "table registry" },
  "/data-integration/importing":       { routeClass: "customer", reason: "import" },
  "/data-integration/jobs":            { routeClass: "customer", reason: "job monitor" },
  "/data-integration/connector-truth": { routeClass: "customer", reason: "connector evidence" },
  "/data-integration/alerting":        { routeClass: "customer", reason: "alerting" },
  "/data-integration/supervisor":      { routeClass: "customer", reason: "supervisor report" },
  "/data-integration/author-mapping":  { routeClass: "customer", reason: "mapping authoring" },
  "/data-integration/prepare":         { routeClass: "customer", reason: "preparation" },
  "/commercial/license":               { routeClass: "customer", reason: "licence surface" },
  "/page-builder":                     { routeClass: "customer", reason: "page authoring" },
  "/dashboard/widgets/schema-drift":   { routeClass: "customer", reason: "widget evidence" },
  "/access-matrix":                    { routeClass: "internal", reason: "access review surface" },
  "/admin/*":                          { routeClass: "internal", reason: "administration" },
  "/admin-preview":                    { routeClass: "internal", reason: "administration preview" },
  "/brand":                            { routeClass: "internal", reason: "brand reference surface" },
  "/i18n-rtl":                         { routeClass: "internal", reason: "localisation harness" },
  "/analytics-widgets":                { routeClass: "internal", reason: "widget gallery" },
  "/widget-script-compiler":           { routeClass: "internal", reason: "authoring tool" },
  "/edge-collector":                   { routeClass: "internal", reason: "edge deployment surface" },
  "/historian-connector":              { routeClass: "internal", reason: "connector configuration" },
  "/materials/:materialUnitId":        { routeClass: "requires-instance", reason: "needs a material unit id" },
  "/pages/:slug":                      { routeClass: "requires-instance", reason: "needs a persisted page slug" },
  "/workspace/:dashboardCode":         { routeClass: "requires-instance", reason: "needs a dashboard code" },
};

function repoRoot(): string {
  let dir = process.cwd();
  while (dir && !fs.existsSync(path.join(dir, "src", "App.tsx"))) {
    const parent = path.dirname(dir);
    if (parent === dir) throw new Error("Frontend project root not found from " + process.cwd());
    dir = parent;
  }
  return dir;
}

/** Parse the product's own route table. This is the authority. */
export function readDeclaredRoutes(): RouteEntry[] {
  const appPath = path.join(repoRoot(), "src", "App.tsx");
  const source = fs.readFileSync(appPath, "utf8");

  const parentByChild: Record<string, string> = {};
  const nested = /<Route\s+path="\/data-integration"[\s\S]*?<\/Route>/.exec(source);
  if (nested) {
    for (const m of nested[0].matchAll(/path="([a-z-]+)"/g)) {
      parentByChild[m[1]] = "/data-integration";
    }
  }

  const seen = new Set<string>();
  const routes: RouteEntry[] = [];

  for (const m of source.matchAll(/path="([^"]+)"/g)) {
    const raw = m[1];
    const window = source.slice(m.index ?? 0, (m.index ?? 0) + 400);
    const beforeNextRoute = window.split("<Route")[0];
    const isRedirect = beforeNextRoute.includes("<Navigate");

    const full = raw.startsWith("/")
      ? raw
      : (parentByChild[raw] ? parentByChild[raw] + "/" + raw : "/" + raw);

    if (full === "*") continue;
    if (seen.has(full)) continue;
    seen.add(full);

    if (isRedirect) {
      routes.push({ path: full, routeClass: "redirect", reason: "declared <Navigate>" });
      continue;
    }

    const declared = ROUTE_CLASSIFICATION[full];
    if (!declared) {
      // Deliberately unclassified. The coverage assertion turns this into RED.
      routes.push({ path: full, routeClass: "customer", reason: "UNCLASSIFIED" });
      continue;
    }
    routes.push({ path: full, routeClass: declared.routeClass, reason: declared.reason });
  }

  return routes;
}

export function unclassifiedRoutes(routes: RouteEntry[]): RouteEntry[] {
  return routes.filter((r) => r.reason === "UNCLASSIFIED");
}

export function customerRoutes(routes: RouteEntry[]): RouteEntry[] {
  return routes.filter((r) => r.routeClass === "customer" && r.reason !== "UNCLASSIFIED");
}