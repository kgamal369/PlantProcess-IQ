/*
 * ============================================================================
 * PlantProcess IQ - T-205 FRONTEND SUITE GATE VALIDATOR
 * ============================================================================
 * Backlog task : T-205   Release: R1   Owner: Worker 2
 * File         : tools/ci/validate-frontend-suite-gate.cjs
 *
 * This is the CI consumer of the machine-readable manifest that
 * tools/run/Run-T205-FrontendSuiteGate.ps1 emits. T-150 consumes this. T-150
 * does not reinvent frontend-suite execution.
 *
 * It reads structured evidence only. It never parses console prose, never
 * re-runs the suite, and never decides anything the manifest did not measure.
 *
 * THREE TRUTHS, KEPT SEPARATE
 *
 *   T205CertificationVerdict  is the gate itself sound?
 *   suiteVerdict              did the frontend suite pass?
 *   pipelineVerdict           may Release Truth proceed?
 *
 * EXIT CODES
 *   0  pipeline may proceed: everything GREEN
 *   3  the gate is sound but the suite it measured is RED, so the pipeline is
 *      blocked. This is the honest state while external product failures exist.
 *      It is NOT success, and nothing here downgrades it to a warning.
 *   1  the gate itself is not trustworthy, or the manifest is unusable
 *
 * There is deliberately no allowlist mechanism for failing tests. A known
 * failure is still a failure. The only allowlist the manifest carries is
 * mandatorySkipAllowlist, which must be narrow and explicit; an empty one is
 * the correct state today because the suite has zero skips.
 * ============================================================================
 */

"use strict";

const fs = require("node:fs");
const path = require("node:path");

const REQUIRED_VERDICTS = [
  "terminationVerdict",
  "machineReadableVerdict",
  "determinismVerdict",
  "orphanVerdict",
  "falsificationVerdict",
  "suiteVerdict",
  "pipelineVerdict",
  "T205CertificationVerdict"
];

function fail(message) {
  console.error("T-205 VALIDATOR: " + message);
}

function ok(message) {
  console.log("T-205 VALIDATOR: " + message);
}

function newestManifest(evidenceRoot) {
  const base = path.join(evidenceRoot, "T205Gate");
  if (!fs.existsSync(base)) return null;
  const found = [];
  for (const dir of fs.readdirSync(base)) {
    const candidate = path.join(base, dir, "t205-gate-manifest.json");
    if (fs.existsSync(candidate)) {
      found.push({ p: candidate, m: fs.statSync(candidate).mtimeMs });
    }
  }
  if (found.length === 0) return null;
  found.sort((a, b) => b.m - a.m);
  return found[0].p;
}

