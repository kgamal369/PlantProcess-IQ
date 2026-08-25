// ============================================================================
// Definition Replay Falsification
//
// Backlog origin: T-202   Release: M2   Owner: Worker 2 (Release Truth)
//
// PURPOSE: prove the replay gate can go RED, WITHOUT weakening any product
// validation and WITHOUT corrupting a real persisted definition.
//
// The earlier strategy was wrong. Attempting to persist an unsupported
// measure/dimension is correctly rejected by write-time validation with HTTP
// 400. That is a GOOD product result. We record it as evidence and we do not
// touch that validation.
//
// PART A - product evidence (read/write against an isolated disposable
//   dashboard): confirm that persistence validation refuses an invalid widget
//   contract, then delete the disposable dashboard. No real definition is
//   touched. Skipped entirely in HistoricalBaseline mode.
//
// PART B - gate failure-path proof (isolated stub, no database at all): stand
//   up a local stub that serves the API contract and returns one 5xx widget and
//   one 2xx-wrong-shape widget. The gate must exit non-zero and must report
//   FAILED and UNCLASSIFIED. Then serve a healthy inventory and the gate must
//   exit zero. This proves the detection logic at the correct test layer.
//
// Reports from Part B are written to their own file and never mixed with the
// CurrentRelease or HistoricalBaseline reports.
// ============================================================================

import http from "node:http";
import fs from "node:fs";
import path from "node:path";
import { spawn } from "node:child_process";

const GATE = path.resolve(process.cwd(), "tests/release-truth/persisted-definition-replay.mjs");
const STUB_REPORT = path.resolve(process.cwd(),
  "reports/release-truth/persisted_definition_replay.falsification.json");
const DISPOSABLE_PREFIX = "REPLAY_VALIDATION_PROBE_";

function pick(o, ...names) {
  if (!o || typeof o !== "object") return undefined;
  for (const n of names) {
    if (Object.prototype.hasOwnProperty.call(o, n)) return o[n];
    const lo = n.charAt(0).toLowerCase() + n.slice(1);
    if (Object.prototype.hasOwnProperty.call(o, lo)) return o[lo];
  }
  return undefined;
}

async function api(base, method, route, token, body) {
  const headers = { Accept: "application/json" };
  if (token) { headers["Authorization"] = "Bearer " + token; headers["X-PPIQ-MFA-Verified"] = "true"; }
  if (body !== undefined) headers["Content-Type"] = "application/json";
  const res = await fetch(base + route, {
    method, headers, body: body === undefined ? undefined : JSON.stringify(body)
  });
  const text = await res.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch { json = null; }
  return { status: res.status, ok: res.ok, json, text };
}

function runGateAgainst(stubBase) {
  // Part B must be HERMETIC. It supplies its own stub identity so the gate can only
  // go red for the reason under test. Depending on ambient profile variables would
  // let a missing-config failure masquerade as a detected replay defect - which is
  // exactly the red-for-the-wrong-reason trap this whole gate exists to prevent.
  const env = Object.assign({}, process.env, {
    PPIQ_REPLAY_MODE: "GateFalsification",
    PPIQ_REPLAY_API_BASE: stubBase,
    PPIQ_SMOKE_USERNAME: "stub-identity",
    PPIQ_SMOKE_PASSWORD: "stub-secret-not-a-real-credential",
    POSTGRES_DB: "stub-no-database"
  });
  delete env.ConnectionStrings__PlantProcessDb;
  delete env.VITE_SMOKE_USERNAME;
  delete env.VITE_SMOKE_PASSWORD;

  // MUST be async. spawnSync blocks the Node event loop, which would stop the
  // in-process stub server from ever answering the child gate - a deadlock, not
  // a test result.
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [GATE], { stdio: "inherit", env });
    const timer = setTimeout(() => {
      child.kill("SIGKILL");
      reject(new Error("[B] Gate did not terminate within 120s against the stub."));
    }, 120000);
    child.on("exit", (code) => { clearTimeout(timer); resolve(code); });
    child.on("error", (err) => { clearTimeout(timer); reject(err); });
  });
}

