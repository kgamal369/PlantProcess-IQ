

// ============================================================
// FILE: Frontend/PlantProcess.Web/src/pages/Admin/AdminSchemaConfigurationTab.tsx
//
// HIGH PRIORITY ITEMS 11 + 12 + 13:
//  11. Schema Configuration visual mapper — map source fields to canonical targets
//  12. SQL View editor — full-featured textarea with syntax hints, save & preview
//  13. KPI Definition UI — create/edit KPIs with SQL expressions
//
// Replaces the previous read-only version.
// ============================================================

import { useEffect, useState } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  ChevronRight,
  FileCode2,
  FileJson,
  Gauge,
  Loader2,
  PlayCircle,
  Plus,
  Save,
  TableProperties,
} from "lucide-react";

import {
  plantProcessApi,
  type CreateKpiDefinitionRequest,
  type CreateSchemaViewDefinitionRequest,
  type KpiDefinitionRecord,
  type SchemaConfigurationSummary,
  type SchemaViewDefinitionRecord,
  type SchemaViewPreviewResult,
} from "@/api/plantProcessApi";
import { AdminPanel, MiniKpi, StatusPill, formatDate } from "./AdminSharedComponents";
import { useOptimisticSave } from "@/hooks/useOptimisticSave";
import { InlineFieldError } from "@/components/forms/InlineFieldError";
import {
  useInlineFormValidation,
  validateCode,
  validateIntervalMinutes,
  validateRequired,
} from "@/hooks/useInlineFormValidation";

// ── SchemaConfigurationTab ────────────────────────────────────────────────────

export function SchemaConfigurationTab({
  data,
}: {
  data: SchemaConfigurationSummary | null;
}) {
  const coverage = data?.sourceObjects ?? [];
  const mappings = data?.mappings ?? [];

  return (
    <section className="admin-panel-grid">

      {/* Item 12: SQL View Editor */}
      <SqlViewEditorPanel />

      {/* Item 13: KPI Definition UI */}
      <KpiDefinitionPanel />

      {/* Stats summary */}
      <AdminPanel
        title="Schema Configuration Summary"
        subtitle="Source objects, mappings and canonical targets"
        icon={<TableProperties size={18} />}
        wide
      >
        <p className="admin-copy">
          {data?.message ?? "Schema Configuration summary is not available yet."}
        </p>

        <div className="admin-kpi-row">
          <MiniKpi label="Mappings" value={data?.mappingCount ?? 0} />
          <MiniKpi label="Active Mappings" value={data?.activeMappingCount ?? 0} />
          <MiniKpi label="Source Objects" value={coverage.length} />
          <MiniKpi label="Target Entities" value={data?.targetCoverage.length ?? 0} />
        </div>
      </AdminPanel>

      {/* Item 11: Visual field mapper */}
      <FieldMapperPanel mappings={mappings} coverage={coverage} />

      {/* Source coverage */}
      {coverage.length > 0 ? (
        <AdminPanel
          title="Source Object Coverage"
          subtitle="Raw/staging rows per source object"
          icon={<FileJson size={18} />}
        >
          <div className="admin-list">
            {coverage.map((item) => (
              <div className="admin-list-item" key={item.sourceObjectName}>
                <div>
                  <strong>{item.sourceObjectName}</strong>
                  <span>
                    {item.mappedRows} mapped / {item.pendingRows} pending / {item.failedRows} failed
                  </span>
                </div>
                <b>{item.totalRows}</b>
              </div>
            ))}
          </div>
        </AdminPanel>
      ) : null}

    </section>
  );
}

// ── Item 12: SQL View Editor Panel ────────────────────────────────────────────

