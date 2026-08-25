// ============================================================================
// Customer route network, console and widget-state invariant gate.
//
// Backlog origin: T-203   Release: M2   Owner: Worker 2 (Release Truth)
//
// This is not a browser walkthrough. Every customer-reachable route under the
// Release-1 shell is navigated, settled, and observed. A route fails on an
// unexpected HTTP >= 400, a failed required request, an uncaught exception, a
// console error, a visible error surface, a Failed widget, or never settling.
//
// Two falsification tests prove the gate can go RED, both at the test layer.
// No production behaviour is modified to produce them.
// ============================================================================

import fs from "node:fs";
import path from "node:path";
import { expect, test } from "@playwright/test";
import { login } from "../helpers/auth";
import {
  customerRoutes,
  readDeclaredRoutes,
  unclassifiedRoutes,
} from "./customerRouteInventory";
import { installRouteInvariantGuard, type RouteObservation } from "./routeInvariantGuard";
import { aggregate, runId, writeRouteArtifact } from "./durableRouteEvidence";

const OUT_DIR = path.resolve(process.cwd(), "reports/release-truth");
const OUT_FILE = path.join(OUT_DIR, "customer_route_invariant.json");

const observations: RouteObservation[] = [];
const declared = readDeclaredRoutes();
const gated = customerRoutes(declared);

// Deliberately NOT serial. Playwright skips the remainder of a serial group
// after the first failure, so the gate reported one route and left 37 unrun.
// A route-inventory gate must report EVERY defective route in one pass;
// determinism comes from workers:1 in the configuration, not from serial mode.

test.beforeAll(() => {
  fs.mkdirSync(OUT_DIR, { recursive: true });
});

/**
 * One atomic artifact per route, written the moment the route reaches a terminal
 * state. A visit failure is still an observation and is still recorded.
 */
function record(
  route: { path: string; routeClass: string; reason: string },
  observation: RouteObservation,
  testStatus: string
): void {
  writeRouteArtifact({
    ...observation,
    classification: route.routeClass,
    reason: route.reason,
    testStatus,
    runId: runId(),
    writtenAtUtc: new Date().toISOString(),
  });
}

test.afterAll(() => {
  // EVIDENCE IS AGGREGATED FROM DISK, NOT FROM MEMORY.
  // Playwright restarts the worker process after a failure, discarding any
  // module-level accumulator. Measured: 36 routes ran, 7 failed, and only 14
  // observations reached afterAll. Each test now writes one atomic artifact and
  // this step reads them back, so a restart cannot lose an observation.
  //
  // Coverage remains part of the verdict: expected and observed must agree, and
  // a missing, unreadable, stale, duplicate or half-written artifact is a
  // failure rather than something to ignore.
  const summary = aggregate(gated.map((r) => r.path));
  fs.writeFileSync(
    OUT_FILE,
    JSON.stringify(
      {
        gate: "Customer Route Invariant",
        backlogOrigin: "T-203",
        release: "M2",
        finishedAtUtc: new Date().toISOString(),
        declaredRoutes: declared.length,
        gatedRoutes: gated.length,
        classification: declared.reduce<Record<string, number>>((acc, r) => {
          acc[r.routeClass] = (acc[r.routeClass] ?? 0) + 1;
          return acc;
        }, {}),
        runId: runId(),
        routesExpected: summary.routesExpected,
        routesObserved: summary.routesObserved,
        routesPassed: summary.routesPassed,
        routesFailed: summary.routesFailed,
        incompleteRun: summary.incompleteRun,
        verdict: summary.verdict,
        fatal:
          summary.fatal ??
          (summary.incompleteRun
            ? `incomplete run: ${summary.routesObserved} of ${summary.routesExpected} routes ` +
              "were observed. A verdict cannot be drawn from a partial inventory."
            : null),
        routes: summary.routes,
      },
      null,
      2
    ),
    "utf8"
  );
});

// --------------------------------------------------------------- coverage ---
test("every declared route carries an explicit classification", () => {
  const unknown = unclassifiedRoutes(declared);
  expect(
    unknown.map((r) => r.path),
    "A route exists in App.tsx with no classification in customerRouteInventory.ts. " +
      "Classify it as customer, internal or requires-instance; do not leave it to default."
  ).toEqual([]);

  // Non-vacuity: an empty inventory must never read as a pass.
  expect(gated.length, "zero customer routes enumerated - inventory is vacuous").toBeGreaterThan(0);
});

// ------------------------------------------------------------- the gate -----
for (const route of gated) {
  test(`customer route is clean: ${route.path}`, async ({ page, context }) => {
    // Every route records an entry, including one that could not be visited.
    // An unrecorded failure is indistinguishable from a route that was never
    // required, and that is how a partial run turns into a false green.
    let guard;
    try {
      await login(context.request);
      guard = installRouteInvariantGuard(page, route.path);
      await page.goto(route.path, { waitUntil: "domcontentloaded" });
    } catch (error) {
      record(route, {
        route: route.path,
        settled: false,
        violations: [
          {
            kind: "visit-failed",
            detail: String(error instanceof Error ? error.message : error).slice(0, 300),
          },
        ],
        allowances: [],
        requestCount: 0,
        widgetStates: [],
      }, "failed");
      throw error;
    }

    const settled = await guard.settle();
    const observation = await guard.collect(settled);
    const clean = observation.violations.length === 0 && observation.settled;
    record(route, observation, clean ? "passed" : "failed");

    expect(
      observation.violations.map((v) => `${v.kind}: ${v.detail}`),
      `route ${route.path} (${route.reason})`
    ).toEqual([]);
    expect(observation.settled, `route ${route.path} never settled`).toBe(true);
  });
}

// -------------------------------------------------- falsification proof 1 ---
test("FALSIFICATION: a faulted API response makes the gate RED and names route and request", async ({
  page,
  context,
}) => {
  await login(context.request);
  const target = gated[0];

  await page.route("**/analytics/**", async (r) => {
    await r.fulfill({ status: 500, contentType: "application/json", body: '{"title":"injected"}' });
  });

  const guard = installRouteInvariantGuard(page, target.path);
  await page.goto(target.path, { waitUntil: "domcontentloaded" });
  const settled = await guard.settle();
  const observation = await guard.collect(settled);

  const named = observation.violations.filter(
    (v) => v.kind === "http-error" || v.kind === "request-failed" || v.kind === "error-surface"
  );
  expect(named.length, "a faulted API response did not make the guard report a violation").toBeGreaterThan(0);
  expect(observation.route).toBe(target.path);
});

// -------------------------------------------------- falsification proof 2 ---
test("FALSIFICATION: an uncaught render error makes the gate RED and names the exception", async ({
  page,
  context,
}) => {
  await login(context.request);
  const target = gated[0];

  const guard = installRouteInvariantGuard(page, target.path);
  await page.goto(target.path, { waitUntil: "domcontentloaded" });

  // Test-layer injection only. Nothing in the product is changed to produce it.
  await page.evaluate(() => {
    setTimeout(() => {
      throw new Error("PPIQ_ROUTE_INVARIANT_FALSIFICATION");
    }, 0);
  });
  await page.waitForTimeout(300);

  const settled = await guard.settle();
  const observation = await guard.collect(settled);

  const uncaught = observation.violations.filter((v) => v.kind === "uncaught-exception");
  expect(uncaught.length, "an uncaught exception did not make the guard report a violation").toBeGreaterThan(0);
  expect(uncaught.some((v) => v.detail.includes("PPIQ_ROUTE_INVARIANT_FALSIFICATION"))).toBe(true);
});