// ------------------------------------------------------------ stub server ---
function startStub(shape) {
  const widgets = [
    { id: "s1", widgetCode: "HEALTHY", widgetTitle: "Healthy", widgetType: "chart",
      chartType: "bar", dimensionCode: "D", measureCode: "M", filterJson: "{}",
      isActive: true, expressionEnabled: false }
  ];
  if (shape === "broken") {
    widgets.push(
      { id: "s2", widgetCode: "SERVER_ERROR", widgetTitle: "Runtime failure", widgetType: "chart",
        chartType: "bar", dimensionCode: "D", measureCode: "M_500", filterJson: "{}",
        isActive: true, expressionEnabled: false },
      { id: "s3", widgetCode: "WRONG_SHAPE", widgetTitle: "Unrecognised payload", widgetType: "chart",
        chartType: "pie", dimensionCode: "D", measureCode: "M_SHAPE", filterJson: "{}",
        isActive: true, expressionEnabled: false });
  }

  const defs = [{ id: "sd1", dashboardCode: "STUB_DASHBOARD", isActive: true, widgets }];

  const srv = http.createServer(async (req, res) => {
    let body = "";
    for await (const c of req) body += c;
    const send = (o, s) => { res.writeHead(s || 200, { "content-type": "application/json" }); res.end(JSON.stringify(o)); };

    if (req.url === "/auth/login") return send({ accessToken: "stub-token" });
    if (req.url === "/analytics/dashboard/definitions") return send(defs);
    if (req.url === "/pages") return send([]);
    if (req.url === "/analytics/dashboard/widgets/query") {
      const q = JSON.parse(body || "{}");
      if (q.measureCode === "M_500") return send({ title: "server error" }, 500);
      if (q.measureCode === "M_SHAPE") return send({ status: "ok", payload: [1, 2, 3] }, 200);
      return send({ generatedAtUtc: new Date().toISOString(), widget: {}, columns: [{ name: "x" }],
                    rows: [{ x: 1 }, { x: 2 }], warnings: [] });
    }
    return send({}, 404);
  });

  return new Promise((resolve) => {
    srv.listen(0, "127.0.0.1", () => {
      resolve({ srv, base: "http://127.0.0.1:" + srv.address().port });
    });
  });
}

// ------------------------------------------------------------------ Part A ---
async function partA() {
  const mode = process.env.PPIQ_REPLAY_MODE || "CurrentRelease";
  if (mode === "HistoricalBaseline") {
    console.log("\n[A] SKIPPED - HistoricalBaseline is read-only. No write probe against the frozen baseline.");
    return { skipped: true };
  }

  const base = (process.env.VITE_API_BASE_URL || process.env.ASPNETCORE_URLS || "")
    .split(";")[0].replace(/\/+$/, "");
  if (!base) throw new Error("No API base in the loaded profile.");

  const login = await api(base, "POST", "/auth/login", null, {
    UserName: process.env.PPIQ_SMOKE_USERNAME || process.env.VITE_SMOKE_USERNAME,
    Password: process.env.PPIQ_SMOKE_PASSWORD || process.env.VITE_SMOKE_PASSWORD
  });
  if (!login.ok) throw new Error("login failed: http " + login.status);
  const token = pick(login.json, "accessToken", "token");

  const code = DISPOSABLE_PREFIX + Date.now();
  let defId = null;
  console.log("\n[A] Probing write-time validation via disposable dashboard " + code);

  try {
    const created = await api(base, "POST", "/analytics/dashboard/definitions", token, {
      dashboardCode: code, name: "Replay validation probe (disposable)",
      description: "Created by the replay falsification driver. Safe to delete.",
      layoutJson: "{}", isDefault: false, isSystemTemplate: false, isSynthetic: true
    });
    if (!created.ok) throw new Error("could not create disposable dashboard: http " + created.status);
    defId = pick(created.json, "Id");

    const probe = await api(base, "POST", "/analytics/dashboard/definitions/" + defId + "/widgets", token, {
      widgetCode: "VALIDATION_PROBE", widgetTitle: "Validation probe", widgetType: "chart",
      chartType: "bar", dimensionCode: "__UNSUPPORTED_DIMENSION_PROBE__",
      measureCode: "__UNSUPPORTED_MEASURE_PROBE__", filterJson: "{}", layoutJson: "{}",
      displayOptionsJson: "{}", sortOrder: 0, isSynthetic: true
    });

    if (probe.status === 400) {
      console.log("[A] PASS - persistence validation refused the invalid widget contract (http 400).");
      console.log("[A] Recorded: an invalid persisted definition cannot be created through the product.");
      console.log("[A] This validation is CORRECT and must not be weakened to satisfy a falsification ritual.");
      return { validationEnforced: true, status: 400 };
    }
    if (probe.ok) {
      console.log("[A] FINDING - the API ACCEPTED an unsupported measure/dimension contract.");
      console.log("[A] Write-time validation is weaker than expected. Report to the tech lead.");
      return { validationEnforced: false, status: probe.status };
    }
    console.log("[A] INCONCLUSIVE - unexpected http " + probe.status);
    return { validationEnforced: null, status: probe.status };
  } finally {
    if (defId) {
      const del = await api(base, "DELETE", "/analytics/dashboard/definitions/" + defId, token);
      console.log(del.ok ? "[A] Disposable dashboard removed."
                         : "!!! [A] CLEANUP FAILED http " + del.status + " - remove " + code + " manually");
      if (!del.ok) process.exitCode = 2;
    }
  }
}

