// ============================================================
// FILE: Frontend/PlantProcess.Web/src/pages/Admin/AdminSchemaConfigurationTab.tsx
//
// Extracted from monolithic AdminPageContent.tsx.
// Contains: SchemaConfigurationTab + SchemaViewBuilderPanel
// ============================================================

import { useEffect, useState } from "react";
import {
  FileJson,
  Link2,
  PlayCircle,
  TableProperties,
} from "lucide-react";

import {
  plantProcessApi,
  type KpiDefinitionRecord,
  type SchemaConfigurationSummary,
  type SchemaViewDefinitionRecord,
  type SchemaViewPreviewResult,
} from "@/api/plantProcessApi";
import { AdminPanel, MiniKpi, StatusPill, formatDate } from "./AdminSharedComponents";

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
      <SchemaViewBuilderPanel />

      <AdminPanel
        title="Schema Configuration"
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

      <AdminPanel
        title="Source Object Coverage"
        subtitle="Current raw/staging source-object distribution"
        icon={<FileJson size={18} />}
      >
        <div className="admin-list">
          {coverage.map((item) => (
            <div className="admin-list-item" key={item.sourceObjectName}>
              <div>
                <strong>{item.sourceObjectName}</strong>
                <span>
                  {item.mappedRows} mapped / {item.pendingRows} pending /{" "}
                  {item.failedRows} failed
                </span>
              </div>
              <b>{item.totalRows}</b>
            </div>
          ))}

          {coverage.length === 0 ? (
            <div className="empty-insight">
              <strong>No staging source objects found yet.</strong>
            </div>
          ) : null}
        </div>
      </AdminPanel>

      <AdminPanel
        title="Mapping Definitions"
        subtitle="Existing raw-to-canonical mapping metadata"
        icon={<Link2 size={18} />}
        wide
      >
        <div className="admin-table-wrap">
          <table>
            <thead>
              <tr>
                <th>Mapping</th>
                <th>Source Object</th>
                <th>Target Entity</th>
                <th>Version</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {mappings.map((mapping) => (
                <tr key={mapping.id}>
                  <td>
                    <strong>{mapping.mappingCode}</strong>
                    <small>{mapping.mappingName}</small>
                  </td>
                  <td>{mapping.sourceObjectName}</td>
                  <td>{mapping.targetEntityName}</td>
                  <td>{mapping.mappingVersion}</td>
                  <td>
                    <StatusPill
                      status={mapping.isActive ? "Active" : "Inactive"}
                      statusClass={mapping.isActive ? "success" : "neutral"}
                    />
                  </td>
                </tr>
              ))}

              {mappings.length === 0 ? (
                <tr>
                  <td colSpan={5}>
                    No mapping definitions found. Phase 4 will add the visual
                    schema configuration and SQL/view layer.
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      </AdminPanel>
    </section>
  );
}

// ── SchemaViewBuilderPanel ────────────────────────────────────────────────────

