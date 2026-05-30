import { useEffect, useMemo, useState } from "react";
import {
  CheckCircle2,
  DatabaseZap,
  GitBranch,
  Gauge,
  Loader2,
  PlayCircle,
  RefreshCw,
  Save,
  SearchCheck,
} from "lucide-react";

import { AdminPanel, EmptyAdminState, StatusPill, formatDate } from "./AdminSharedComponents";
import {
  schemaMappingApi,
  type CanonicalSchemaViewRow,
  type CrossSourceJoinRequest,
  type PreviewResult,
} from "@/api/schemaMapping/schemaMapping.api";

function safeText(value: unknown) {
  if (value === null || value === undefined) return "—";
  if (typeof value === "object") return JSON.stringify(value);
  return String(value);
}

export function CanonicalSchemaMappingPanel() {
  const [catalog, setCatalog] = useState<CanonicalSchemaViewRow[]>([]);
  const [preview, setPreview] = useState<PreviewResult | null>(null);
  const [selectedViewCode, setSelectedViewCode] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);

  const [joinForm, setJoinForm] = useState({
    viewCode: "HSM_SURFACE_DEFECT_JOIN",
    viewName: "HSM to Surface Defect Join",
    leftSchema: "src_hsm_oracle_shape",
    leftTable: "hsm_coils",
    rightSchema: "src_inspection_mysql_shape",
    rightTable: "parsytec_surface_defects",
    leftJoinColumn: "coil_id",
    rightJoinColumn: "coil_id",
    targetEntity: "QualityEvent",
    physicalViewName: "cv_hsm_surface_defect_join",
    columnsText:
      "left,coil_id,coil_id\nleft,input_piece_id,slab_id\nleft,heat_no,heat_no\nleft,mill_line,mill_line\nright,defect_code,defect_code\nright,defect_name,defect_name\nright,defect_severity,defect_severity\nright,event_time_utc,event_time_utc",
  });

  const [kpiForm, setKpiForm] = useState({
    viewCode: "KPI_HSM_TEMPERATURE_QUALITY",
    viewName: "HSM Temperature Quality KPI",
    kpiCode: "HSM_TEMP_DEFECT_RATE",
    kpiName: "HSM temperature defect rate",
    kpiCategory: "Quality",
    attachedScopeType: "Area",
    attachedScopeCode: "HSM",
    unit: "%",
    physicalViewName: "cv_kpi_hsm_temperature_quality",
    sqlText:
      "SELECT\n  mill_line,\n  production_day_utc,\n  coil_count,\n  avg_fdt_c,\n  avg_ct_c,\n  defect_count,\n  defects_per_100_coils\nFROM public.v_phase1_kpi_quality_temperature_window",
  });

  const activeViews = useMemo(
    () => catalog.filter((x) => x.is_active && x.is_approved),
    [catalog]
  );

  async function load() {
    setIsBusy(true);
    setMessage(null);
    try {
      const rows = await schemaMappingApi.getCatalog();
      setCatalog(rows);
      if (!selectedViewCode && rows.length > 0) {
        setSelectedViewCode(rows[0].view_code);
      }
    } catch (err) {
      setMessage(err instanceof Error ? err.message : "Schema mapping catalog refresh failed.");
    } finally {
      setIsBusy(false);
    }
  }

  useEffect(() => {
    load();
  }, []);

  function buildJoinRequest(): CrossSourceJoinRequest {
    const columns = joinForm.columnsText
      .split(/\r?\n/g)
      .map((line) => line.trim())
      .filter(Boolean)
      .map((line) => {
        const [side, column, alias] = line.split(",").map((part) => part.trim());
        return {
          side: side === "right" ? "right" : "left",
          column,
          alias: alias || null,
        } as const;
      });

    return {
      leftSchema: joinForm.leftSchema,
      leftTable: joinForm.leftTable,
      rightSchema: joinForm.rightSchema,
      rightTable: joinForm.rightTable,
      leftJoinColumn: joinForm.leftJoinColumn,
      rightJoinColumn: joinForm.rightJoinColumn,
      columns,
      maxRows: 100,
    };
  }

  async function previewJoin() {
    setIsBusy(true);
    setMessage(null);
    setPreview(null);
    try {
      const result = await schemaMappingApi.previewJoin(buildJoinRequest());
      setPreview(result);
      setMessage(`Join preview returned ${result.rowCount} row(s).`);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : "Join preview failed.");
    } finally {
      setIsBusy(false);
    }
  }

  async function saveJoin() {
    setIsBusy(true);
    setMessage(null);
    try {
      const row = await schemaMappingApi.materializeJoin({
        viewCode: joinForm.viewCode,
        viewName: joinForm.viewName,
        join: buildJoinRequest(),
        targetEntity: joinForm.targetEntity,
        physicalSchema: "public",
        physicalViewName: joinForm.physicalViewName,
        attachedScopeType: "DemoLifecycle",
        attachedScopeCode: "FlatSteelGoldenDemo",
        mappingJson: JSON.stringify({
          source: "cross-source join authoring",
          left: `${joinForm.leftSchema}.${joinForm.leftTable}`,
          right: `${joinForm.rightSchema}.${joinForm.rightTable}`,
        }),
      });
      setMessage(`Saved canonical join view ${row.view_code}.`);
      await load();
    } catch (err) {
      setMessage(err instanceof Error ? err.message : "Saving join view failed.");
    } finally {
      setIsBusy(false);
    }
  }

  async function saveKpiView() {
    setIsBusy(true);
    setMessage(null);
    try {
      const row = await schemaMappingApi.createKpiView({
        viewCode: kpiForm.viewCode,
        viewName: kpiForm.viewName,
        kpiCode: kpiForm.kpiCode,
        kpiName: kpiForm.kpiName,
        kpiCategory: kpiForm.kpiCategory,
        sqlText: kpiForm.sqlText,
        physicalSchema: "public",
        physicalViewName: kpiForm.physicalViewName,
        unit: kpiForm.unit,
        valueExpression: "defects_per_100_coils",
        dimensionExpression: "mill_line",
        aggregationType: "Average",
        attachedScopeType: kpiForm.attachedScopeType,
        attachedScopeCode: kpiForm.attachedScopeCode,
        isSynthetic: true,
      });
      setMessage(`Saved KPI-as-view ${row.view_code}.`);
      await load();
    } catch (err) {
      setMessage(err instanceof Error ? err.message : "Saving KPI view failed.");
    } finally {
      setIsBusy(false);
    }
  }

  async function executeSelected() {
    if (!selectedViewCode) return;

    setIsBusy(true);
    setMessage(null);
    try {
      const result = await schemaMappingApi.executeMapping(selectedViewCode, {
        executionMode: "ValidateAndRefreshView",
        previewOnly: false,
        stopOnFirstError: true,
      });
      setMessage(
        `${result.status}: ${result.qualifiedName} refreshed with ${result.rowCount} row(s) in ${result.durationMs}ms.`
      );
      await load();
    } catch (err) {
      setMessage(err instanceof Error ? err.message : "Execution failed.");
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <AdminPanel
      title="Generic Schema Mapping Engine"
      subtitle="Canonical view catalog, resolver, cross-source joins, KPI-as-view and mapping execution"
      icon={<DatabaseZap size={18} />}
      wide
    >
      <div className="admin-kpi-row">
        <div className="admin-mini-kpi">
          <span>Catalog Views</span>
          <strong>{catalog.length}</strong>
        </div>
        <div className="admin-mini-kpi">
          <span>Active / Approved</span>
          <strong>{activeViews.length}</strong>
        </div>
        <div className="admin-mini-kpi">
          <span>Join Views</span>
          <strong>{catalog.filter((x) => x.view_kind === "JoinView").length}</strong>
        </div>
        <div className="admin-mini-kpi">
          <span>KPI Views</span>
          <strong>{catalog.filter((x) => x.view_kind === "KpiView").length}</strong>
        </div>
      </div>

      {message ? (
        <div className="admin-inline-alert">
          <SearchCheck size={16} />
          <span>{message}</span>
        </div>
      ) : null}

      <div className="admin-action-row">
        <button className="admin-action-button" onClick={load} disabled={isBusy}>
          {isBusy ? <Loader2 size={16} className="spin" /> : <RefreshCw size={16} />}
          Refresh catalog
        </button>

        <select
          className="admin-input"
          value={selectedViewCode}
          onChange={(event) => setSelectedViewCode(event.target.value)}
        >
          <option value="">Select canonical view…</option>
          {catalog.map((row) => (
            <option key={row.id} value={row.view_code}>
              {row.view_code} · {row.target_entity}
            </option>
          ))}
        </select>

        <button
          className="admin-action-button admin-action-button--primary"
          onClick={executeSelected}
          disabled={!selectedViewCode || isBusy}
        >
          <PlayCircle size={16} />
          Execute selected mapping
        </button>
      </div>

      <div className="admin-subgrid">
        <section className="admin-card-soft">
          <h3>
            <GitBranch size={16} /> Cross-source join authoring
          </h3>
          <div className="admin-form-grid">
            <label>
              View code
              <input
                value={joinForm.viewCode}
                onChange={(event) => setJoinForm((f) => ({ ...f, viewCode: event.target.value }))}
              />
            </label>
            <label>
              View name
              <input
                value={joinForm.viewName}
                onChange={(event) => setJoinForm((f) => ({ ...f, viewName: event.target.value }))}
              />
            </label>
            <label>
              Left source
              <input
                value={`${joinForm.leftSchema}.${joinForm.leftTable}`}
                onChange={(event) => {
                  const [schema, table] = event.target.value.split(".");
                  setJoinForm((f) => ({
                    ...f,
                    leftSchema: schema ?? "",
                    leftTable: table ?? "",
                  }));
                }}
              />
            </label>
            <label>
              Right source
              <input
                value={`${joinForm.rightSchema}.${joinForm.rightTable}`}
                onChange={(event) => {
                  const [schema, table] = event.target.value.split(".");
                  setJoinForm((f) => ({
                    ...f,
                    rightSchema: schema ?? "",
                    rightTable: table ?? "",
                  }));
                }}
              />
            </label>
            <label>
              Join keys
              <input
                value={`${joinForm.leftJoinColumn} = ${joinForm.rightJoinColumn}`}
                onChange={(event) => {
                  const [left, right] = event.target.value.split("=").map((x) => x.trim());
                  setJoinForm((f) => ({
                    ...f,
                    leftJoinColumn: left ?? "",
                    rightJoinColumn: right ?? "",
                  }));
                }}
              />
            </label>
            <label>
              Physical view
              <input
                value={joinForm.physicalViewName}
                onChange={(event) =>
                  setJoinForm((f) => ({ ...f, physicalViewName: event.target.value }))
                }
              />
            </label>
          </div>

          <label className="admin-textarea-label">
            Selected columns: side,column,alias
            <textarea
              value={joinForm.columnsText}
              onChange={(event) =>
                setJoinForm((f) => ({ ...f, columnsText: event.target.value }))
              }
              rows={7}
            />
          </label>

          <div className="admin-action-row">
            <button className="admin-action-button" onClick={previewJoin} disabled={isBusy}>
              <PlayCircle size={16} />
              Preview join
            </button>
            <button className="admin-action-button admin-action-button--primary" onClick={saveJoin} disabled={isBusy}>
              <Save size={16} />
              Save canonical join
            </button>
          </div>
        </section>

        <section className="admin-card-soft">
          <h3>
            <Gauge size={16} /> KPI-as-view authoring
          </h3>

          <div className="admin-form-grid">
            <label>
              KPI view code
              <input
                value={kpiForm.viewCode}
                onChange={(event) => setKpiForm((f) => ({ ...f, viewCode: event.target.value }))}
              />
            </label>
            <label>
              KPI code
              <input
                value={kpiForm.kpiCode}
                onChange={(event) => setKpiForm((f) => ({ ...f, kpiCode: event.target.value }))}
              />
            </label>
            <label>
              Scope
              <input
                value={`${kpiForm.attachedScopeType}:${kpiForm.attachedScopeCode}`}
                onChange={(event) => {
                  const [type, code] = event.target.value.split(":");
                  setKpiForm((f) => ({
                    ...f,
                    attachedScopeType: type ?? "",
                    attachedScopeCode: code ?? "",
                  }));
                }}
              />
            </label>
            <label>
              Physical view
              <input
                value={kpiForm.physicalViewName}
                onChange={(event) =>
                  setKpiForm((f) => ({ ...f, physicalViewName: event.target.value }))
                }
              />
            </label>
          </div>

          <label className="admin-textarea-label">
            KPI SELECT SQL
            <textarea
              value={kpiForm.sqlText}
              onChange={(event) => setKpiForm((f) => ({ ...f, sqlText: event.target.value }))}
              rows={9}
            />
          </label>

          <div className="admin-action-row">
            <button className="admin-action-button admin-action-button--primary" onClick={saveKpiView} disabled={isBusy}>
              <Save size={16} />
              Save KPI view
            </button>
          </div>
        </section>
      </div>

      {preview ? (
        <section className="admin-card-soft">
          <h3>
            <CheckCircle2 size={16} /> Preview result
          </h3>
          <p className="admin-copy">
            {preview.rowCount} row(s), {preview.columns.length} column(s)
          </p>

          <div className="admin-table-scroll">
            <table className="admin-table">
              <thead>
                <tr>
                  {preview.columns.map((column) => (
                    <th key={column.columnName}>{column.columnName}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {preview.rows.slice(0, 8).map((row, index) => (
                  <tr key={index}>
                    {preview.columns.map((column) => (
                      <td key={column.columnName}>{safeText(row[column.columnName])}</td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ) : null}

      <section className="admin-card-soft">
        <h3>Canonical catalog</h3>
        {catalog.length === 0 ? (
          <EmptyAdminState text="No canonical schema views registered yet. Apply SQL script 120 or save a join/KPI view." />
        ) : (
          <div className="admin-table-scroll">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Kind</th>
                  <th>Target</th>
                  <th>Physical View</th>
                  <th>Status</th>
                  <th>Rows</th>
                  <th>Last Run</th>
                </tr>
              </thead>
              <tbody>
                {catalog.map((row) => (
                  <tr key={row.id}>
                    <td>
                      <strong>{row.view_code}</strong>
                      <span>{row.view_name}</span>
                    </td>
                    <td>{row.view_kind}</td>
                    <td>{row.target_entity}</td>
                    <td>
                      {row.physical_schema}.{row.physical_view_name}
                    </td>
                    <td>
                      <StatusPill
                        status={row.last_execution_status ?? row.last_validation_status ?? "Registered"}
                        statusClass={
                          row.last_execution_status === "Failed" ? "danger" :
                          row.is_approved ? "success" : "warning"
                        }
                      />
                    </td>
                    <td>{row.last_execution_row_count ?? "—"}</td>
                    <td>{formatDate(row.last_executed_at_utc ?? row.last_validated_at_utc)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </AdminPanel>
  );
}