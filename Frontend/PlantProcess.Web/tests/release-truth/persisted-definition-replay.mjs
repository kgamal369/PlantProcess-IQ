// ============================================================================
// Persisted Definition Replay Gate
//
// Backlog origin: T-202   Release: M2   Owner: Worker 2 (Release Truth)
//
// SCOPE (bounded): enumerate every ACTIVE persisted dashboard/widget/page
// definition from the resolved release database and prove each replays through
// the real API path without a hidden HTTP/API/runtime failure.
//   NOT cross-filter correctness  -> T-204
//   NOT route/console invariants  -> T-203
//   NOT frontend suite truth      -> T-205
//   NOT Day-1 customer flow       -> T-247
//
// MODES (PPIQ_REPLAY_MODE):
//   CurrentRelease     DEFAULT, AUTHORITATIVE. Canonical generic M2 database.
//                      Refuses ppiq_presentation.
//   HistoricalBaseline Informational only. ppiq_presentation, frozen M1
//                      definitions, read-only regression evidence. Refuses any
//                      other database. NEVER closes an M2 task.
//   GateFalsification  Internal. Points at a local stub to prove this gate's
//                      own failure path. Never touches the product database.
//
// Reports are written per mode and never mixed.
//
// TERMINAL STATES:
//   POPULATED     2xx, recognised result shape, rows > 0
//   EMPTY         2xx, recognised result shape, rows == 0
//   BLOCKED       reserved for an explicitly declared entitlement envelope
//   FAILED        any unexpected status, including 401/403
//   UNCLASSIFIED  2xx whose body is not a recognised result shape -> FAIL
// ============================================================================

import fs from "node:fs";
import path from "node:path";

const MODE = process.env.PPIQ_REPLAY_MODE || "CurrentRelease";
const HISTORICAL_DB = "ppiq_presentation";

const REPORT_BY_MODE = {
  CurrentRelease:     "persisted_definition_replay.json",
  HistoricalBaseline: "persisted_definition_replay.historical-baseline.json",
  GateFalsification:  "persisted_definition_replay.falsification.json"
};

const OUT_DIR  = path.resolve(process.cwd(), "reports/release-truth");
const OUT_FILE = path.join(OUT_DIR, REPORT_BY_MODE[MODE] || REPORT_BY_MODE.CurrentRelease);

const results = [];
let fatal = null;

function record(e) { results.push(e); }

function pick(o, ...names) {
  if (!o || typeof o !== "object") return undefined;
  for (const n of names) {
    if (Object.prototype.hasOwnProperty.call(o, n)) return o[n];
    const lo = n.charAt(0).toLowerCase() + n.slice(1);
    if (Object.prototype.hasOwnProperty.call(o, lo)) return o[lo];
    const up = n.charAt(0).toUpperCase() + n.slice(1);
    if (Object.prototype.hasOwnProperty.call(o, up)) return o[up];
  }
  return undefined;
}

function parseJsonOrNull(t) {
  if (typeof t !== "string" || t.trim() === "") return null;
  try { return JSON.parse(t); } catch { return null; }
}