function SqlViewEditorPanel() {
  const [schemaViews, setSchemaViews] = useState<SchemaViewDefinitionRecord[]>([]);
  const [selectedViewId, setSelectedViewId] = useState("");
  const [preview, setPreview] = useState<SchemaViewPreviewResult | null>(null);
  // isBusy is now ONLY used for non-save async work (load + previewSql).
  // Save and approve are owned by useOptimisticSave below.
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [form, setForm] = useState({
    schemaViewCode: "",
    schemaViewName: "",
    viewKind: "SqlView",
    sqlText: `-- Write a safe SELECT or WITH query.
-- Joins between staging tables are supported.
-- Example:
SELECT
    sr.id,
    sr.source_object_name,
    sr.row_number,
    sr.processing_status,
    sr.raw_json
FROM staging_records sr
WHERE sr.is_deleted = false
ORDER BY sr.row_number
LIMIT 100`,
    maxPreviewRows: 50,
    timeoutSeconds: 15,
    description: "",
  });

  function setField(key: string, value: unknown) {
    setForm((f) => ({ ...f, [key]: value }));
  }

  type SqlViewField =
    | "schemaViewCode"
    | "schemaViewName"
    | "sqlText"
    | "maxPreviewRows"
    | "timeoutSeconds";

  const validation = useInlineFormValidation<typeof form, SqlViewField>(
    form,
    (value) => ({
      schemaViewCode: validateCode(value.schemaViewCode, "View code"),
      schemaViewName: validateRequired(value.schemaViewName, "View name"),
      sqlText: validateRequired(value.sqlText, "SQL query"),
      maxPreviewRows: validateIntervalMinutes(value.maxPreviewRows, 1, "Max preview rows"),
      timeoutSeconds: validateIntervalMinutes(value.timeoutSeconds, 1, "Timeout seconds"),
    })
  );
  
  async function load() {
    setIsBusy(true);
    try {
      const views = await plantProcessApi.getSchemaViews(true);
      setSchemaViews(views);
      if (!selectedViewId && views.length > 0) setSelectedViewId(views[0].id);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Load failed.");
    } finally {
      setIsBusy(false);
    }
  }

  useEffect(() => { load(); }, []);

  function loadSelectedViewIntoEditor(viewId: string) {
    const view = schemaViews.find((v) => v.id === viewId);
    if (!view) return;
    setSelectedViewId(viewId);
    setForm((f) => ({
      ...f,
      schemaViewCode: view.schemaViewCode,
      schemaViewName: view.schemaViewName,
      viewKind: view.viewKind,
      sqlText: view.sqlText,
      maxPreviewRows: view.maxPreviewRows,
      timeoutSeconds: view.timeoutSeconds,
      description: view.description ?? "",
    }));
    setPreview(null);
  }

  async function previewSql() {
    setIsBusy(true);
    setError(null);
    setPreview(null);
    if (!validation.prepareSubmit()) {
      setError("Please fix the highlighted fields before previewing SQL.");
      return;
    }
    try {
      const result = await plantProcessApi.previewAdHocSchemaSql({
        sqlText: form.sqlText,
        maxRows: form.maxPreviewRows,
        timeoutSeconds: form.timeoutSeconds,
      });
      setPreview(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Preview failed.");
    } finally {
      setIsBusy(false);
    }
  }

  // ── FE-HARD-005: Optimistic save for saveView ──────────────────────────────
  // The hook computes a fresh successMessage on every render via the closure,
  // so it always reflects the current form.schemaViewName.
  const { isSaving: isSavingView, save: saveView, error: saveError } = useOptimisticSave({
    successMessage: selectedViewId
      ? `Schema view "${form.schemaViewName}" updated`
      : `Schema view "${form.schemaViewName}" created`,
    toastId: `save-schema-view-${selectedViewId || "new"}`,
    onSave: async () => {
      if (!form.schemaViewCode.trim() || !form.schemaViewName.trim()) {
        throw new Error("View Code and View Name are required to save.");
      }

      const existing = schemaViews.find((v) => v.id === selectedViewId);
      if (existing) {
        await plantProcessApi.updateSchemaView(existing.id, {
          schemaViewName: form.schemaViewName,
          viewKind: form.viewKind,
          primarySourceDatasetDefinitionId: null,
          sqlText: form.sqlText,
          sourceDatasetIdsJson: "[]",
          maxPreviewRows: form.maxPreviewRows,
          timeoutSeconds: form.timeoutSeconds,
          description: form.description,
        });
      } else {
        const request: CreateSchemaViewDefinitionRequest = {
          schemaViewCode: form.schemaViewCode,
          schemaViewName: form.schemaViewName,
          viewKind: form.viewKind,
          sqlText: form.sqlText,
          sourceDatasetIdsJson: "[]",
          maxPreviewRows: form.maxPreviewRows,
          timeoutSeconds: form.timeoutSeconds,
          description: form.description,
          isSynthetic: false,
          sourceSystem: "PlantProcessIQ.Admin",
          sourceRecordId: null,
        };
        const created = await plantProcessApi.createSchemaView(request);
        setSelectedViewId(created.id);
      }
    },
    onSuccess: async () => {
      await load();
    },
  });

  // ── FE-HARD-005: Optimistic save for approveView ───────────────────────────
  const { isSaving: isApproving, save: approveView, error: approveError } = useOptimisticSave({
    successMessage: "View approved for use in canonical mappings",
    toastId: `approve-schema-view-${selectedViewId || "none"}`,
    onSave: async () => {
      if (!selectedViewId) {
        throw new Error("Select a view first.");
      }
      await plantProcessApi.approveSchemaView(selectedViewId);
    },
    onSuccess: async () => {
      await load();
    },
  });

  const selectedView = schemaViews.find((v) => v.id === selectedViewId);

  return (
    <AdminPanel
      title="SQL View Editor"
      subtitle="Write and manage controlled SQL views and JOINs over staging data"
      icon={<FileCode2 size={18} />}
      wide
    >
      <p className="admin-copy">
        Views are safe SELECT/WITH queries over raw staging records. Use them to
        normalise column names, JOIN tables from different sources, and prepare data
        for canonical mapping. Only read operations are permitted.
      </p>

      {error ? <div className="admin-inline-error">{error}</div> : null}
      {saveError instanceof Error ? <div className="admin-inline-error">{saveError.message}</div> : null}
      {approveError instanceof Error ? <div className="admin-inline-error">{approveError.message}</div> : null}

      {/* Existing views selector */}
      {schemaViews.length > 0 ? (
        <div className="admin-form-row" style={{ marginBottom: "1rem" }}>
          <label className="admin-form-label">Load existing view</label>
          <div className="admin-form-inline">
            <select
              className="admin-select"
              value={selectedViewId}
              onChange={(e) => loadSelectedViewIntoEditor(e.target.value)}
            >
              <option value="">New view…</option>
              {schemaViews.map((v) => (
                <option key={v.id} value={v.id}>
                  {v.schemaViewCode} — {v.schemaViewName}
                  {v.isApproved ? " ✅" : ""}
                </option>
              ))}
            </select>
            {selectedView ? (
              <span className="admin-copy" style={{ fontSize: 12 }}>
                Status: {selectedView.lastValidationStatus ?? "Not run"} ·
                Approved: {selectedView.isApproved ? "Yes" : "No"} ·
                Last: {formatDate(selectedView.lastValidatedAtUtc)}
              </span>
            ) : null}
          </div>
        </div>
      ) : null}

      {/* Form metadata */}
      <div className="admin-form-grid" style={{ marginBottom: "1rem" }}>
        <label className="admin-form-label">
          View Code *
          <input
            className="admin-input"
            value={form.schemaViewCode}
            onChange={(e) => setField("schemaViewCode", e.target.value)}
            placeholder="e.g. DEFECT_JOIN_MATERIAL_VIEW"
            disabled={!!selectedView}
          />
        </label>
        <label className="admin-form-label">
          View Name *
          <input
            className="admin-input"
            value={form.schemaViewName}
            onChange={(e) => setField("schemaViewName", e.target.value)}
            placeholder="e.g. Defect + Material JOIN"
          />
        </label>
        <label className="admin-form-label">
          View Kind
          <select
            className="admin-select"
            value={form.viewKind}
            onChange={(e) => setField("viewKind", e.target.value)}
          >
            <option value="SqlView">SQL View — generic SELECT</option>
            <option value="JoinView">Join View — multi-table JOIN</option>
            <option value="KpiView">KPI View — aggregation for KPI</option>
            <option value="MappingPreparationView">Mapping Prep — pre-canonical transform</option>
          </select>
        </label>
        <label className="admin-form-label">
          Max Preview Rows
          <select
            className="admin-select admin-select--narrow"
            value={form.maxPreviewRows}
            onChange={(e) => setField("maxPreviewRows", Number(e.target.value))}
          >
            {[10, 25, 50, 100, 200].map((v) => (
              <option key={v} value={v}>{v} rows</option>
            ))}
          </select>
        </label>
      </div>

      {/* SQL Editor */}
      <label className="admin-form-label">
        SQL Query
        <div className="admin-sql-editor-wrapper">
          <div className="admin-sql-editor-hint">
            Supported: SELECT, WITH (CTE), JOINs on staging_records. Blocked: INSERT, UPDATE, DELETE, DROP, TRUNCATE.
          </div>
          <textarea
            className="admin-sql-editor admin-sql-editor--large"
            value={form.sqlText}
            onChange={(e) => setField("sqlText", e.target.value)}
            spellCheck={false}
            rows={12}
          />
        </div>
      </label>

      {/* Description */}
      <label className="admin-form-label">
        Description
        <input
          className="admin-input"
          value={form.description}
          onChange={(e) => setField("description", e.target.value)}
          placeholder="Describe what this view does and how it will be used"
        />
      </label>

      {/* Actions */}
      <div className="admin-action-row" style={{ marginTop: "1rem", flexWrap: "wrap" }}>
        <button
          className="secondary-button"
          type="button"
          onClick={previewSql}
          disabled={isBusy || isSavingView || isApproving || !form.sqlText.trim()}
        >
          {isBusy ? <Loader2 size={14} className="spin" /> : <PlayCircle size={14} />}
          Preview SQL
        </button>
        <button
          className="primary-button"
          type="button"
          onClick={saveView}
          disabled={isSavingView || isBusy || isApproving || !form.schemaViewCode.trim()}
        >
          {isSavingView
            ? <><Loader2 size={14} className="spin" /> Saving…</>
            : <><Save size={14} /> {selectedView ? "Update View" : "Save View"}</>}
        </button>
        {selectedView && !selectedView.isApproved ? (
          <button
            className="secondary-button"
            type="button"
            onClick={approveView}
            disabled={isApproving || isBusy || isSavingView}
          >
            {isApproving
              ? <><Loader2 size={14} className="spin" /> Approving…</>
              : <><CheckCircle2 size={14} /> Approve for Mapping</>}
          </button>
        ) : null}
      </div>

      {/* Preview result */}
      {preview ? (
        <div className="admin-preview-panel" style={{ marginTop: "1.5rem" }}>
          <div className="admin-panel__header">
            <div className="admin-panel__icon">
              <PlayCircle size={18} />
            </div>
            <div>
              <h2>Preview Result</h2>
              <p>
                {preview.isSuccess ? "✅" : "❌"} {preview.message} ·
                {preview.durationMs}ms · {preview.rowCount} rows
              </p>
            </div>
          </div>

          {preview.columns.length > 0 ? (
            <div className="admin-table-wrap" style={{ maxHeight: 300, overflowY: "auto" }}>
              <table>
                <thead>
                  <tr>
                    {preview.columns.map((c) => (
                      <th key={c.columnName}>
                        {c.columnName}
                        <small>{c.dataType}</small>
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {preview.rows.map((row, i) => (
                    <tr key={i}>
                      {preview.columns.map((c) => (
                        <td key={c.columnName}>
                          {String(row[c.columnName] ?? "—")}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </div>
      ) : null}

      {/* All saved views table */}
      {schemaViews.length > 0 ? (
        <div className="admin-table-wrap" style={{ marginTop: "1.5rem" }}>
          <strong style={{ fontSize: 12, color: "var(--ppiq-text-muted)", display: "block", marginBottom: 8 }}>
            All Schema Views ({schemaViews.length})
          </strong>
          <table>
            <thead>
              <tr>
                <th>View</th><th>Kind</th><th>Validation</th><th>Approved</th><th>Max Rows</th><th>Last Run</th>
              </tr>
            </thead>
            <tbody>
              {schemaViews.map((v) => (
                <tr
                  key={v.id}
                  className={selectedViewId === v.id ? "selected-row" : ""}
                  style={{ cursor: "pointer" }}
                  onClick={() => loadSelectedViewIntoEditor(v.id)}
                >
                  <td>
                    <strong>{v.schemaViewCode}</strong>
                    <small>{v.schemaViewName}</small>
                  </td>
                  <td>{v.viewKind}</td>
                  <td>
                    <StatusPill
                      status={v.lastValidationStatus ?? "Not run"}
                      statusClass={v.lastValidationStatus === "Success" ? "success" : "warning"}
                    />
                  </td>
                  <td>{v.isApproved ? "✅ Yes" : "—"}</td>
                  <td>{v.maxPreviewRows}</td>
                  <td>{formatDate(v.lastValidatedAtUtc)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </AdminPanel>
  );
}

// ── Item 13: KPI Definition Panel ─────────────────────────────────────────────

function KpiDefinitionPanel() {
  const [kpis, setKpis] = useState<KpiDefinitionRecord[]>([]);
  const [schemaViews, setSchemaViews] = useState<SchemaViewDefinitionRecord[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);

  const [form, setForm] = useState({
    kpiCode: "",
    kpiName: "",
    kpiCategory: "Quality",
    valueExpression: "COUNT(*)",
    dimensionExpression: "",
    filterExpression: "",
    aggregationType: "Count",
    unit: "",
    description: "",
    schemaViewDefinitionId: "",
  });

  function setField(key: string, value: string) {
    setForm((f) => ({ ...f, [key]: value }));
  }

  async function load() {
    try {
      const [kpiList, views] = await Promise.all([
        plantProcessApi.getKpiDefinitions(true),
        plantProcessApi.getSchemaViews(true),
      ]);
      setKpis(kpiList);
      setSchemaViews(views.filter((v) => v.isApproved));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load KPIs.");
    }
  }

  useEffect(() => { load(); }, []);

  // ── FE-HARD-005: Optimistic save for createKpi ─────────────────────────────
  const { isSaving: isCreatingKpi, save: createKpi, error: createKpiError } = useOptimisticSave({
    successMessage: `KPI "${form.kpiName}" created`,
    toastId: `create-kpi-${form.kpiCode || "new"}`,
    onSave: async () => {
      if (!form.kpiCode.trim() || !form.kpiName.trim()) {
        throw new Error("KPI Code and Name are required.");
      }

      const request: CreateKpiDefinitionRequest = {
        schemaViewDefinitionId: form.schemaViewDefinitionId || null,
        kpiCode: form.kpiCode.toUpperCase().replace(/[^A-Z0-9_]/g, "_"),
        kpiName: form.kpiName,
        kpiCategory: form.kpiCategory,
        valueExpression: form.valueExpression,
        unit: form.unit || null,
        dimensionExpression: form.dimensionExpression || null,
        filterExpression: form.filterExpression || null,
        aggregationType: form.aggregationType,
        kpiOptionsJson: "{}",
        description: form.description || null,
        isSynthetic: false,
        sourceSystem: "PlantProcessIQ.Admin",
        sourceRecordId: null,
      };
      await plantProcessApi.createKpiDefinition(request);
    },
    onSuccess: async () => {
      setShowForm(false);
      setForm((f) => ({ ...f, kpiCode: "", kpiName: "", valueExpression: "COUNT(*)", description: "" }));
      await load();
    },
  });

  return (
    <AdminPanel
      title="KPI Definitions"
      subtitle="Define SQL-expression KPIs linked to schema views and equipment/areas"
      icon={<Gauge size={18} />}
      wide
    >
      <p className="admin-copy">
        KPIs are named metrics computed from a SQL value expression against schema view data.
        They appear as dimensions in the Dashboard Widget Builder and feed into the ML correlation engine.
      </p>

      {error ? <div className="admin-inline-error">{error}</div> : null}
      {createKpiError instanceof Error ? <div className="admin-inline-error">{createKpiError.message}</div> : null}

      <div className="admin-action-row" style={{ marginBottom: "1rem" }}>
        <button
          className="primary-button"
          type="button"
          onClick={() => { setShowForm(!showForm); setError(null); }}
        >
          <Plus size={14} /> {showForm ? "Cancel" : "New KPI"}
        </button>
      </div>

      {/* Create form */}
      {showForm ? (
        <div className="admin-form-card" style={{ marginBottom: "1.5rem" }}>
          <h3>New KPI Definition</h3>
          <div className="admin-form-grid">

            <label className="admin-form-label">
              KPI Code *
              <input
                className="admin-input"
                value={form.kpiCode}
                onChange={(e) => setField("kpiCode", e.target.value)}
                placeholder="e.g. DEFECT_RATE_PER_HEAT"
              />
            </label>

            <label className="admin-form-label">
              KPI Name *
              <input
                className="admin-input"
                value={form.kpiName}
                onChange={(e) => setField("kpiName", e.target.value)}
                placeholder="e.g. Defect Rate per Heat"
              />
            </label>

            <label className="admin-form-label">
              Category
              <select
                className="admin-select"
                value={form.kpiCategory}
                onChange={(e) => setField("kpiCategory", e.target.value)}
              >
                {["Quality", "Production", "Process", "Downtime", "Energy", "Maintenance", "Safety"].map((c) => (
                  <option key={c} value={c}>{c}</option>
                ))}
              </select>
            </label>

            <label className="admin-form-label">
              Aggregation Type
              <select
                className="admin-select"
                value={form.aggregationType}
                onChange={(e) => setField("aggregationType", e.target.value)}
              >
                {["Count", "Sum", "Average", "Min", "Max", "Rate", "Ratio", "Custom"].map((a) => (
                  <option key={a} value={a}>{a}</option>
                ))}
              </select>
            </label>

            <label className="admin-form-label">
              Value Expression *
              <input
                className="admin-input admin-input--mono"
                value={form.valueExpression}
                onChange={(e) => setField("valueExpression", e.target.value)}
                placeholder="e.g. COUNT(*) or SUM(defect_count) / COUNT(heat_id)"
              />
              <small className="admin-form-hint">SQL expression that produces the KPI numeric value.</small>
            </label>

            <label className="admin-form-label">
              Dimension Expression
              <input
                className="admin-input admin-input--mono"
                value={form.dimensionExpression}
                onChange={(e) => setField("dimensionExpression", e.target.value)}
                placeholder="e.g. equipment_code or DATE_TRUNC('day', created_at)"
              />
              <small className="admin-form-hint">Optional GROUP BY expression for time/equipment breakdowns.</small>
            </label>

            <label className="admin-form-label">
              Filter Expression
              <input
                className="admin-input admin-input--mono"
                value={form.filterExpression}
                onChange={(e) => setField("filterExpression", e.target.value)}
                placeholder="e.g. defect_type = 'SurfaceCrack'"
              />
              <small className="admin-form-hint">Optional WHERE clause fragment to scope the KPI.</small>
            </label>

            <label className="admin-form-label">
              Unit
              <input
                className="admin-input admin-input--narrow"
                value={form.unit}
                onChange={(e) => setField("unit", e.target.value)}
                placeholder="e.g. % or ppm"
              />
            </label>

            <label className="admin-form-label">
              Source Schema View (optional)
              <select
                className="admin-select"
                value={form.schemaViewDefinitionId}
                onChange={(e) => setField("schemaViewDefinitionId", e.target.value)}
              >
                <option value="">None — use raw staging directly</option>
                {schemaViews.map((v) => (
                  <option key={v.id} value={v.id}>
                    {v.schemaViewCode} — {v.schemaViewName}
                  </option>
                ))}
              </select>
              {schemaViews.length === 0 ? (
                <small className="admin-form-hint">
                  <AlertTriangle size={11} /> No approved views yet. Approve a SQL View above first.
                </small>
              ) : null}
            </label>

            <label className="admin-form-label" style={{ gridColumn: "1 / -1" }}>
              Description
              <input
                className="admin-input"
                value={form.description}
                onChange={(e) => setField("description", e.target.value)}
                placeholder="What does this KPI measure and why is it important?"
              />
            </label>
          </div>

          <div className="admin-form-actions">
            <button
              className="primary-button"
              type="button"
              onClick={createKpi}
              disabled={isCreatingKpi}
            >
              {isCreatingKpi
                ? <><Loader2 size={14} className="spin" /> Creating…</>
                : <><Save size={14} /> Create KPI</>}
            </button>
          </div>
        </div>
      ) : null}

      {/* KPI list */}
      {kpis.length > 0 ? (
        <div className="admin-table-wrap">
          <table>
            <thead>
              <tr>
                <th>KPI</th><th>Category</th><th>Expression</th><th>Unit</th><th>Aggregation</th><th>Status</th>
              </tr>
            </thead>
            <tbody>
              {kpis.map((kpi) => (
                <tr key={kpi.id}>
                  <td>
                    <strong>{kpi.kpiCode}</strong>
                    <small>{kpi.kpiName}</small>
                  </td>
                  <td>{kpi.kpiCategory}</td>
                  <td>
                    <code style={{ fontSize: 11 }}>{kpi.valueExpression}</code>
                  </td>
                  <td>{kpi.unit ?? "—"}</td>
                  <td>{kpi.aggregationType}</td>
                  <td>
                    <StatusPill
                      status={kpi.isActive ? "Active" : "Inactive"}
                      statusClass={kpi.isActive ? "success" : "neutral"}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="empty-insight">
          <Gauge size={20} />
          <strong>No KPIs defined yet</strong>
          <p>Click "New KPI" to define your first metric.</p>
        </div>
      )}
    </AdminPanel>
  );
}

// ── Item 11: Visual Field Mapper Panel ────────────────────────────────────────

function FieldMapperPanel({
  mappings, coverage,
}: {
  mappings: import("@/api/plantProcessApi").SchemaMappingSummary[];
  coverage: import("@/api/plantProcessApi").SourceObjectCoverage[];
}) {
  const [expandedMapping, setExpandedMapping] = useState<string | null>(null);

  // Canonical target entities that PlantProcess IQ supports
  const CANONICAL_TARGETS = [
    { entity: "MaterialUnit", fields: ["MaterialCode", "MaterialUnitType", "ProductFamily", "GradeOrRecipe", "ProductionStartUtc", "ProductionEndUtc"] },
    { entity: "ProcessEvent", fields: ["MaterialUnitId", "EquipmentId", "EventCode", "StartedAtUtc", "CompletedAtUtc"] },
    { entity: "ParameterObservation", fields: ["ProcessEventId", "ParameterCode", "NumericValue", "TextValue", "ObservedAtUtc"] },
    { entity: "QualityEvent", fields: ["MaterialUnitId", "DefectCode", "Severity", "InspectedAtUtc", "InspectionDevice"] },
    { entity: "DowntimeEvent", fields: ["EquipmentId", "StartedAtUtc", "DurationMinutes", "DowntimeCode", "Reason"] },
  ];

  return (
    <AdminPanel
      title="Schema Field Mapper"
      subtitle="Map source fields to canonical PlantProcess IQ entities"
      icon={<ChevronRight size={18} />}
      wide
    >
      <p className="admin-copy">
        For each staging source object, define which source fields map to which canonical
        entity fields. These mappings drive the canonical refresh jobs.
      </p>

      {/* Canonical target reference */}
      <div style={{ marginBottom: "1.5rem" }}>
        <strong style={{ fontSize: 13, display: "block", marginBottom: 8 }}>
          Canonical Target Entities
        </strong>
        <div className="admin-provider-grid">
          {CANONICAL_TARGETS.map((target) => (
            <div key={target.entity} className="admin-provider-card">
              <div className="admin-provider-card__head">
                <strong>{target.entity}</strong>
              </div>
              <div style={{ display: "flex", flexWrap: "wrap", gap: 4, marginTop: 6 }}>
                {target.fields.map((f) => (
                  <code key={f} style={{ fontSize: 10, background: "var(--ppiq-surface-2)", padding: "1px 5px", borderRadius: 3 }}>
                    {f}
                  </code>
                ))}
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Existing mappings */}
      {mappings.length > 0 ? (
        <div className="admin-table-wrap">
          <strong style={{ fontSize: 12, color: "var(--ppiq-text-muted)", display: "block", marginBottom: 8 }}>
            Configured Mappings ({mappings.length})
          </strong>
          <table>
            <thead>
              <tr>
                <th>Mapping</th><th>Source Object</th><th>→ Target Entity</th><th>Version</th><th>Status</th>
              </tr>
            </thead>
            <tbody>
              {mappings.map((m) => (
                <tr
                  key={m.id}
                  style={{ cursor: "pointer" }}
                  onClick={() => setExpandedMapping(expandedMapping === m.id ? null : m.id)}
                >
                  <td>
                    <strong>{m.mappingCode}</strong>
                    <small>{m.mappingName}</small>
                  </td>
                  <td>{m.sourceObjectName}</td>
                  <td>→ {m.targetEntityName}</td>
                  <td>{m.mappingVersion}</td>
                  <td>
                    <StatusPill
                      status={m.isActive ? "Active" : "Inactive"}
                      statusClass={m.isActive ? "success" : "neutral"}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      {/* Source object coverage — starting points for mapping */}
      {coverage.length > 0 ? (
        <div style={{ marginTop: "1rem" }}>
          <strong style={{ fontSize: 12, color: "var(--ppiq-text-muted)", display: "block", marginBottom: 8 }}>
            Source Objects available for mapping ({coverage.length})
          </strong>
          <div className="admin-list">
            {coverage.map((item) => (
              <div className="admin-list-item" key={item.sourceObjectName}>
                <div>
                  <strong>{item.sourceObjectName}</strong>
                  <span>{item.totalRows} rows — {item.pendingRows} pending</span>
                </div>
                <span className="admin-pill info">Stageable</span>
              </div>
            ))}
          </div>
        </div>
      ) : (
        <div className="empty-insight">
          <strong>No source objects staged yet</strong>
          <p>Import data via the DB Configuration tab to populate source objects for mapping.</p>
        </div>
      )}

      <p className="admin-copy" style={{ marginTop: "1rem", fontSize: 12 }}>
        Full drag-and-drop field-level mapping UI is in Phase 5. Use the Integration API
        (<code>/admin/schema-configuration/mappings</code>) to create MappingDefinition records now.
      </p>
    </AdminPanel>
  );
}