function SchemaViewBuilderPanel() {
  const [schemaViews, setSchemaViews] = useState<SchemaViewDefinitionRecord[]>([]);
  const [kpis, setKpis] = useState<KpiDefinitionRecord[]>([]);
  const [selectedViewId, setSelectedViewId] = useState("");
  const [preview, setPreview] = useState<SchemaViewPreviewResult | null>(null);
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [viewForm, setViewForm] = useState({
    schemaViewCode: "CSV_STAGING_PREVIEW_VIEW",
    schemaViewName: "CSV Staging Preview View",
    viewKind: "SqlView",
    sqlText:
      "select sr.id, sr.source_object_name, sr.row_number, sr.processing_status, sr.raw_json\n" +
      "from staging_records sr\n" +
      "where sr.is_deleted = false\n" +
      "order by sr.row_number",
    maxPreviewRows: 50,
    timeoutSeconds: 15,
    description: "Controlled SQL view over raw staging records.",
  });

  const [kpiForm, setKpiForm] = useState({
    kpiCode: "STAGING_ROW_COUNT",
    kpiName: "Staging Row Count",
    kpiCategory: "Production",
    valueExpression: "count(*)",
    unit: "rows",
    dimensionExpression: "source_object_name",
    aggregationType: "Count",
  });

  async function loadSchemaConfig() {
    setIsBusy(true);
    setError(null);
    try {
      const [views, kpiRows] = await Promise.all([
        plantProcessApi.getSchemaViews(true),
        plantProcessApi.getKpiDefinitions(true),
      ]);
      setSchemaViews(views);
      setKpis(kpiRows);
      if (!selectedViewId && views.length > 0) {
        setSelectedViewId(views[0].id);
      }
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "Failed to load schema config.");
    } finally {
      setIsBusy(false);
    }
  }

  useEffect(() => { loadSchemaConfig(); }, []);

  async function createSchemaView() {
    setIsBusy(true);
    setError(null);
    setPreview(null);
    try {
      const created = await plantProcessApi.createSchemaView({
        schemaViewCode: viewForm.schemaViewCode,
        schemaViewName: viewForm.schemaViewName,
        viewKind: viewForm.viewKind,
        sqlText: viewForm.sqlText,
        sourceDatasetIdsJson: "[]",
        maxPreviewRows: viewForm.maxPreviewRows,
        timeoutSeconds: viewForm.timeoutSeconds,
        description: viewForm.description,
        isSynthetic: true,
        sourceSystem: "PlantProcessIQ.Admin",
        sourceRecordId: "PHASE4-SCHEMA-VIEW",
      });
      setSelectedViewId(created.id);
      await loadSchemaConfig();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create schema view.");
    } finally {
      setIsBusy(false);
    }
  }

  async function previewAdHocSql() {
    setIsBusy(true);
    setError(null);
    setPreview(null);
    try {
      const result = await plantProcessApi.previewAdHocSchemaSql({
        sqlText: viewForm.sqlText,
        maxRows: viewForm.maxPreviewRows,
        timeoutSeconds: viewForm.timeoutSeconds,
      });
      setPreview(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Preview failed.");
    } finally {
      setIsBusy(false);
    }
  }

  async function previewSelectedView() {
    if (!selectedViewId) { setError("Select a schema view first."); return; }
    setIsBusy(true);
    setError(null);
    setPreview(null);
    try {
      const result = await plantProcessApi.previewSchemaView(selectedViewId, {
        maxRows: viewForm.maxPreviewRows,
        timeoutSeconds: viewForm.timeoutSeconds,
      });
      setPreview(result);
      await loadSchemaConfig();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Preview failed.");
    } finally {
      setIsBusy(false);
    }
  }

  async function approveSelectedView() {
    if (!selectedViewId) { setError("Select a schema view first."); return; }
    setIsBusy(true);
    setError(null);
    try {
      await plantProcessApi.approveSchemaView(selectedViewId);
      await loadSchemaConfig();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Approval failed.");
    } finally {
      setIsBusy(false);
    }
  }

  async function createKpi() {
    setIsBusy(true);
    setError(null);
    try {
      await plantProcessApi.createKpiDefinition({
        schemaViewDefinitionId: selectedViewId || null,
        kpiCode: kpiForm.kpiCode,
        kpiName: kpiForm.kpiName,
        kpiCategory: kpiForm.kpiCategory,
        valueExpression: kpiForm.valueExpression,
        unit: kpiForm.unit,
        dimensionExpression: kpiForm.dimensionExpression,
        aggregationType: kpiForm.aggregationType,
        kpiOptionsJson: "{}",
        description: "Created from Phase 4 Schema Configuration panel.",
        isSynthetic: true,
        sourceSystem: "PlantProcessIQ.Admin",
        sourceRecordId: "PHASE4-KPI",
      });
      await loadSchemaConfig();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create KPI.");
    } finally {
      setIsBusy(false);
    }
  }

  const selectedView = schemaViews.find((x) => x.id === selectedViewId);

  return (
    <AdminPanel
      title="Schema View Builder"
      subtitle="Controlled SQL views, JOIN previews and KPI definitions"
      icon={<TableProperties size={18} />}
      wide
    >
      <p className="admin-copy">
        This layer converts raw/dump/staging records into customer-specific
        schema views before canonical mapping. Only safe SELECT/WITH queries are
        allowed — destructive SQL is blocked.
      </p>

      {error ? (
        <div className="admin-inline-error">{error}</div>
      ) : null}

      {/* SQL View builder */}
      <div className="admin-schema-grid">
        <section className="admin-form-card admin-form-card--wide">
          <h3>Create / Preview Controlled SQL View</h3>

          <div className="admin-form-row">
            <label>
              View Code
              <input
                value={viewForm.schemaViewCode}
                onChange={(e) => setViewForm((f) => ({ ...f, schemaViewCode: e.target.value }))}
              />
            </label>
            <label>
              View Name
              <input
                value={viewForm.schemaViewName}
                onChange={(e) => setViewForm((f) => ({ ...f, schemaViewName: e.target.value }))}
              />
            </label>
            <label>
              View Kind
              <select
                value={viewForm.viewKind}
                onChange={(e) => setViewForm((f) => ({ ...f, viewKind: e.target.value }))}
              >
                <option value="SqlView">SQL View</option>
                <option value="JoinView">Join View</option>
                <option value="KpiView">KPI View</option>
                <option value="MappingPreparationView">Mapping Preparation View</option>
              </select>
            </label>
          </div>

          <textarea
            className="admin-sql-editor"
            value={viewForm.sqlText}
            onChange={(e) => setViewForm((f) => ({ ...f, sqlText: e.target.value }))}
            spellCheck={false}
            rows={8}
          />

          <div className="admin-action-row">
            <button
              className="secondary-button"
              onClick={previewAdHocSql}
              disabled={isBusy || !viewForm.sqlText.trim()}
              type="button"
            >
              Preview SQL
            </button>
            <button
              className="primary-button"
              onClick={createSchemaView}
              disabled={isBusy || !viewForm.schemaViewCode.trim()}
              type="button"
            >
              Save Schema View
            </button>
          </div>
        </section>
      </div>

      {/* Stored views selector */}
      <div className="admin-schema-grid">
        <section className="admin-form-card">
          <h3>Stored Schema Views</h3>
          <label>
            Select View
            <select
              value={selectedViewId}
              onChange={(e) => setSelectedViewId(e.target.value)}
            >
              <option value="">Select schema view…</option>
              {schemaViews.map((view) => (
                <option key={view.id} value={view.id}>
                  {view.schemaViewCode} — {view.viewKind}
                </option>
              ))}
            </select>
          </label>

          {selectedView ? (
            <div className="admin-selected-hint">
              <strong>{selectedView.schemaViewName}</strong>
              <br />
              Status: {selectedView.lastValidationStatus ?? "Not validated"} ·
              Approved: {selectedView.isApproved ? "Yes" : "No"}
              {selectedView.lastValidationMessage ? (
                <><br />{selectedView.lastValidationMessage}</>
              ) : null}
            </div>
          ) : null}

          <div className="admin-action-row">
            <button
              className="secondary-button"
              onClick={previewSelectedView}
              disabled={isBusy || !selectedViewId}
              type="button"
            >
              Preview Selected
            </button>
            <button
              className="secondary-button"
              onClick={approveSelectedView}
              disabled={isBusy || !selectedViewId}
              type="button"
            >
              Approve View
            </button>
          </div>
        </section>

        {/* KPI creator */}
        <section className="admin-form-card">
          <h3>Create KPI Definition</h3>
          <label>
            KPI Code
            <input
              value={kpiForm.kpiCode}
              onChange={(e) => setKpiForm((f) => ({ ...f, kpiCode: e.target.value }))}
            />
          </label>
          <label>
            KPI Name
            <input
              value={kpiForm.kpiName}
              onChange={(e) => setKpiForm((f) => ({ ...f, kpiName: e.target.value }))}
            />
          </label>
          <label>
            Value Expression
            <input
              value={kpiForm.valueExpression}
              onChange={(e) => setKpiForm((f) => ({ ...f, valueExpression: e.target.value }))}
            />
          </label>
          <button
            className="primary-button"
            onClick={createKpi}
            disabled={isBusy || !kpiForm.kpiCode.trim()}
            type="button"
          >
            Create KPI
          </button>
        </section>
      </div>

      {/* Preview result */}
      {preview ? (
        <section className="admin-preview-panel">
          <div className="admin-panel__header">
            <div className="admin-panel__icon"><PlayCircle size={18} /></div>
            <div>
              <h2>Preview Result</h2>
              <p>
                {preview.message} · {preview.durationMs}ms · {preview.rowCount} rows
              </p>
            </div>
          </div>

          {preview.columns.length > 0 ? (
            <div className="admin-table-wrap">
              <table>
                <thead>
                  <tr>
                    {preview.columns.map((col) => (
                      <th key={col.columnName}>
                        {col.columnName}
                        <small>{col.dataType}</small>
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {preview.rows.map((row, i) => (
                    <tr key={i}>
                      {preview.columns.map((col) => (
                        <td key={col.columnName}>
                          {String(row[col.columnName] ?? "—")}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </section>
      ) : null}

      {/* All schema views table */}
      <div className="admin-table-wrap">
        <table>
          <thead>
            <tr>
              <th>Schema View</th>
              <th>Kind</th>
              <th>Status</th>
              <th>Approved</th>
              <th>Max Rows</th>
              <th>Last Validation</th>
            </tr>
          </thead>
          <tbody>
            {schemaViews.map((view) => (
              <tr key={view.id}>
                <td>
                  <strong>{view.schemaViewCode}</strong>
                  <small>{view.schemaViewName}</small>
                </td>
                <td>{view.viewKind}</td>
                <td>
                  <StatusPill
                    status={view.lastValidationStatus ?? "NotValidated"}
                    statusClass={view.lastValidationStatus === "Success" ? "success" : "warning"}
                  />
                </td>
                <td>{view.isApproved ? "Yes" : "No"}</td>
                <td>{view.maxPreviewRows}</td>
                <td>{formatDate(view.lastValidatedAtUtc)}</td>
              </tr>
            ))}
            {schemaViews.length === 0 ? (
              <tr>
                <td colSpan={6}>No schema views yet. Create the first above.</td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>

      {/* KPI table */}
      {kpis.length > 0 ? (
        <div className="admin-table-wrap">
          <table>
            <thead>
              <tr>
                <th>KPI</th>
                <th>Category</th>
                <th>Expression</th>
                <th>Unit</th>
                <th>Status</th>
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
                  <td>{kpi.valueExpression}</td>
                  <td>{kpi.unit ?? "—"}</td>
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
      ) : null}
    </AdminPanel>
  );
}