function resolveConfig() {
  const base =
    process.env.PPIQ_REPLAY_API_BASE ||
    process.env.VITE_API_BASE_URL ||
    process.env.ASPNETCORE_URLS ||
    (process.env.PPIQ_API_HOST && process.env.PPIQ_API_PORT
      ? "http://" + process.env.PPIQ_API_HOST + ":" + process.env.PPIQ_API_PORT
      : null);

  const user = process.env.PPIQ_SMOKE_USERNAME || process.env.VITE_SMOKE_USERNAME;
  const pass = process.env.PPIQ_SMOKE_PASSWORD || process.env.VITE_SMOKE_PASSWORD;
  const db   = process.env.POSTGRES_DB || process.env.POSTGRES_APP_DB;

  const missing = [];
  if (!base) missing.push("VITE_API_BASE_URL");
  if (!user) missing.push("PPIQ_SMOKE_USERNAME");
  if (!pass) missing.push("PPIQ_SMOKE_PASSWORD");
  if (!db && MODE !== "GateFalsification") missing.push("POSTGRES_DB");

  return { base: base ? base.split(";")[0].replace(/\/+$/, "") : null, user, pass, db, missing };
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

// Typed refusal discriminators. The engine already returns machine-readable
// prefixes inside the problem+json errors dictionary; the validator returns
// prose. Both are mapped to one stable discriminator so a failure is routable
// without a human reading six response bodies. No backend DTO is changed.
const PROSE_DISCRIMINATORS = [
  [/requires a selected parameter code/i,          "parameter_required"],
  [/Dimension code is required/i,                  "dimension_required"],
  [/Unsupported dimension code/i,                  "unsupported_dimension_code"],
  [/Unsupported measure code/i,                    "unsupported_measure_code"],
  [/Unsupported chart type/i,                      "unsupported_chart_type"],
  [/Unsupported widget type/i,                     "unsupported_widget_type"],
  [/is not compatible with measure/i,              "chart_measure_incompatible"],
  [/Measure code is required/i,                    "measure_required"],
  [/FromUtc must be before ToUtc/i,                "invalid_time_window"]
];

function collectProblemMessages(body) {
  const out = [];
  if (!body || typeof body !== "object") return out;
  const detail = pick(body, "Detail");
  if (typeof detail === "string" && detail) out.push(detail);
  const errors = pick(body, "Errors");
  if (errors && typeof errors === "object") {
    for (const field of Object.keys(errors)) {
      const list = errors[field];
      if (Array.isArray(list)) for (const msg of list) out.push(field + ": " + String(msg));
      else if (typeof list === "string") out.push(field + ": " + list);
    }
  }
  return out;
}

function extractDiscriminator(messages) {
  // Engine refusals carry an explicit snake_case prefix, e.g.
  //   "dimension_not_carried_by_source: measure 'x' reads a source that ..."
  for (const m of messages) {
    const tagged = /(?:^|:\s*)([a-z][a-z0-9]*(?:_[a-z0-9]+){2,}):/.exec(m);
    if (tagged) return tagged[1];
  }
  for (const m of messages) {
    for (const [re, code] of PROSE_DISCRIMINATORS) if (re.test(m)) return code;
  }
  return null;
}

function definitionOrigin(w) {
  const src = pick(w, "SourceSystem");
  const seeded = typeof src === "string" && /SystemTemplates/i.test(src);
  return {
    sourceSystem: src ?? null,
    isSynthetic: pick(w, "IsSynthetic") === true,
    origin: seeded ? "product-seeded" : "user-authored"
  };
}

function classify(res, w) {
  const provenance = definitionOrigin(w || {});

  if (res.status === 401 || res.status === 403) {
    return { state: "FAILED", discriminator: "identity_or_authorisation",
             ...provenance,
             reason: "http " + res.status + " on the replay identity. An inaccessible " +
                     "customer surface is a gate failure, not a terminal state." };
  }
  if (!res.ok) {
    const messages = collectProblemMessages(res.json);
    const discriminator = extractDiscriminator(messages) || "unclassified_" + res.status;

    // LAW: a user-authored unsupported pairing may legitimately REFUSE. A
    // product-seeded system template that refuses to execute is FAILED. The
    // product must not ship a dashboard it cannot run. Both are reported as
    // FAILED today; refusalEligible marks which ones a later ruling could
    // reclassify, so the distinction is recorded rather than assumed.
    return {
      state: "FAILED",
      discriminator,
      ...provenance,
      refusalEligible: provenance.origin === "user-authored" && !discriminator.startsWith("unclassified_"),
      problemMessages: messages.length > 0 ? messages : undefined,
      reason: "http " + res.status + " / " + discriminator +
              (messages.length > 0 ? " :: " + messages[0] : "")
    };
  }

  const body = res.json;
  if (body === null || typeof body !== "object") {
    return { state: "UNCLASSIFIED", reason: "2xx with non-object body" };
  }
  const licence = pick(body, "LicenceState", "LicenseState", "EntitlementState");
  if (typeof licence === "string" && licence.length > 0) {
    return { state: "BLOCKED", reason: "declared entitlement envelope: " + licence };
  }
  const rows = pick(body, "Rows");
  const widget = pick(body, "Widget");
  if (!Array.isArray(rows) || widget === undefined) {
    return { state: "UNCLASSIFIED",
             reason: "2xx but not a DashboardWidgetQueryResultDto shape; keys=" +
                     Object.keys(body).slice(0, 12).join(",") };
  }
  const warn = pick(body, "Warnings");
  const warnings = Array.isArray(warn) ? warn : [];
  return rows.length > 0
    ? { state: "POPULATED", rowCount: rows.length, warnings }
    : { state: "EMPTY", rowCount: 0, warnings };
}

function buildQuery(w) {
  const filters = parseJsonOrNull(pick(w, "FilterJson"));
  return {
    widgetType:    pick(w, "WidgetType"),
    chartType:     pick(w, "ChartType"),
    dimensionCode: pick(w, "DimensionCode"),
    measureCode:   pick(w, "MeasureCode"),
    parameterCode: pick(w, "ParameterCode") ?? null,
    filters: (filters && typeof filters === "object" && Object.keys(filters).length > 0) ? filters : null,
    options: null
  };
}

async function main() {
  if (!Object.prototype.hasOwnProperty.call(REPORT_BY_MODE, MODE)) {
    fatal = "Unknown PPIQ_REPLAY_MODE '" + MODE + "'. Allowed: " +
            Object.keys(REPORT_BY_MODE).join(", ");
    return;
  }

  const cfg = resolveConfig();
  if (cfg.missing.length > 0) {
    fatal = "Configuration missing from the loaded PPIQ profile: " + cfg.missing.join(", ") +
            ". Run Invoke-PersistedDefinitionReplay.ps1, which loads the canonical profile " +
            "loader. This gate does not skip.";
    return;
  }

  // ---- release-database authority --------------------------------------
  const db = (cfg.db || "").toLowerCase();
  if (MODE === "CurrentRelease" && db === HISTORICAL_DB) {
    fatal = "CurrentRelease refuses '" + HISTORICAL_DB + "'. That database is the frozen M1 " +
            "presentation baseline, not M2 product authority. Use the canonical generic " +
            "application database, or run -HistoricalBaseline for informational regression only.";
    return;
  }
  if (MODE === "HistoricalBaseline" && db !== HISTORICAL_DB) {
    fatal = "HistoricalBaseline requires '" + HISTORICAL_DB + "' but the profile resolves '" +
            cfg.db + "'.";
    return;
  }

  // ---- wrong-database guard --------------------------------------------
  const cs = process.env.ConnectionStrings__PlantProcessDb || "";
  const m = /Database=([^;]+)/i.exec(cs);
  const csDb = m ? m[1].trim() : null;
  if (MODE !== "GateFalsification" && csDb && csDb.toLowerCase() !== db) {
    fatal = "Refusing to run: connection string database '" + csDb +
            "' does not match profile POSTGRES_DB '" + cfg.db + "'.";
    return;
  }

  record({ scope: "config", id: "authority", state: "EVIDENCE",
           mode: MODE, database: cfg.db || null, apiBase: cfg.base,
           authoritative: MODE === "CurrentRelease" });

  // ---- known limitation, recorded rather than assumed away --------------
  if (MODE !== "GateFalsification") {
    record({ scope: "config", id: "runtime-db-identity", state: "EVIDENCE",
             note: "The API cannot be asked which database it reached: /db-health returns a " +
                   "hardcoded literal 'plantprocessiq'. Both the local and presentation " +
                   "profiles bind the same API port. The database above is asserted from the " +
                   "profile the runner loaded, and the runner refuses to reuse an API process " +
                   "it did not start. A truthful database field on /db-health is a Worker-1 ask." });
  }

  // ---- authenticate ------------------------------------------------------
  let login;
  try {
    login = await api(cfg.base, "POST", "/auth/login", null, { UserName: cfg.user, Password: cfg.pass });
  } catch (err) {
    fatal = "Could not reach the API at " + cfg.base + ": " + String(err && err.message);
    return;
  }
  if (!login.ok) { fatal = "Login failed: http " + login.status; return; }
  const token = pick(login.json, "accessToken", "token");
  if (!token) { fatal = "Login returned no bearer token."; return; }

  // ---- enumerate ---------------------------------------------------------
  const defsRes = await api(cfg.base, "GET", "/analytics/dashboard/definitions", token);
  if (!defsRes.ok) { fatal = "Could not enumerate dashboard definitions: http " + defsRes.status; return; }
  const rawDefs = Array.isArray(defsRes.json) ? defsRes.json : pick(defsRes.json, "items", "Items");
  const definitions = Array.isArray(rawDefs) ? rawDefs : [];

  const pagesRes = await api(cfg.base, "GET", "/pages", token);
  const pages = pagesRes.ok && Array.isArray(pagesRes.json) ? pagesRes.json : [];
  for (const p of pages) {
    record({ scope: "page", id: pick(p, "Slug") || pick(p, "Id"), title: pick(p, "Title"),
             backingDashboardDefinitionId: pick(p, "BackingDashboardDefinitionId") ?? null,
             state: pick(p, "IsDeleted") === true ? "SKIPPED_DELETED" : "ENUMERATED" });
  }

  let widgetCount = 0;

  for (const def of definitions) {
    const defId = pick(def, "Id");
    const defCode = pick(def, "DashboardCode");

    if (pick(def, "IsActive") === false) {
      record({ scope: "dashboard", id: defId, code: defCode, state: "SKIPPED_INACTIVE" });
      continue;
    }

    let widgets = pick(def, "Widgets");
    if (!Array.isArray(widgets)) {
      const one = await api(cfg.base, "GET", "/analytics/dashboard/definitions/" + defId, token);
      if (!one.ok) {
        record({ scope: "dashboard", id: defId, code: defCode, state: "FAILED",
                 reason: "definition detail http " + one.status });
        continue;
      }
      widgets = pick(one.json, "Widgets");
    }
    if (!Array.isArray(widgets)) widgets = [];

    record({ scope: "dashboard", id: defId, code: defCode, state: "ENUMERATED", widgetCount: widgets.length });

    for (const w of widgets) {
      if (pick(w, "IsActive") === false) {
        record({ scope: "widget", dashboard: defCode, id: pick(w, "WidgetCode"), state: "SKIPPED_INACTIVE" });
        continue;
      }
      widgetCount += 1;

      const exprEnabled = pick(w, "ExpressionEnabled") === true;
      const expr = pick(w, "QueryExpression");

      let route, res;
      if (exprEnabled && expr) {
        route = "/analytics/dashboard/widgets/execute";
        res = await api(cfg.base, "POST", route, token,
          { queryExpression: expr, widgetType: pick(w, "WidgetType"), chartType: pick(w, "ChartType") });
      } else {
        route = "/analytics/dashboard/widgets/query";
        res = await api(cfg.base, "POST", route, token, buildQuery(w));
      }

      const verdict = classify(res, w);
      record({
        scope: "widget", dashboard: defCode, id: pick(w, "WidgetCode"), title: pick(w, "WidgetTitle"),
        route, chartType: pick(w, "ChartType"), dimensionCode: pick(w, "DimensionCode"),
        measureCode: pick(w, "MeasureCode"), parameterCode: pick(w, "ParameterCode") ?? null,
        expressionEnabled: exprEnabled, httpStatus: res.status,
        ...verdict,
        bodyExcerpt: (verdict.state === "FAILED" || verdict.state === "UNCLASSIFIED")
          ? res.text.slice(0, 2000) : undefined
      });
    }
  }

  if (definitions.length === 0) {
    fatal = "Refusing GREEN: zero dashboard definitions enumerated in '" + cfg.db + "'. " +
            "An empty inventory is indistinguishable from a passing replay. If this is the " +
            "canonical M2 database, this is a real product finding: the generic product owns " +
            "no Release-1 persisted definitions yet. Do NOT resolve it by copying presentation " +
            "or demo dashboards into the M2 database.";
    return;
  }
  if (widgetCount === 0) {
    fatal = "Refusing GREEN: zero active widget definitions replayed across " +
            definitions.length + " dashboard definition(s) in '" + cfg.db + "'.";
    return;
  }
}

const started = new Date().toISOString();
main()
  .catch((e) => { fatal = "Harness threw: " + String(e && e.stack ? e.stack : e); })
  .finally(() => {
    const counts = {};
    for (const r of results) counts[r.state] = (counts[r.state] || 0) + 1;

    const failCount = (counts.FAILED || 0) + (counts.UNCLASSIFIED || 0);
    const verdict = (fatal || failCount > 0) ? "FAIL" : "PASS";

    const manifest = {
      gate: "Persisted Definition Replay",
      backlogOrigin: "T-202",
      release: "M2",
      mode: MODE,
      authoritativeForM2Closure: MODE === "CurrentRelease",
      classification: MODE === "HistoricalBaseline"
        ? "M1 Frozen Baseline Regression Evidence - informational, cannot close an M2 task"
        : (MODE === "GateFalsification"
            ? "Gate self-falsification against an isolated stub - proves the gate can go RED"
            : "M2 Release Truth - authoritative"),
      startedAtUtc: started,
      finishedAtUtc: new Date().toISOString(),
      apiBase: process.env.PPIQ_REPLAY_API_BASE || process.env.VITE_API_BASE_URL || null,
      database: process.env.POSTGRES_DB || null,
      verdict, fatal, counts, entries: results
    };

    fs.mkdirSync(OUT_DIR, { recursive: true });
    fs.writeFileSync(OUT_FILE, JSON.stringify(manifest, null, 2), "utf8");

    console.log("");
    console.log("PERSISTED DEFINITION REPLAY GATE");
    console.log("  mode     : " + MODE + (MODE === "CurrentRelease" ? "  (AUTHORITATIVE)" : "  (informational)"));
    console.log("  api      : " + (manifest.apiBase || "?"));
    console.log("  database : " + (manifest.database || "?"));
    console.log("  manifest : " + OUT_FILE);
    console.log("  counts   : " + JSON.stringify(counts));
    if (fatal) console.log("  fatal    : " + fatal);
    for (const r of results) {
      if (r.state === "FAILED" || r.state === "UNCLASSIFIED") {
        console.log("  [" + r.state + (r.discriminator ? "/" + r.discriminator : "") + "] " +
                    (r.dashboard || r.scope) + "/" + (r.id || "") +
                    (r.origin ? "  (" + r.origin + ")" : "") +
                    " :: " + (r.reason || ""));
      }
    }
    console.log("  VERDICT  : " + verdict);
    process.exit(verdict === "PASS" ? 0 : 2);
  });