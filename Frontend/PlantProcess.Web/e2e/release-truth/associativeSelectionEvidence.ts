// ============================================================================
// Associative selection evidence.
//
// Backlog origin: T-204   Release: M2   Owner: Worker 2 (Release Truth)
//
// Separate run root and separate schema from the route-invariant evidence. The
// durable PATTERN is reused - one atomic artifact per observation, aggregated
// from disk - because a worker restart must not lose a phase. The T-203 route
// artifact schema is NOT overloaded.
// ============================================================================

import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";

export type Phase = "BASELINE_A" | "BASELINE_B" | "SELECTED" | "CLEARED";

export type WidgetPhase = {
  scenario: string;
  phase: Phase;
  widgetCode: string;
  chartType: string;
  /** What the page intended: the selection state the UI is showing. */
  intendedFilters: Record<string, unknown>;
  /** What actually went on the wire for this widget. The falsification hinges
   *  on these two being recorded separately. */
  executedRequestFilters: Record<string, unknown>;
  population: number;
  semanticResultSignature: string;
  settled: boolean;
  writtenAtUtc: string;
  runId: string;
};

const ROOT = () => path.resolve(process.cwd(), "reports/release-truth/associative");
const RUN_MARKER = "run-id.txt";

// Fields that change on every execution and say nothing about the answer.
const VOLATILE = new Set([
  "generatedAtUtc", "generatedAt", "executedAtUtc", "timestamp",
  "runId", "requestId", "executionId", "correlationId",
  "executionEvidenceHandle", "__executionSnapshot", "__sourceRowIndex",
]);

function canonical(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(canonical);
  if (value && typeof value === "object") {
    const out: Record<string, unknown> = {};
    for (const key of Object.keys(value as Record<string, unknown>).sort()) {
      if (VOLATILE.has(key)) continue;
      out[key] = canonical((value as Record<string, unknown>)[key]);
    }
    return out;
  }
  return value;
}

/**
 * Row ordering follows the result's own contract, not JSON accident.
 *   categorical / unordered -> sorted by stable semantic row identity
 *   time-series (line/area)  -> declared dimension order preserved
 *   table                    -> declared server/widget sort contract preserved
 * Hashing raw response order would go red when the same analytical answer came
 * back in a different incidental order.
 */
export function semanticResultSignature(chartType: string, rows: unknown[]): string {
  const canonicalRows = (rows ?? []).map(canonical) as Record<string, unknown>[];
  const ordered = ["line", "area", "table"].includes((chartType || "").toLowerCase())
    ? canonicalRows
    : [...canonicalRows].sort((a, b) => JSON.stringify(a).localeCompare(JSON.stringify(b)));
  return crypto.createHash("sha256").update(JSON.stringify(ordered), "utf8").digest("hex").slice(0, 24);
}

function artifactName(scenario: string, phase: Phase, widgetCode: string): string {
  const id = crypto.createHash("sha256")
    .update(`${scenario}|${phase}|${widgetCode}`, "utf8").digest("hex").slice(0, 16);
  return `phase-${id}.json`;
}

export function createRunDirectory(): string {
  const root = ROOT();
  fs.rmSync(root, { recursive: true, force: true });
  const runId = new Date().toISOString().replace(/[:.]/g, "-");
  const dir = path.join(root, runId);
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(path.join(root, RUN_MARKER), runId, "utf8");
  return dir;
}

export function currentRunDirectory(): string {
  const marker = path.join(ROOT(), RUN_MARKER);
  if (!fs.existsSync(marker)) {
    throw new Error("No associative evidence run directory. globalSetup did not run.");
  }
  return path.join(ROOT(), fs.readFileSync(marker, "utf8").trim());
}

export function runId(): string { return path.basename(currentRunDirectory()); }

export function writePhase(p: WidgetPhase): void {
  const dir = currentRunDirectory();
  const finalPath = path.join(dir, artifactName(p.scenario, p.phase, p.widgetCode));
  if (fs.existsSync(finalPath)) {
    throw new Error(`Duplicate phase artifact: ${p.scenario}/${p.phase}/${p.widgetCode}`);
  }
  const tmp = `${finalPath}.${process.pid}.tmp`;
  const fd = fs.openSync(tmp, "w");
  try { fs.writeFileSync(fd, JSON.stringify(p, null, 2), "utf8"); fs.fsyncSync(fd); }
  finally { fs.closeSync(fd); }
  fs.renameSync(tmp, finalPath);
}

export function readAllPhases(): WidgetPhase[] {
  const dir = currentRunDirectory();
  const expectedRun = path.basename(dir);
  const out: WidgetPhase[] = [];
  for (const f of fs.readdirSync(dir)) {
    if (f.endsWith(".tmp")) throw new Error(`Half-written phase artifact left behind: ${f}`);
    if (!f.endsWith(".json")) continue;
    const parsed = JSON.parse(fs.readFileSync(path.join(dir, f), "utf8")) as WidgetPhase;
    if (parsed.runId !== expectedRun) throw new Error(`Stale phase artifact: ${f} from run ${parsed.runId}`);
    out.push(parsed);
  }
  return out;
}

export function phasesFor(all: WidgetPhase[], scenario: string, phase: Phase): WidgetPhase[] {
  return all.filter((p) => p.scenario === scenario && p.phase === phase);
}