// ------------------------------------------------------------------ Part B ---
async function partB() {
  console.log("\n[B] Proving the gate's failure path against an isolated stub (no database).");

  const broken = await startStub("broken");
  let red;
  try { red = await runGateAgainst(broken.base); } finally { broken.srv.close(); }

  if (red === 0) {
    throw new Error("[B] FALSIFICATION FAILED: the gate returned PASS while the stub served a " +
                    "5xx widget and an unrecognised payload. The gate cannot detect a broken replay.");
  }
  console.log("[B] Gate correctly went RED (exit " + red + ").");

  const manifest = JSON.parse(fs.readFileSync(STUB_REPORT, "utf8"));
  const counts = manifest.counts || {};
  if (manifest.fatal) {
    throw new Error("[B] Gate went red for the WRONG REASON (fatal: " + manifest.fatal +
                    "). A red caused by configuration is not proof that the gate detects a " +
                    "broken replay.");
  }
  if (!counts.FAILED)       throw new Error("[B] Gate went red but never reported FAILED for the 5xx widget.");
  if (!counts.UNCLASSIFIED) throw new Error("[B] Gate went red but never reported UNCLASSIFIED for the wrong-shape payload.");
  if (!counts.POPULATED)    throw new Error("[B] Gate never reported POPULATED for the healthy widget - it is failing everything indiscriminately.");
  console.log("[B] Manifest correctly named both defect classes: " + JSON.stringify(counts));

  const healthy = await startStub("healthy");
  let green;
  try { green = await runGateAgainst(healthy.base); } finally { healthy.srv.close(); }

  if (green !== 0) throw new Error("[B] Gate stayed RED on a healthy stub (exit " + green + "). False positive.");
  console.log("[B] Gate correctly returned GREEN on a healthy stub.");
}

async function main() {
  const a = await partA();
  await partB();
  console.log("\nDEFINITION REPLAY FALSIFICATION: PASS");
  if (a && a.validationEnforced === true) {
    console.log("  Product note: persistence validation prevents creation of invalid persisted");
    console.log("  definitions. The gate's failure path is therefore proven at the harness layer,");
    console.log("  which is the correct test layer, rather than by damaging product validation.");
  }
}

main().catch((e) => {
  console.error("\nDEFINITION REPLAY FALSIFICATION: FAIL - " + String(e && e.message ? e.message : e));
  process.exit(2);
});