function main() {
  let manifestPath = process.argv[2];
  if (!manifestPath) {
    manifestPath = newestManifest(
      process.env.PPIQ_EVIDENCE_ROOT || "C:\\Workspace\\_ppiq_evidence"
    );
  }
  if (!manifestPath || !fs.existsSync(manifestPath)) {
    fail("no gate manifest found. The suite gate has not produced evidence.");
    process.exit(1);
  }
  ok("manifest " + manifestPath);

  let manifest;
  try {
    manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
  } catch (error) {
    fail("the manifest exists but does not parse: " + error.message);
    process.exit(1);
  }

  const structural = [];

  if (manifest.task !== "T-205") {
    structural.push("manifest is not a T-205 manifest: task=" + manifest.task);
  }
  for (const key of REQUIRED_VERDICTS) {
    if (typeof manifest[key] !== "string") {
      structural.push("missing verdict field: " + key);
    }
  }
  const runs = Array.isArray(manifest.runs) ? manifest.runs : [];
  if (runs.length === 0) {
    structural.push("the manifest records no run at all");
  }

  // Discovery-only execution can never be accepted as a suite run.
  for (const run of runs) {
    if (typeof run.command === "string" && run.command.includes("--list")) {
      structural.push(run.label + ": discovery-only --list is not execution");
    }
    if (run.reportParsed !== true) {
      structural.push(run.label + ": no parseable machine-readable report");
    }
    if (run.total === 0) {
      structural.push(run.label + ": zero tests collected is not a pass");
    }
    if (run.nonTerminating === true) {
      structural.push(
        run.label + ": did not terminate - " + run.terminationReason
      );
    }
    if (Array.isArray(run.survivingPids) && run.survivingPids.length > 0) {
      structural.push(
        run.label + ": orphan processes survived: " + run.survivingPids.join(", ")
      );
    }
  }

  // Determinism: identical counts AND identical failing identities.
  const production = runs.filter((r) => r.label === "runA" || r.label === "runB");
  if (production.length === 2) {
    const [a, b] = production;
    for (const field of ["total", "passed", "failed", "skipped"]) {
      if (a[field] !== b[field]) {
        structural.push(
          "determinism: " + field + " A=" + a[field] + " B=" + b[field]
        );
      }
    }
    const idA = (a.failingIdentities || []).join("\n");
    const idB = (b.failingIdentities || []).join("\n");
    if (idA !== idB) {
      structural.push("determinism: failing test identities differ between A and B");
    }
  }

  // Falsification must have been RED, and must have cleaned up after itself.
  const falsifications = Array.isArray(manifest.falsifications)
    ? manifest.falsifications
    : [];
  if (manifest.mode === "Certify" || manifest.mode === "Falsify") {
    if (falsifications.length === 0) {
      structural.push("no falsification was performed");
    }
    for (const f of falsifications) {
      if (f.verdict !== "RED") {
        structural.push(
          f.label + ": falsification did not turn the gate RED, so the watchdog is unproven"
        );
      }
      if (Array.isArray(f.survivingPids) && f.survivingPids.length > 0) {
        structural.push(
          f.label + ": falsification left survivors: " + f.survivingPids.join(", ")
        );
      }
      if (typeof f.assetSha256 !== "string" || f.assetSha256.length === 0) {
        structural.push(f.label + ": falsification asset identity is not recorded");
      }
    }
  }

  // Mandatory skips. Narrow allowlist only; empty is the correct state today.
  const allowlist = Array.isArray(manifest.mandatorySkipAllowlist)
    ? manifest.mandatorySkipAllowlist
    : [];
  if (allowlist.some((entry) => typeof entry !== "string" || entry.includes("*"))) {
    structural.push("the mandatory-skip allowlist contains a wildcard entry");
  }
  const skipped = Number(manifest.mandatorySkipped);
  if (Number.isFinite(skipped) && skipped > allowlist.length) {
    structural.push(
      "skipped tests (" + skipped + ") exceed the explicit allowlist (" + allowlist.length + ")"
    );
  }

  if (structural.length > 0) {
    fail("the gate itself is not trustworthy:");
    for (const s of structural) console.error("  - " + s);
    process.exit(1);
  }

  if (manifest.T205CertificationVerdict !== "GREEN") {
    fail("T205CertificationVerdict is " + manifest.T205CertificationVerdict);
    for (const p of manifest.problems || []) console.error("  - " + p);
    process.exit(1);
  }
  ok("T205CertificationVerdict GREEN - deterministic execution, machine-readable evidence, timeout, leak and orphan detection, and falsification are all proven.");

  const external = Array.isArray(manifest.externalProductFailures)
    ? manifest.externalProductFailures
    : [];

  if (manifest.pipelineVerdict === "GREEN" && manifest.suiteVerdict === "GREEN") {
    ok("suiteVerdict GREEN, pipelineVerdict GREEN - Release Truth may proceed.");
    process.exit(0);
  }

  fail("pipelineVerdict " + manifest.pipelineVerdict + ", suiteVerdict " + manifest.suiteVerdict);
  if (external.length > 0) {
    console.error(
      "  " + external.length + " failing test(s) the gate correctly detected. They are product defects owned outside T-205:"
    );
    for (const e of external) console.error("    - " + e);
  }
  console.error(
    "  This is not a gate defect and it is not a warning. The pipeline stays blocked until these are fixed."
  );
  process.exit(3);
}

main();
