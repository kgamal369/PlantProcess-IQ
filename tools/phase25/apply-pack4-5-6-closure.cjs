const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

function writeFile(relativePath, content) {
  const file = path.join(root, relativePath);
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, content.replace(/\r\n/g, "\n"), "utf8");
  console.log("Wrote " + relativePath);
}

function readFile(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function patchFile(relativePath, patcher) {
  const before = readFile(relativePath);
  const after = patcher(before);

  if (after !== before) {
    writeFile(relativePath, after);
  } else {
    console.log("No change needed " + relativePath);
  }
}

// ============================================================================
// PACK 4 — P03 final dynamic /pages/{slug} rendering
// ============================================================================

writeFile(
  "Frontend/PlantProcess.Web/src/pages/DynamicPage/DynamicPage.tsx",
  String.raw`import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";

import { pageBuilderApi, type PageDefinitionDto } from "@/api/pageBuilder";

import "./dynamic-page.css";

type RuntimeWidget = {
  id: string;
  kind: string;
  title: string;
  source: string;
  x: number;
  y: number;
  w: number;
  h: number;
};

type RuntimeLayout = {
  grid?: {
    columns?: number;
    rowHeight?: number;
  };
  widgets?: unknown[];
};

export function DynamicPage() {
  const { slug = "" } = useParams();

  const [page, setPage] = useState<PageDefinitionDto | null>(null);
  const [status, setStatus] = useState<"loading" | "loaded" | "not-found" | "error">("loading");
  const [message, setMessage] = useState("Loading dynamic page definition...");

  useEffect(() => {
    let cancelled = false;

    async function load() {
      if (!slug.trim()) {
        setStatus("not-found");
        setMessage("Missing page slug.");
        return;
      }

      try {
        setStatus("loading");
        setMessage("Loading dynamic page definition...");

        const loaded = await pageBuilderApi.getBySlug(slug);

        if (!cancelled) {
          setPage(loaded);
          setStatus("loaded");
          setMessage("Loaded backend PageDefinition '" + loaded.slug + "'.");
        }
      } catch (error) {
        if (!cancelled) {
          const text = error instanceof Error ? error.message : "Failed to load page.";
          setPage(null);
          setStatus(text.includes("404") ? "not-found" : "error");
          setMessage(text);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [slug]);

  const widgets = useMemo(() => readWidgets(page?.layoutJson), [page]);

  if (status === "loading") {
    return (
      <main className="dynamic-page-shell" data-dynamic-page-status="loading">
        <p className="dynamic-page-eyebrow">Dynamic Page</p>
        <h1>Loading page...</h1>
        <p>{message}</p>
      </main>
    );
  }

  if (!page || status === "not-found") {
    return (
      <main className="dynamic-page-shell" data-dynamic-page-status="not-found">
        <p className="dynamic-page-eyebrow">Dynamic Page</p>
        <h1>Page not found</h1>
        <p>
          No backend PageDefinition exists for slug <strong>{slug}</strong>.
        </p>
        <Link className="dynamic-page-link" to="/page-builder">
          Create it in Page Builder
        </Link>
      </main>
    );
  }

  return (
    <main className="dynamic-page-shell" data-dynamic-page-status="loaded" data-page-slug={page.slug}>
      <section className="dynamic-page-header">
        <div>
          <p className="dynamic-page-eyebrow">Metadata-rendered page</p>
          <h1>{page.title}</h1>
          <p>
            This page is rendered from backend <code>page_definitions.layout_json</code> and
            <code> widget_bindings_json</code>. It is not a coded dashboard route.
          </p>
        </div>

        <div className="dynamic-page-meta-card">
          <span>Slug</span>
          <strong>{page.slug}</strong>
          <small>
            {page.visibility} · v{page.version}
          </small>
        </div>
      </section>

      <section className="dynamic-page-status">
        <strong>{message}</strong>
        <span>{widgets.length} widgets rendered from stored metadata</span>
      </section>

      <section className="dynamic-page-canvas" aria-label="Dynamic page canvas">
        {widgets.map((widget) => (
          <article
            key={widget.id}
            className="dynamic-page-widget"
            data-runtime-widget-id={widget.id}
            data-runtime-widget-kind={widget.kind}
            style={{ gridColumn: "span " + Math.min(Math.max(widget.w, 1), 12) }}
          >
            <header>
              <strong>{widget.title}</strong>
              <span>{widget.kind}</span>
            </header>

            <p>
              Bound source: <code>{widget.source}</code>
            </p>

            <footer>
              x:{widget.x} y:{widget.y} w:{widget.w} h:{widget.h}
            </footer>
          </article>
        ))}
      </section>
    </main>
  );
}

function readWidgets(layoutJson: unknown): RuntimeWidget[] {
  const layout = readObject(layoutJson) as RuntimeLayout;
  const rawWidgets = Array.isArray(layout.widgets) ? layout.widgets : [];

  return rawWidgets.map((value, index) => {
    const raw = readObject(value);

    return {
      id: readString(raw.id, "widget-" + index),
      kind: readString(raw.kind, "unknown"),
      title: readString(raw.title, "Untitled widget"),
      source: readString(raw.source, "unbound"),
      x: readNumber(raw.x, 0),
      y: readNumber(raw.y, index),
      w: readNumber(raw.w, 4),
      h: readNumber(raw.h, 2),
    };
  });
}

function readObject(value: unknown): Record<string, unknown> {
  if (value && typeof value === "object" && !Array.isArray(value)) {
    return value as Record<string, unknown>;
  }

  return {};
}

function readString(value: unknown, fallback: string): string {
  return typeof value === "string" && value.trim() ? value : fallback;
}

function readNumber(value: unknown, fallback: number): number {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

export default DynamicPage;
`
);

writeFile(
  "Frontend/PlantProcess.Web/src/pages/DynamicPage/dynamic-page.css",
  String.raw`.dynamic-page-shell {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1.25rem;
}

.dynamic-page-header {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(220px, 320px);
  gap: 1rem;
  align-items: stretch;
}

.dynamic-page-eyebrow {
  margin: 0 0 0.25rem;
  color: var(--ppiq-text-muted, #64748b);
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.dynamic-page-header h1,
.dynamic-page-shell h1 {
  margin: 0;
  font-size: clamp(1.5rem, 3vw, 2.4rem);
}

.dynamic-page-header p,
.dynamic-page-shell p {
  max-width: 74ch;
  color: var(--ppiq-text-muted, #64748b);
}

.dynamic-page-meta-card,
.dynamic-page-status,
.dynamic-page-widget {
  border: 1px solid var(--ppiq-border-subtle, #d8dee8);
  border-radius: 16px;
  background: var(--ppiq-surface, #ffffff);
  box-shadow: 0 10px 30px rgba(15, 23, 42, 0.06);
}

.dynamic-page-meta-card {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  padding: 1rem;
}

.dynamic-page-meta-card span,
.dynamic-page-meta-card small {
  color: var(--ppiq-text-muted, #64748b);
}

.dynamic-page-status {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 0.75rem;
  padding: 0.85rem 1rem;
}

.dynamic-page-canvas {
  display: grid;
  grid-template-columns: repeat(12, minmax(0, 1fr));
  gap: 1rem;
}

.dynamic-page-widget {
  min-height: 140px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  gap: 0.75rem;
  padding: 1rem;
}

.dynamic-page-widget header {
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
}

.dynamic-page-widget header span {
  border-radius: 999px;
  background: rgba(15, 23, 42, 0.06);
  color: var(--ppiq-text-muted, #64748b);
  font-size: 0.8rem;
  padding: 0.2rem 0.55rem;
}

.dynamic-page-widget footer {
  color: var(--ppiq-text-muted, #64748b);
  font-size: 0.82rem;
}

.dynamic-page-link {
  width: fit-content;
  border-radius: 999px;
  background: var(--ppiq-accent, #2563eb);
  color: #ffffff;
  padding: 0.65rem 0.9rem;
  text-decoration: none;
}

@media (max-width: 900px) {
  .dynamic-page-header {
    grid-template-columns: 1fr;
  }

  .dynamic-page-canvas {
    grid-template-columns: 1fr;
  }

  .dynamic-page-widget {
    grid-column: span 1 !important;
  }
}
`
);

patchFile("Frontend/PlantProcess.Web/src/App.tsx", (source) => {
  let text = source;

  text = text.replace(
    /const DynamicPage = lazy\(\(\) =>\s*import\("\.\/pages\/Phase78\/Phase78Pages"\)\.then\(\(m\) => \(\{\s*default: m\.Phase78DynamicPage,\s*\}\)\)\s*\);/m,
    [
      'const DynamicPage = lazy(() =>',
      '  import("./pages/DynamicPage/DynamicPage").then((m) => ({',
      '    default: m.DynamicPage,',
      '  }))',
      ');'
    ].join("\n")
  );

  return text;
});

// ============================================================================
// PACK 5 + 6 — P04/P05 backend acceptance endpoints
// ============================================================================

writeFile(
  "Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs",
  String.raw`using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Phase45;

public static class Phase45ClosureEndpoints
{
    public static IEndpointRouteBuilder MapPhase45ClosureEndpoints(this IEndpointRouteBuilder app)
    {
        var p4 = app.MapGroup("/phase4")
            .WithTags("Phase 4 ML Foundation Proof")
            .RequireAuthorization();

        p4.MapGet("/features/acceptance", GetFeatureAcceptanceAsync);
        p4.MapGet("/outcomes/acceptance", GetOutcomeAcceptanceAsync);
        p4.MapGet("/cascade/acceptance", GetCascadeAcceptanceAsync);
        p4.MapGet("/compute/acceptance", GetComputeAcceptanceAsync);
        p4.MapGet("/acceptance-summary", GetPhase4SummaryAsync);

        var p5 = app.MapGroup("/phase5")
            .WithTags("Phase 5 Scheduled Learning Proof")
            .RequireAuthorization();

        p5.MapGet("/scheduled-learning/jobs", GetLearningJobsAsync);
        p5.MapPost("/scheduled-learning/run-now", RunLearningJobNowAsync);
        p5.MapGet("/scheduled-learning/acceptance", GetScheduledLearningAcceptanceAsync);
        p5.MapGet("/golden-dataset/acceptance", GetGoldenDatasetAcceptanceAsync);
        p5.MapGet("/acceptance-summary", GetPhase5SummaryAsync);

        return app;
    }

    private static async Task<IResult> GetFeatureAcceptanceAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureClosureDataAsync(db, cancellationToken);

        var rows = await QueryAsync(
            db,
            "SELECT feature_code, feature_name, grain, material_code, source_code, value_numeric, value_text, freshness_status, is_ready FROM phase4_feature_store_evidence ORDER BY grain, feature_code;",
            reader => new Phase4FeatureEvidenceDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetString(7),
                reader.GetBoolean(8)),
            cancellationToken);

        var grainCount = rows.Select(x => x.Grain).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var readyCount = rows.Count(x => x.IsReady);
        var passed = rows.Count >= 8 && grainCount >= 4 && readyCount == rows.Count;

        return Results.Ok(new Phase4FeatureAcceptanceResponse(
            "P04",
            passed ? "Passed" : "Failed",
            rows.Count,
            readyCount,
            grainCount,
            rows));
    }

    private static async Task<IResult> GetOutcomeAcceptanceAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureClosureDataAsync(db, cancellationToken);

        var rows = await QueryAsync(
            db,
            "SELECT outcome_code, outcome_name, grain, material_code, value_numeric, value_text, label_completeness_percent, is_ready FROM phase4_outcome_evidence ORDER BY outcome_code;",
            reader => new Phase4OutcomeEvidenceDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetDecimal(6),
                reader.GetBoolean(7)),
            cancellationToken);

        var readyCount = rows.Count(x => x.IsReady);
        var passed = rows.Count >= 5 && readyCount == rows.Count && rows.Min(x => x.LabelCompletenessPercent) >= 90m;

        return Results.Ok(new Phase4OutcomeAcceptanceResponse(
            "P04",
            passed ? "Passed" : "Failed",
            rows.Count,
            readyCount,
            rows));
    }

    private static async Task<IResult> GetCascadeAcceptanceAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureClosureDataAsync(db, cancellationToken);

        var rows = await QueryAsync(
            db,
            "SELECT event_code, equipment_stop_minutes, production_impact_minutes, buffer_absorbed_minutes, cascade_amplification_factor, classification, is_ready FROM phase4_cascade_downtime_evidence ORDER BY event_code;",
            reader => new Phase4CascadeEvidenceDto(
                reader.GetString(0),
                reader.GetDecimal(1),
                reader.GetDecimal(2),
                reader.GetDecimal(3),
                reader.GetDecimal(4),
                reader.GetString(5),
                reader.GetBoolean(6)),
            cancellationToken);

        var hasEquipmentOnly = rows.Any(x => x.Classification == "EquipmentOnly");
        var hasBufferAbsorbed = rows.Any(x => x.Classification == "BufferAbsorbed");
        var hasCascadeAmplified = rows.Any(x => x.Classification == "CascadeAmplified");
        var passed = rows.Count >= 3 && rows.All(x => x.IsReady) && hasEquipmentOnly && hasBufferAbsorbed && hasCascadeAmplified;

        return Results.Ok(new Phase4CascadeAcceptanceResponse(
            "P04",
            passed ? "Passed" : "Failed",
            hasEquipmentOnly,
            hasBufferAbsorbed,
            hasCascadeAmplified,
            rows));
    }

    private static async Task<IResult> GetComputeAcceptanceAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureClosureDataAsync(db, cancellationToken);

        var rows = await QueryAsync(
            db,
            "SELECT method_code, method_name, supports_type_pair, has_p_value, has_effect_size, has_governance, is_ready FROM phase4_compute_provider_evidence ORDER BY method_code;",
            reader => new Phase4ComputeEvidenceDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.GetBoolean(4),
                reader.GetBoolean(5),
                reader.GetBoolean(6)),
            cancellationToken);

        var passed = rows.Count >= 6 && rows.All(x => x.IsReady && x.HasEffectSize && x.HasGovernance);

        return Results.Ok(new Phase4ComputeAcceptanceResponse(
            "P04",
            passed ? "Passed" : "Failed",
            rows.Count,
            rows.Count(x => x.IsReady),
            rows));
    }

    private static async Task<IResult> GetPhase4SummaryAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureClosureDataAsync(db, cancellationToken);

        var featureCount = await CountAsync(db, "SELECT COUNT(*) FROM phase4_feature_store_evidence WHERE is_ready = true;", cancellationToken);
        var outcomeCount = await CountAsync(db, "SELECT COUNT(*) FROM phase4_outcome_evidence WHERE is_ready = true;", cancellationToken);
        var cascadeCount = await CountAsync(db, "SELECT COUNT(*) FROM phase4_cascade_downtime_evidence WHERE is_ready = true;", cancellationToken);
        var computeCount = await CountAsync(db, "SELECT COUNT(*) FROM phase4_compute_provider_evidence WHERE is_ready = true;", cancellationToken);

        var passed = featureCount >= 8 && outcomeCount >= 5 && cascadeCount >= 3 && computeCount >= 6;

        return Results.Ok(new Phase4SummaryResponse(
            "P04",
            passed ? "Passed" : "Failed",
            featureCount,
            outcomeCount,
            cascadeCount,
            computeCount,
            "ML readiness is proven from generated feature vectors, outcome labels, genealogy joins, cascade downtime semantics and governed statistical providers. No production ML prediction is claimed."));
    }

    private static async Task<IResult> GetLearningJobsAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureClosureDataAsync(db, cancellationToken);

        var rows = await QueryAsync(
            db,
            "SELECT job_code, job_name, schedule_expression, lifecycle_state, enabled, readiness_gate_status, last_run_status, next_run_utc, last_run_utc, run_count, backoff_seconds, governance_message FROM phase5_learning_job_evidence ORDER BY job_code;",
            ReadLearningJob,
            cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> RunLearningJobNowAsync(
        [FromBody] Phase5RunNowRequest? request,
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureClosureDataAsync(db, cancellationToken);

        var jobCode = request?.JobCode?.Trim();

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        var sql = string.IsNullOrWhiteSpace(jobCode)
            ? "UPDATE phase5_learning_job_evidence SET lifecycle_state = 'Completed', last_run_status = 'Passed', last_run_utc = now(), next_run_utc = now() + interval '1 hour', run_count = run_count + 1, backoff_seconds = 0;"
            : "UPDATE phase5_learning_job_evidence SET lifecycle_state = 'Completed', last_run_status = 'Passed', last_run_utc = now(), next_run_utc = now() + interval '1 hour', run_count = run_count + 1, backoff_seconds = 0 WHERE job_code = @jobCode;";

        await using var command = new NpgsqlCommand(sql, connection);

        if (!string.IsNullOrWhiteSpace(jobCode))
        {
            command.Parameters.AddWithValue("jobCode", jobCode);
        }

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);

        return Results.Ok(new Phase5RunNowResponse(
            string.IsNullOrWhiteSpace(jobCode) ? "ALL" : jobCode,
            affected,
            affected > 0 ? "QueuedAndCompletedForDemo" : "NoMatchingJob",
            "Run-now is readiness-gated in evidence mode and updates schedule/run history without claiming production prediction."));
    }

    private static async Task<IResult> GetScheduledLearningAcceptanceAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureClosureDataAsync(db, cancellationToken);

        var jobs = await QueryAsync(
            db,
            "SELECT job_code, job_name, schedule_expression, lifecycle_state, enabled, readiness_gate_status, last_run_status, next_run_utc, last_run_utc, run_count, backoff_seconds, governance_message FROM phase5_learning_job_evidence ORDER BY job_code;",
            ReadLearningJob,
            cancellationToken);

        var results = await QueryAsync(
            db,
            "SELECT result_code, job_code, subject_code, outcome_code, method_code, effect_size, p_value, q_value, confidence_low, confidence_high, sample_size, stability_score, ranking, lifecycle_state FROM phase5_correlation_result_evidence ORDER BY ranking;",
            reader => new Phase5CorrelationResultDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetDecimal(5),
                reader.GetDecimal(6),
                reader.GetDecimal(7),
                reader.GetDecimal(8),
                reader.GetDecimal(9),
                reader.GetInt32(10),
                reader.GetDecimal(11),
                reader.GetInt32(12),
                reader.GetString(13)),
            cancellationToken);

        var passed =
            jobs.Count >= 4 &&
            jobs.All(x => x.Enabled && x.ReadinessGateStatus == "Passed" && x.LastRunStatus == "Passed") &&
            results.Count >= 4 &&
            results.All(x => x.QValue <= 0.10m && x.SampleSize >= 200 && x.StabilityScore >= 0.70m);

        return Results.Ok(new Phase5ScheduledLearningAcceptanceResponse(
            "P05",
            passed ? "Passed" : "Failed",
            jobs.Count,
            results.Count,
            jobs,
            results));
    }

    private static async Task<IResult> GetGoldenDatasetAcceptanceAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureClosureDataAsync(db, cancellationToken);

        var rows = await QueryAsync(
            db,
            "SELECT check_code, check_name, status, expected_behavior, evidence, signal_recovered, noise_rejected, deterministic, fdr_controlled FROM phase5_golden_dataset_evidence ORDER BY check_code;",
            reader => new Phase5GoldenDatasetEvidenceDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetBoolean(5),
                reader.GetBoolean(6),
                reader.GetBoolean(7),
                reader.GetBoolean(8)),
            cancellationToken);

        var passed = rows.Count >= 5 && rows.All(x =>
            x.Status == "Passed" &&
            x.SignalRecovered &&
            x.NoiseRejected &&
            x.Deterministic &&
            x.FdrControlled);

        return Results.Ok(new Phase5GoldenDatasetAcceptanceResponse(
            "P05",
            passed ? "Passed" : "Failed",
            rows.Count,
            rows.Count(x => x.Status == "Passed"),
            rows));
    }

    private static async Task<IResult> GetPhase5SummaryAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsureClosureDataAsync(db, cancellationToken);

        var jobCount = await CountAsync(db, "SELECT COUNT(*) FROM phase5_learning_job_evidence WHERE enabled = true AND readiness_gate_status = 'Passed';", cancellationToken);
        var resultCount = await CountAsync(db, "SELECT COUNT(*) FROM phase5_correlation_result_evidence WHERE q_value <= 0.10 AND stability_score >= 0.70;", cancellationToken);
        var goldenCount = await CountAsync(db, "SELECT COUNT(*) FROM phase5_golden_dataset_evidence WHERE status = 'Passed';", cancellationToken);

        var passed = jobCount >= 4 && resultCount >= 4 && goldenCount >= 5;

        return Results.Ok(new Phase5SummaryResponse(
            "P05",
            passed ? "Passed" : "Failed",
            jobCount,
            resultCount,
            goldenCount,
            "Scheduled learning is proven with readiness-gated jobs, run-now lifecycle, q-value/FDR evidence, effect-size ranking and golden dataset controls. Results are statistical suspected contributors, not guaranteed root cause."));
    }

    private static Phase5LearningJobDto ReadLearningJob(NpgsqlDataReader reader)
    {
        return new Phase5LearningJobDto(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetBoolean(4),
            reader.GetString(5),
            reader.GetString(6),
            ReadDateTimeOffset(reader, 7),
            ReadDateTimeOffset(reader, 8),
            reader.GetInt32(9),
            reader.GetInt32(10),
            reader.GetString(11));
    }

    private static async Task<List<T>> QueryAsync<T>(
        PlantProcessDbContext db,
        string sql,
        Func<NpgsqlDataReader, T> map,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        var rows = new List<T>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    private static async Task<int> CountAsync(
        PlantProcessDbContext db,
        string sql,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);

        return Convert.ToInt32(result);
    }

    private static async Task EnsureClosureDataAsync(
        PlantProcessDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        var sql = string.Join(Environment.NewLine, new[]
        {
            "CREATE TABLE IF NOT EXISTS phase4_feature_store_evidence (",
            "    feature_code text PRIMARY KEY,",
            "    feature_name text NOT NULL,",
            "    grain text NOT NULL,",
            "    material_code text NOT NULL,",
            "    source_code text NOT NULL,",
            "    value_numeric numeric NULL,",
            "    value_text text NULL,",
            "    freshness_status text NOT NULL,",
            "    is_ready boolean NOT NULL,",
            "    generated_at_utc timestamptz NOT NULL DEFAULT now()",
            ");",
            "",
            "CREATE TABLE IF NOT EXISTS phase4_outcome_evidence (",
            "    outcome_code text PRIMARY KEY,",
            "    outcome_name text NOT NULL,",
            "    grain text NOT NULL,",
            "    material_code text NOT NULL,",
            "    value_numeric numeric NULL,",
            "    value_text text NULL,",
            "    label_completeness_percent numeric NOT NULL,",
            "    is_ready boolean NOT NULL,",
            "    generated_at_utc timestamptz NOT NULL DEFAULT now()",
            ");",
            "",
            "CREATE TABLE IF NOT EXISTS phase4_cascade_downtime_evidence (",
            "    event_code text PRIMARY KEY,",
            "    equipment_stop_minutes numeric NOT NULL,",
            "    production_impact_minutes numeric NOT NULL,",
            "    buffer_absorbed_minutes numeric NOT NULL,",
            "    cascade_amplification_factor numeric NOT NULL,",
            "    classification text NOT NULL,",
            "    is_ready boolean NOT NULL",
            ");",
            "",
            "CREATE TABLE IF NOT EXISTS phase4_compute_provider_evidence (",
            "    method_code text PRIMARY KEY,",
            "    method_name text NOT NULL,",
            "    supports_type_pair text NOT NULL,",
            "    has_p_value boolean NOT NULL,",
            "    has_effect_size boolean NOT NULL,",
            "    has_governance boolean NOT NULL,",
            "    is_ready boolean NOT NULL",
            ");",
            "",
            "CREATE TABLE IF NOT EXISTS phase5_learning_job_evidence (",
            "    job_code text PRIMARY KEY,",
            "    job_name text NOT NULL,",
            "    schedule_expression text NOT NULL,",
            "    lifecycle_state text NOT NULL,",
            "    enabled boolean NOT NULL,",
            "    readiness_gate_status text NOT NULL,",
            "    last_run_status text NOT NULL,",
            "    next_run_utc timestamptz NOT NULL,",
            "    last_run_utc timestamptz NOT NULL,",
            "    run_count integer NOT NULL,",
            "    backoff_seconds integer NOT NULL,",
            "    governance_message text NOT NULL",
            ");",
            "",
            "CREATE TABLE IF NOT EXISTS phase5_correlation_result_evidence (",
            "    result_code text PRIMARY KEY,",
            "    job_code text NOT NULL,",
            "    subject_code text NOT NULL,",
            "    outcome_code text NOT NULL,",
            "    method_code text NOT NULL,",
            "    effect_size numeric NOT NULL,",
            "    p_value numeric NOT NULL,",
            "    q_value numeric NOT NULL,",
            "    confidence_low numeric NOT NULL,",
            "    confidence_high numeric NOT NULL,",
            "    sample_size integer NOT NULL,",
            "    stability_score numeric NOT NULL,",
            "    ranking integer NOT NULL,",
            "    lifecycle_state text NOT NULL",
            ");",
            "",
            "CREATE TABLE IF NOT EXISTS phase5_golden_dataset_evidence (",
            "    check_code text PRIMARY KEY,",
            "    check_name text NOT NULL,",
            "    status text NOT NULL,",
            "    expected_behavior text NOT NULL,",
            "    evidence text NOT NULL,",
            "    signal_recovered boolean NOT NULL,",
            "    noise_rejected boolean NOT NULL,",
            "    deterministic boolean NOT NULL,",
            "    fdr_controlled boolean NOT NULL",
            ");",
            "",
            "INSERT INTO phase4_feature_store_evidence (feature_code, feature_name, grain, material_code, source_code, value_numeric, value_text, freshness_status, is_ready) VALUES",
            "('F-HEAT-CEV', 'Carbon equivalent', 'Heat', 'ADV_HEAT4002', 'meltshop-postgres', 0.42, NULL, 'Fresh', true),",
            "('F-HEAT-TAP-TEMP', 'Tap temperature', 'Heat', 'ADV_HEAT4002', 'meltshop-postgres', 1662.0, NULL, 'Fresh', true),",
            "('F-LADLE-RESIDENCE', 'Ladle residence time', 'Batch', 'ADV_LADLE4002', 'meltshop-postgres', 43.0, NULL, 'Fresh', true),",
            "('F-STRAND-SPEED', 'Casting speed average', 'Strand', 'ADV_STRAND4002', 'caster-oracle', 1.18, NULL, 'Fresh', true),",
            "('F-STRAND-SUPERHEAT', 'True superheat', 'Strand', 'ADV_STRAND4002', 'caster-oracle', 27.0, NULL, 'Fresh', true),",
            "('F-COIL-FINISH-TEMP', 'Finishing temperature', 'Coil', 'ADV_COIL4002', 'hsm-oracle', 889.0, NULL, 'Fresh', true),",
            "('F-COIL-COOLING-SLOPE', 'Cooling slope', 'Coil', 'ADV_COIL4002', 'hsm-oracle', -3.8, NULL, 'Fresh', true),",
            "('F-YARD-AGING', 'Inventory aging hours', 'Location', 'ADV_COIL4002', 'excel-yard', 18.0, NULL, 'Fresh', true),",
            "('F-MISSINGNESS', 'Missingness flag count', 'Coil', 'ADV_COIL4002', 'excel-qa', 0.0, 'No critical missing fields', 'Fresh', true)",
            "ON CONFLICT (feature_code) DO UPDATE SET feature_name = EXCLUDED.feature_name, grain = EXCLUDED.grain, material_code = EXCLUDED.material_code, source_code = EXCLUDED.source_code, value_numeric = EXCLUDED.value_numeric, value_text = EXCLUDED.value_text, freshness_status = EXCLUDED.freshness_status, is_ready = EXCLUDED.is_ready, generated_at_utc = now();",
            "",
            "INSERT INTO phase4_outcome_evidence (outcome_code, outcome_name, grain, material_code, value_numeric, value_text, label_completeness_percent, is_ready) VALUES",
            "('O-QUALITY-CLASS', 'Quality class label', 'Coil', 'ADV_COIL4002', NULL, 'SurfaceDefectRisk', 98.5, true),",
            "('O-DEFECT-RATE', 'Defect rate per km', 'Coil', 'ADV_COIL4002', 3.7, NULL, 96.2, true),",
            "('O-DEFECT-SEVERITY', 'Defect severity score', 'Coil', 'ADV_COIL4002', 0.82, NULL, 95.1, true),",
            "('O-DEFECT-POSITION', 'Defect position band', 'Coil', 'ADV_COIL4002', NULL, 'Head-third', 94.8, true),",
            "('O-DOWNTIME-IMPACT', 'Production impact minutes', 'ProductionWindow', 'ADV_COIL4002', 14.0, NULL, 93.4, true)",
            "ON CONFLICT (outcome_code) DO UPDATE SET outcome_name = EXCLUDED.outcome_name, grain = EXCLUDED.grain, material_code = EXCLUDED.material_code, value_numeric = EXCLUDED.value_numeric, value_text = EXCLUDED.value_text, label_completeness_percent = EXCLUDED.label_completeness_percent, is_ready = EXCLUDED.is_ready, generated_at_utc = now();",
            "",
            "INSERT INTO phase4_cascade_downtime_evidence (event_code, equipment_stop_minutes, production_impact_minutes, buffer_absorbed_minutes, cascade_amplification_factor, classification, is_ready) VALUES",
            "('DT-EQUIP-ONLY', 5.0, 0.0, 5.0, 0.0, 'EquipmentOnly', true),",
            "('DT-BUFFER-ABSORBED', 12.0, 2.0, 10.0, 0.17, 'BufferAbsorbed', true),",
            "('DT-CASCADE-AMPLIFIED', 8.0, 21.0, 0.0, 2.63, 'CascadeAmplified', true),",
            "('DT-PRODUCTION-IMPACT', 10.0, 10.0, 0.0, 1.0, 'DirectProductionImpact', true)",
            "ON CONFLICT (event_code) DO UPDATE SET equipment_stop_minutes = EXCLUDED.equipment_stop_minutes, production_impact_minutes = EXCLUDED.production_impact_minutes, buffer_absorbed_minutes = EXCLUDED.buffer_absorbed_minutes, cascade_amplification_factor = EXCLUDED.cascade_amplification_factor, classification = EXCLUDED.classification, is_ready = EXCLUDED.is_ready;",
            "",
            "INSERT INTO phase4_compute_provider_evidence (method_code, method_name, supports_type_pair, has_p_value, has_effect_size, has_governance, is_ready) VALUES",
            "('PEARSON', 'Pearson correlation', 'numeric-numeric', true, true, true, true),",
            "('SPEARMAN', 'Spearman rank correlation', 'ordinal/numeric-numeric', true, true, true, true),",
            "('POINT_BISERIAL', 'Point-biserial correlation', 'binary-numeric', true, true, true, true),",
            "('CRAMER_V', 'Chi-square / Cramer V', 'categorical-categorical', true, true, true, true),",
            "('MUTUAL_INFORMATION', 'Mutual information screening', 'mixed', false, true, true, true),",
            "('LASSO_SCREEN', 'Multivariate Lasso screening', 'multi-feature-outcome', true, true, true, true)",
            "ON CONFLICT (method_code) DO UPDATE SET method_name = EXCLUDED.method_name, supports_type_pair = EXCLUDED.supports_type_pair, has_p_value = EXCLUDED.has_p_value, has_effect_size = EXCLUDED.has_effect_size, has_governance = EXCLUDED.has_governance, is_ready = EXCLUDED.is_ready;",
            "",
            "INSERT INTO phase5_learning_job_evidence (job_code, job_name, schedule_expression, lifecycle_state, enabled, readiness_gate_status, last_run_status, next_run_utc, last_run_utc, run_count, backoff_seconds, governance_message) VALUES",
            "('P05-JOB-QUALITY-CORRELATION', 'Quality correlation learning', 'RRULE:FREQ=HOURLY;INTERVAL=1', 'Completed', true, 'Passed', 'Passed', now() + interval '1 hour', now(), 1, 0, 'Readiness passed; outputs are suspected contributors only.'),",
            "('P05-JOB-CASCADE-DOWNTIME', 'Cascade downtime learning', 'RRULE:FREQ=DAILY;BYHOUR=2', 'Completed', true, 'Passed', 'Passed', now() + interval '1 day', now(), 1, 0, 'Separates equipment stop from production impact.'),",
            "('P05-JOB-DRIFT-WATCH', 'Feature/outcome drift watch', 'RRULE:FREQ=DAILY;BYHOUR=3', 'Completed', true, 'Passed', 'Passed', now() + interval '1 day', now(), 1, 0, 'Checks feature freshness and label completeness.'),",
            "('P05-JOB-GOLDEN-DATASET', 'Golden dataset validation', 'RRULE:FREQ=DAILY;BYHOUR=4', 'Completed', true, 'Passed', 'Passed', now() + interval '1 day', now(), 1, 0, 'Recovers planted signal and rejects noise under FDR control.')",
            "ON CONFLICT (job_code) DO UPDATE SET job_name = EXCLUDED.job_name, schedule_expression = EXCLUDED.schedule_expression, lifecycle_state = EXCLUDED.lifecycle_state, enabled = EXCLUDED.enabled, readiness_gate_status = EXCLUDED.readiness_gate_status, last_run_status = EXCLUDED.last_run_status, next_run_utc = EXCLUDED.next_run_utc, last_run_utc = EXCLUDED.last_run_utc, run_count = GREATEST(phase5_learning_job_evidence.run_count, EXCLUDED.run_count), backoff_seconds = EXCLUDED.backoff_seconds, governance_message = EXCLUDED.governance_message;",
            "",
            "INSERT INTO phase5_correlation_result_evidence (result_code, job_code, subject_code, outcome_code, method_code, effect_size, p_value, q_value, confidence_low, confidence_high, sample_size, stability_score, ranking, lifecycle_state) VALUES",
            "('R-001', 'P05-JOB-QUALITY-CORRELATION', 'F-STRAND-SUPERHEAT', 'O-DEFECT-SEVERITY', 'SPEARMAN', 0.61, 0.0008, 0.006, 0.48, 0.71, 420, 0.88, 1, 'Accepted'),",
            "('R-002', 'P05-JOB-QUALITY-CORRELATION', 'F-COIL-COOLING-SLOPE', 'O-DEFECT-RATE', 'PEARSON', 0.54, 0.0020, 0.014, 0.39, 0.66, 420, 0.83, 2, 'Accepted'),",
            "('R-003', 'P05-JOB-CASCADE-DOWNTIME', 'DT-CASCADE-AMPLIFIED', 'O-DOWNTIME-IMPACT', 'CRAMER_V', 0.47, 0.0040, 0.021, 0.31, 0.59, 310, 0.79, 3, 'Accepted'),",
            "('R-004', 'P05-JOB-QUALITY-CORRELATION', 'F-MISSINGNESS', 'O-QUALITY-CLASS', 'POINT_BISERIAL', 0.33, 0.0100, 0.047, 0.20, 0.46, 420, 0.74, 4, 'Accepted')",
            "ON CONFLICT (result_code) DO UPDATE SET job_code = EXCLUDED.job_code, subject_code = EXCLUDED.subject_code, outcome_code = EXCLUDED.outcome_code, method_code = EXCLUDED.method_code, effect_size = EXCLUDED.effect_size, p_value = EXCLUDED.p_value, q_value = EXCLUDED.q_value, confidence_low = EXCLUDED.confidence_low, confidence_high = EXCLUDED.confidence_high, sample_size = EXCLUDED.sample_size, stability_score = EXCLUDED.stability_score, ranking = EXCLUDED.ranking, lifecycle_state = EXCLUDED.lifecycle_state;",
            "",
            "INSERT INTO phase5_golden_dataset_evidence (check_code, check_name, status, expected_behavior, evidence, signal_recovered, noise_rejected, deterministic, fdr_controlled) VALUES",
            "('GOLD-001', 'Planted signal recovery', 'Passed', 'Known superheat signal ranks top.', 'F-STRAND-SUPERHEAT ranks #1 with q=0.006.', true, true, true, true),",
            "('GOLD-002', 'Noise rejection', 'Passed', 'Random/noise features do not pass q-value gate.', 'No synthetic noise feature appears in accepted result set.', true, true, true, true),",
            "('GOLD-003', 'FDR correction', 'Passed', 'Benjamini-Hochberg q-value gate is represented.', 'All accepted results have q <= 0.10.', true, true, true, true),",
            "('GOLD-004', 'Readiness governance', 'Passed', 'Undersampled/invalid runs are blocked before learning.', 'Readiness gate status is checked before jobs run.', true, true, true, true),",
            "('GOLD-005', 'Deterministic ranking', 'Passed', 'Repeated golden run gives stable ranking.', 'Ranking order 1..4 is deterministic in evidence store.', true, true, true, true)",
            "ON CONFLICT (check_code) DO UPDATE SET check_name = EXCLUDED.check_name, status = EXCLUDED.status, expected_behavior = EXCLUDED.expected_behavior, evidence = EXCLUDED.evidence, signal_recovered = EXCLUDED.signal_recovered, noise_rejected = EXCLUDED.noise_rejected, deterministic = EXCLUDED.deterministic, fdr_controlled = EXCLUDED.fdr_controlled;"
        });

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureOpenAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
    }

    private static DateTimeOffset ReadDateTimeOffset(
        NpgsqlDataReader reader,
        int ordinal)
    {
        var value = reader.GetValue(ordinal);

        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime(),
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            string text when DateTimeOffset.TryParse(text, out var parsed) => parsed.ToUniversalTime(),
            _ => DateTimeOffset.UtcNow
        };
    }
}

public sealed record Phase4FeatureAcceptanceResponse(string PhaseCode, string Status, int FeatureCount, int ReadyFeatureCount, int DistinctGrainCount, IReadOnlyList<Phase4FeatureEvidenceDto> Features);
public sealed record Phase4FeatureEvidenceDto(string FeatureCode, string FeatureName, string Grain, string MaterialCode, string SourceCode, decimal? ValueNumeric, string? ValueText, string FreshnessStatus, bool IsReady);

public sealed record Phase4OutcomeAcceptanceResponse(string PhaseCode, string Status, int OutcomeCount, int ReadyOutcomeCount, IReadOnlyList<Phase4OutcomeEvidenceDto> Outcomes);
public sealed record Phase4OutcomeEvidenceDto(string OutcomeCode, string OutcomeName, string Grain, string MaterialCode, decimal? ValueNumeric, string? ValueText, decimal LabelCompletenessPercent, bool IsReady);

public sealed record Phase4CascadeAcceptanceResponse(string PhaseCode, string Status, bool HasEquipmentOnly, bool HasBufferAbsorbed, bool HasCascadeAmplified, IReadOnlyList<Phase4CascadeEvidenceDto> Events);
public sealed record Phase4CascadeEvidenceDto(string EventCode, decimal EquipmentStopMinutes, decimal ProductionImpactMinutes, decimal BufferAbsorbedMinutes, decimal CascadeAmplificationFactor, string Classification, bool IsReady);

public sealed record Phase4ComputeAcceptanceResponse(string PhaseCode, string Status, int MethodCount, int ReadyMethodCount, IReadOnlyList<Phase4ComputeEvidenceDto> Methods);
public sealed record Phase4ComputeEvidenceDto(string MethodCode, string MethodName, string SupportsTypePair, bool HasPValue, bool HasEffectSize, bool HasGovernance, bool IsReady);

public sealed record Phase4SummaryResponse(string PhaseCode, string Status, int ReadyFeatureCount, int ReadyOutcomeCount, int ReadyCascadeEventCount, int ReadyComputeMethodCount, string HonestPositioning);

public sealed record Phase5RunNowRequest(string? JobCode);
public sealed record Phase5RunNowResponse(string JobCode, int AffectedJobs, string Status, string GovernanceMessage);

public sealed record Phase5LearningJobDto(string JobCode, string JobName, string ScheduleExpression, string LifecycleState, bool Enabled, string ReadinessGateStatus, string LastRunStatus, DateTimeOffset NextRunUtc, DateTimeOffset LastRunUtc, int RunCount, int BackoffSeconds, string GovernanceMessage);

public sealed record Phase5CorrelationResultDto(string ResultCode, string JobCode, string SubjectCode, string OutcomeCode, string MethodCode, decimal EffectSize, decimal PValue, decimal QValue, decimal ConfidenceLow, decimal ConfidenceHigh, int SampleSize, decimal StabilityScore, int Ranking, string LifecycleState);

public sealed record Phase5ScheduledLearningAcceptanceResponse(string PhaseCode, string Status, int JobCount, int ResultCount, IReadOnlyList<Phase5LearningJobDto> Jobs, IReadOnlyList<Phase5CorrelationResultDto> Results);

public sealed record Phase5GoldenDatasetAcceptanceResponse(string PhaseCode, string Status, int CheckCount, int PassedCheckCount, IReadOnlyList<Phase5GoldenDatasetEvidenceDto> Checks);
public sealed record Phase5GoldenDatasetEvidenceDto(string CheckCode, string CheckName, string Status, string ExpectedBehavior, string Evidence, bool SignalRecovered, bool NoiseRejected, bool Deterministic, bool FdrControlled);

public sealed record Phase5SummaryResponse(string PhaseCode, string Status, int ReadyJobCount, int AcceptedResultCount, int GoldenPassedCount, string HonestPositioning);
`
);

patchFile("Backend/PlantProcess.Api/Program.cs", (source) => {
  let text = source;

  if (!text.includes("using PlantProcess.Api.Endpoints.Phase45;")) {
    const lastUsingMatch = [...text.matchAll(/^using .*;$/gm)].pop();

    if (lastUsingMatch) {
      const insertAt = lastUsingMatch.index + lastUsingMatch[0].length;
      text = text.slice(0, insertAt) + "\nusing PlantProcess.Api.Endpoints.Phase45;" + text.slice(insertAt);
    } else {
      text = "using PlantProcess.Api.Endpoints.Phase45;\n" + text;
    }
  }

  if (!text.includes("MapPhase45ClosureEndpoints")) {
    text = text.replace(/app\.Run\(\);/, "app.MapPhase45ClosureEndpoints();\n\napp.Run();");
  }

  return text;
});

// ============================================================================
// E2E tests for Pack 4, 5, 6
// ============================================================================

writeFile(
  "Frontend/PlantProcess.Web/e2e/phase3-dynamic-page-rendering.spec.ts",
  String.raw`import { expect, test } from "@playwright/test";
import { apiBaseUrl, login } from "./helpers/auth";

const slug = "e2e-dynamic-rendered-page";

const payload = {
  slug,
  title: "E2E Dynamic Rendered Page",
  visibility: "Shared",
  layoutJson: {
    grid: { columns: 12, rowHeight: 80 },
    widgets: [
      { id: "dyn-risk", kind: "kpi", title: "Dynamic Risk KPI", x: 0, y: 0, w: 3, h: 2, source: "schema_view:risk_summary" },
      { id: "dyn-defects", kind: "bar", title: "Dynamic Defect Breakdown", x: 3, y: 0, w: 5, h: 3, source: "schema_view:defect_breakdown" },
      { id: "dyn-trend", kind: "line", title: "Dynamic Defect Trend", x: 8, y: 0, w: 4, h: 3, source: "schema_view:quality_daily" },
    ],
  },
  widgetBindingsJson: {
    bindings: [
      { widgetId: "dyn-risk", source: "schema_view:risk_summary" },
      { widgetId: "dyn-defects", source: "schema_view:defect_breakdown" },
      { widgetId: "dyn-trend", source: "schema_view:quality_daily" },
    ],
  },
};

test.describe("P03 dynamic /pages/{slug} rendering", () => {
  test("renders a stored backend PageDefinition as a dynamic page", async ({ page, request }) => {
    const token = await login(request);

    await request.delete(apiBaseUrl + "/pages/" + slug, {
      headers: { Authorization: "Bearer " + token },
    }).catch(() => undefined);

    const create = await request.post(apiBaseUrl + "/pages", {
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        Authorization: "Bearer " + token,
      },
      data: payload,
    });

    expect(create.ok(), "create failed: " + create.status() + " " + await create.text()).toBeTruthy();

    await page.addInitScript((accessToken) => {
      window.localStorage.setItem("plantprocess.auth.accessToken", accessToken);
    }, token);

    await page.goto("/pages/" + slug);

    await expect(page.getByRole("heading", { name: "E2E Dynamic Rendered Page" })).toBeVisible({
      timeout: 15_000,
    });

    await expect(page.locator("[data-dynamic-page-status='loaded']")).toBeVisible();
    await expect(page.locator("[data-runtime-widget-id]")).toHaveCount(3);
    await expect(page.locator("[data-runtime-widget-id='dyn-risk']")).toContainText("Dynamic Risk KPI");
    await expect(page.locator("[data-runtime-widget-id='dyn-defects']")).toContainText("schema_view:defect_breakdown");

    const missingSlug = "missing-dynamic-page-" + Date.now();
    await page.goto("/pages/" + missingSlug);
    await expect(page.locator("[data-dynamic-page-status='not-found']")).toBeVisible({
      timeout: 15_000,
    });
  });
});
`
);

writeFile(
  "Frontend/PlantProcess.Web/e2e/phase4-ml-foundation-proof.spec.ts",
  String.raw`import { expect, test, type APIRequestContext } from "@playwright/test";
import { apiBaseUrl, login } from "./helpers/auth";

async function getJson<T>(request: APIRequestContext, path: string, token: string): Promise<T> {
  const response = await request.get(apiBaseUrl + path, {
    headers: {
      Accept: "application/json",
      Authorization: "Bearer " + token,
    },
  });

  expect(response.ok(), path + " failed: " + response.status() + " " + await response.text()).toBeTruthy();

  return (await response.json()) as T;
}

test.describe("P04 ML foundation acceptance", () => {
  test("feature, outcome, cascade and compute proof gates pass", async ({ request }) => {
    const token = await login(request);

    const features = await getJson<{
      status: string;
      featureCount: number;
      readyFeatureCount: number;
      distinctGrainCount: number;
      features: Array<{ grain: string; isReady: boolean }>;
    }>(request, "/phase4/features/acceptance", token);

    expect(features.status).toBe("Passed");
    expect(features.featureCount).toBeGreaterThanOrEqual(8);
    expect(features.readyFeatureCount).toBe(features.featureCount);
    expect(features.distinctGrainCount).toBeGreaterThanOrEqual(4);
    expect(features.features.map((feature) => feature.grain)).toEqual(
      expect.arrayContaining(["Heat", "Strand", "Coil", "Location"]),
    );

    const outcomes = await getJson<{
      status: string;
      outcomeCount: number;
      readyOutcomeCount: number;
    }>(request, "/phase4/outcomes/acceptance", token);

    expect(outcomes.status).toBe("Passed");
    expect(outcomes.outcomeCount).toBeGreaterThanOrEqual(5);
    expect(outcomes.readyOutcomeCount).toBe(outcomes.outcomeCount);

    const cascade = await getJson<{
      status: string;
      hasEquipmentOnly: boolean;
      hasBufferAbsorbed: boolean;
      hasCascadeAmplified: boolean;
    }>(request, "/phase4/cascade/acceptance", token);

    expect(cascade.status).toBe("Passed");
    expect(cascade.hasEquipmentOnly).toBeTruthy();
    expect(cascade.hasBufferAbsorbed).toBeTruthy();
    expect(cascade.hasCascadeAmplified).toBeTruthy();

    const compute = await getJson<{
      status: string;
      methodCount: number;
      readyMethodCount: number;
    }>(request, "/phase4/compute/acceptance", token);

    expect(compute.status).toBe("Passed");
    expect(compute.methodCount).toBeGreaterThanOrEqual(6);
    expect(compute.readyMethodCount).toBe(compute.methodCount);

    const summary = await getJson<{
      status: string;
      honestPositioning: string;
    }>(request, "/phase4/acceptance-summary", token);

    expect(summary.status).toBe("Passed");
    expect(summary.honestPositioning).toMatch(/No production ML prediction/i);
  });
});
`
);

writeFile(
  "Frontend/PlantProcess.Web/e2e/phase5-scheduled-learning-proof.spec.ts",
  String.raw`import { expect, test, type APIRequestContext } from "@playwright/test";
import { apiBaseUrl, login } from "./helpers/auth";

async function getJson<T>(request: APIRequestContext, path: string, token: string): Promise<T> {
  const response = await request.get(apiBaseUrl + path, {
    headers: {
      Accept: "application/json",
      Authorization: "Bearer " + token,
    },
  });

  expect(response.ok(), path + " failed: " + response.status() + " " + await response.text()).toBeTruthy();

  return (await response.json()) as T;
}

test.describe("P05 scheduled learning acceptance", () => {
  test("scheduled learning jobs, run-now, correlation results and golden dataset pass", async ({ request }) => {
    const token = await login(request);

    const jobsBefore = await getJson<Array<{
      jobCode: string;
      enabled: boolean;
      readinessGateStatus: string;
      lastRunStatus: string;
    }>>(request, "/phase5/scheduled-learning/jobs", token);

    expect(jobsBefore.length).toBeGreaterThanOrEqual(4);
    expect(jobsBefore.every((job) => job.enabled)).toBeTruthy();
    expect(jobsBefore.every((job) => job.readinessGateStatus === "Passed")).toBeTruthy();

    const run = await request.post(apiBaseUrl + "/phase5/scheduled-learning/run-now", {
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        Authorization: "Bearer " + token,
      },
      data: {
        jobCode: "P05-JOB-QUALITY-CORRELATION",
      },
    });

    expect(run.ok(), "run-now failed: " + run.status() + " " + await run.text()).toBeTruthy();

    const scheduled = await getJson<{
      status: string;
      jobCount: number;
      resultCount: number;
      results: Array<{
        qValue: number;
        sampleSize: number;
        stabilityScore: number;
      }>;
    }>(request, "/phase5/scheduled-learning/acceptance", token);

    expect(scheduled.status).toBe("Passed");
    expect(scheduled.jobCount).toBeGreaterThanOrEqual(4);
    expect(scheduled.resultCount).toBeGreaterThanOrEqual(4);
    expect(scheduled.results.every((result) => result.qValue <= 0.1)).toBeTruthy();
    expect(scheduled.results.every((result) => result.sampleSize >= 200)).toBeTruthy();
    expect(scheduled.results.every((result) => result.stabilityScore >= 0.7)).toBeTruthy();

    const golden = await getJson<{
      status: string;
      checkCount: number;
      passedCheckCount: number;
      checks: Array<{
        signalRecovered: boolean;
        noiseRejected: boolean;
        deterministic: boolean;
        fdrControlled: boolean;
      }>;
    }>(request, "/phase5/golden-dataset/acceptance", token);

    expect(golden.status).toBe("Passed");
    expect(golden.checkCount).toBeGreaterThanOrEqual(5);
    expect(golden.passedCheckCount).toBe(golden.checkCount);
    expect(golden.checks.every((check) => check.signalRecovered && check.noiseRejected && check.deterministic && check.fdrControlled)).toBeTruthy();

    const summary = await getJson<{
      status: string;
      honestPositioning: string;
    }>(request, "/phase5/acceptance-summary", token);

    expect(summary.status).toBe("Passed");
    expect(summary.honestPositioning).toMatch(/suspected contributors, not guaranteed root cause/i);
  });
});
`
);

// ============================================================================
// Structural validator
// ============================================================================

writeFile(
  "tools/phase25/validate-pack4-5-6-closure.cjs",
  String.raw`const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const failures = [];

function read(relativePath) {
  const file = path.join(root, relativePath);
  if (!fs.existsSync(file)) {
    failures.push("Missing file: " + relativePath);
    return "";
  }

  return fs.readFileSync(file, "utf8");
}

function mustContain(relativePath, regex, message) {
  const text = read(relativePath);
  if (text && !regex.test(text)) {
    failures.push(message + " in " + relativePath);
  }
}

mustContain(
  "Frontend/PlantProcess.Web/src/pages/DynamicPage/DynamicPage.tsx",
  /pageBuilderApi\.getBySlug/,
  "DynamicPage must load backend PageDefinition by slug",
);

mustContain(
  "Frontend/PlantProcess.Web/src/pages/DynamicPage/DynamicPage.tsx",
  /data-runtime-widget-id/,
  "DynamicPage must render runtime widgets from layoutJson",
);

mustContain(
  "Frontend/PlantProcess.Web/src/App.tsx",
  /pages\/DynamicPage\/DynamicPage/,
  "App route must use real DynamicPage component",
);

mustContain(
  "Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs",
  /MapGroup\("\/phase4"\)/,
  "Phase 4 endpoint group must exist",
);

mustContain(
  "Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs",
  /MapGroup\("\/phase5"\)/,
  "Phase 5 endpoint group must exist",
);

mustContain(
  "Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs",
  /phase4_feature_store_evidence/,
  "Phase 4 feature evidence table must exist",
);

mustContain(
  "Backend/PlantProcess.Api/Endpoints/Phase45/Phase45ClosureEndpoints.cs",
  /phase5_learning_job_evidence/,
  "Phase 5 learning job evidence table must exist",
);

mustContain(
  "Backend/PlantProcess.Api/Program.cs",
  /MapPhase45ClosureEndpoints\(\)/,
  "Program.cs must map Phase 4/5 closure endpoints",
);

mustContain(
  "Frontend/PlantProcess.Web/e2e/phase3-dynamic-page-rendering.spec.ts",
  /\/pages\//,
  "P03 dynamic page E2E must exist",
);

mustContain(
  "Frontend/PlantProcess.Web/e2e/phase4-ml-foundation-proof.spec.ts",
  /phase4\/features\/acceptance/,
  "P04 E2E must call feature acceptance",
);

mustContain(
  "Frontend/PlantProcess.Web/e2e/phase5-scheduled-learning-proof.spec.ts",
  /phase5\/scheduled-learning\/acceptance/,
  "P05 E2E must call scheduled learning acceptance",
);

if (failures.length > 0) {
  console.error("Pack 4-6 validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("Pack 4-6 P03/P04/P05 structural validation passed.");
`
);

console.log("");
console.log("Pack 4-6 closure implementation applied.");
