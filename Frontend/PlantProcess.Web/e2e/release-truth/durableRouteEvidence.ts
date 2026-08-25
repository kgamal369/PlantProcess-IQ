// ============================================================================
// Durable route evidence.
//
// Backlog origin: T-203   Release: M2   Owner: Worker 2 (Release Truth)
//
// Playwright restarts the worker process after a failure, which discards any
// module-level accumulator. Measured: 36 routes executed, 7 failed, and only 14
// observations survived to afterAll. Evidence must therefore live on disk, one
// atomic artifact per route, aggregated at the end from the filesystem.
//
// Writes are atomic: temp file -> flush/close -> rename. A killed worker cannot
// leave a half-written file that parses as evidence.
// ============================================================================

import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import type { RouteObservation } from "./routeInvariantGuard";

export type RouteArtifact = RouteObservation & {
  classification: string;
  reason: string;
  testStatus: string;
  runId: string;
  writtenAtUtc: string;
};

const RUN_ID_FILE = "run-id.txt";

function evidenceRoot(): string {
  return path.resolve(process.cwd(), "reports/release-truth/routes");
}

/** Stable, filesystem-safe identity for a route path. Never the raw URL. */
export function routeFileName(routePath: string): string {
  const hash = crypto.createHash("sha256").update(routePath, "utf8").digest("hex").slice(0, 16);
  return `route-${hash}.json`;
}

/**
 * One run identity, created by globalSetup and read by every worker. Workers
 * cannot agree on a run id by generating one each, so it is written once.
 */
export function createRunDirectory(): string {
  const root = evidenceRoot();
  fs.rmSync(root, { recursive: true, force: true });
  const runId = new Date().toISOString().replace(/[:.]/g, "-");
  const dir = path.join(root, runId);
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(path.join(root, RUN_ID_FILE), runId, "utf8");
  return dir;
}

export function currentRunDirectory(): string {
  const root = evidenceRoot();
  const marker = path.join(root, RUN_ID_FILE);
  if (!fs.existsSync(marker)) {
    throw new Error(
      "No route-evidence run directory. globalSetup did not run, so evidence would be " +
        "written into an unidentified run. Refusing rather than guessing."
    );
  }
  return path.join(root, fs.readFileSync(marker, "utf8").trim());
}

export function runId(): string {
  return path.basename(currentRunDirectory());
}

/** Atomic write. A partial file never becomes evidence. */
export function writeRouteArtifact(artifact: RouteArtifact): void {
  const dir = currentRunDirectory();
  fs.mkdirSync(dir, { recursive: true });
  const finalPath = path.join(dir, routeFileName(artifact.route));

  if (fs.existsSync(finalPath)) {
    throw new Error(
      `Duplicate route artifact for ${artifact.route}. Two tests claim the same route ` +
        "identity; the inventory or the hash is wrong."
    );
  }

  const tempPath = `${finalPath}.${process.pid}.tmp`;
  const handle = fs.openSync(tempPath, "w");
  try {
    fs.writeFileSync(handle, JSON.stringify(artifact, null, 2), "utf8");
    fs.fsyncSync(handle);
  } finally {
    fs.closeSync(handle);
  }
  fs.renameSync(tempPath, finalPath);
}

export type Aggregate = {
  routesExpected: number;
  routesObserved: number;
  routesPassed: number;
  routesFailed: number;
  incompleteRun: boolean;
  fatal: string | null;
  verdict: "PASS" | "FAIL";
  routes: RouteArtifact[];
};

/**
 * Aggregate from disk. Every expected route must have exactly one readable
 * artifact. A missing file, an unparseable file, a stale artifact from another
 * run, or an unexpected extra artifact all fail the run. None is ignored.
 */
export function aggregate(expectedRoutes: string[]): Aggregate {
  const dir = currentRunDirectory();
  const expectedRun = path.basename(dir);
  const problems: string[] = [];
  const routes: RouteArtifact[] = [];
  const seen = new Set<string>();

  for (const route of expectedRoutes) {
    const file = path.join(dir, routeFileName(route));
    if (!fs.existsSync(file)) {
      problems.push(`no artifact for ${route}`);
      continue;
    }
    let parsed: RouteArtifact;
    try {
      parsed = JSON.parse(fs.readFileSync(file, "utf8")) as RouteArtifact;
    } catch (error) {
      problems.push(`unreadable artifact for ${route}: ${String(error)}`);
      continue;
    }
    if (parsed.runId !== expectedRun) {
      problems.push(`stale artifact for ${route}: run ${parsed.runId} != ${expectedRun}`);
      continue;
    }
    if (parsed.route !== route) {
      problems.push(`artifact identity mismatch: file for ${route} declares ${parsed.route}`);
      continue;
    }
    seen.add(route);
    routes.push(parsed);
  }

  const stray = fs
    .readdirSync(dir)
    .filter((f: string) => f.endsWith(".json"))
    .filter((f: string) => !expectedRoutes.some((r) => routeFileName(r) === f));
  for (const f of stray) problems.push(`unexpected artifact ${f} not matching any expected route`);

  const leftoverTemp = fs.readdirSync(dir).filter((f: string) => f.endsWith(".tmp"));
  for (const f of leftoverTemp) problems.push(`half-written artifact left behind: ${f}`);

  const routesFailed = routes.filter((r) => r.violations.length > 0 || !r.settled).length;
  const routesPassed = routes.length - routesFailed;
  const incompleteRun = seen.size !== expectedRoutes.length;

  const fatal =
    problems.length > 0
      ? `route evidence is not sound: ${problems.slice(0, 10).join("; ")}` +
        (problems.length > 10 ? ` (+${problems.length - 10} more)` : "")
      : null;

  return {
    routesExpected: expectedRoutes.length,
    routesObserved: routes.length,
    routesPassed,
    routesFailed,
    incompleteRun,
    fatal,
    verdict: !incompleteRun && routesFailed === 0 && fatal === null ? "PASS" : "FAIL",
    routes,
  };
}