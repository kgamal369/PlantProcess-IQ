// ============================================================
// FILE: Frontend/PlantProcess.Web/src/pages/SourceImportPrepPage.tsx
// M1-03 Surface-1: Visual import-prep on LIVE discovery.
// Select connected profile -> live tables (M1-04 /tables) -> live columns (/columns)
// -> pick column subset + PK + watermark + row filter -> Register (/register) -> bound to import job.
// 100% generic: no table/column name is hardcoded; works for any registered schema/provider.
//
// PPIQ-SCENE234: restyled onto the demo palette. This page predated the design
// system. It carried six local style objects, thirteen inline styles, a raw
// select, four raw text fields, and eight hardcoded colours that matched
// nothing else in the product. All of that now lives in sourceImportPrep.css on
// the mandated token set, and the failure state is an amber honest notice
// rather than a red banner - a red banner on the demo path is an automatic
// fail by contract. Wiring, API calls and discovery defaults are unchanged.
// ============================================================
import { useEffect, useState, useCallback } from "react";
import { productApi, type ConnectionProfileRecord } from "../api/productApiClient";
import type {
  SourceTableRecord,
  SourceColumnRecord,
} from "../api/product-core/shared-types";
import { StandardPageButton } from "@/components/standard/StandardPageCompat";
import { StandardTable } from "@/components/standard/StandardTable";
import { StandardP2Input, StandardP2Select } from "@/components/standard/StandardP2Controls";
import "./sourceImportPrep.css";

type Step = "source" | "table" | "columns" | "done";

export default function SourceImportPrepPage() {
  const [profiles, setProfiles] = useState<ConnectionProfileRecord[]>([]);
  const [profileId, setProfileId] = useState<string>("");
  const [tables, setTables] = useState<SourceTableRecord[]>([]);
  const [selectedTable, setSelectedTable] = useState<SourceTableRecord | null>(null);
  const [columns, setColumns] = useState<SourceColumnRecord[]>([]);
  const [selectedColumns, setSelectedColumns] = useState<Set<string>>(new Set());
  const [primaryKeys, setPrimaryKeys] = useState<Set<string>>(new Set());
  const [watermark, setWatermark] = useState<string>("");
  const [rowFilter, setRowFilter] = useState<string>("");
  const [step, setStep] = useState<Step>("source");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [resultMsg, setResultMsg] = useState<string | null>(null);

  useEffect(() => {
    productApi
      .getConnectionProfiles(false)
      .then((r) => setProfiles((r ?? []).filter((p) => isDbProvider(p.providerType))))
      .catch((e) => setError(String(e?.message ?? e)));
  }, []);

  const loadTables = useCallback(async (id: string) => {
    setError(null);
    setLoading(true);
    setTables([]);
    setSelectedTable(null);
    setColumns([]);
    try {
      const t = await productApi.listSourceTables(id);
      setTables(t ?? []);
      setStep("table");
    } catch (e: unknown) {
      setError(describeError(e));
    } finally {
      setLoading(false);
    }
  }, []);

  const loadColumns = useCallback(
    async (table: SourceTableRecord) => {
      setError(null);
      setLoading(true);
      setColumns([]);
      setSelectedColumns(new Set());
      setPrimaryKeys(new Set());
      setWatermark("");
      try {
        const cols = await productApi.listSourceColumns(profileId, table.schemaName, table.tableName);
        const list = cols ?? [];
        setColumns(list);
        // Sensible generic defaults from discovery flags.
        setSelectedColumns(new Set(list.map((c) => c.columnName)));
        setPrimaryKeys(new Set(list.filter((c) => c.isPrimaryKeyCandidate).map((c) => c.columnName)));
        const ts = list.find((c) => c.isTimestampCandidate);
        if (ts) setWatermark(ts.columnName);
        setSelectedTable(table);
        setStep("columns");
      } catch (e: unknown) {
        setError(describeError(e));
      } finally {
        setLoading(false);
      }
    },
    [profileId]
  );

  const register = useCallback(async () => {
    if (!selectedTable) return;
    if (primaryKeys.size === 0) {
      setError("Select at least one primary key column before registering.");
      return;
    }
    setError(null);
    setLoading(true);
    try {
      const result = await productApi.registerSourceTable(profileId, {
        schemaName: selectedTable.schemaName,
        tableName: selectedTable.tableName,
        primaryKeyColumns: Array.from(primaryKeys),
        watermarkColumn: watermark || null,
        selectedColumns: selectedColumns.size === columns.length ? null : Array.from(selectedColumns),
        rowFilter: rowFilter.trim() || null,
      });
      setResultMsg(result?.message ?? "Registered.");
      setStep("done");
    } catch (e: unknown) {
      setError(describeError(e));
    } finally {
      setLoading(false);
    }
  }, [profileId, selectedTable, primaryKeys, watermark, selectedColumns, columns.length, rowFilter]);

  const toggle = (set: Set<string>, key: string, setter: (s: Set<string>) => void) => {
    const next = new Set(set);
    if (next.has(key)) {
      next.delete(key);
    } else {
      next.add(key);
    }
    setter(next);
  };

  return (
    <div className="piq-surface1 sip-page">
      <header className="sip-header">
        <h1>Prepare Source for Import</h1>
        <p className="sip-lede">
          Select a connected source, choose a table and columns, and register it to drive the import job.
        </p>
      </header>

      {error && (
        <div role="status" className="sip-notice">
          <span className="sip-notice__label">Not registered</span>
          <span>{error}</span>
        </div>
      )}

      {/* STEP 1: source */}
      <section className="sip-card">
        <div className="sip-step">
          <span className="sip-step__index">Step 1</span>
          <span className="sip-step__label">Connected source</span>
        </div>
        <StandardP2Select
          aria-label="Connected source"
          value={profileId}
          onChange={(e) => {
            setProfileId(e.target.value);
            if (e.target.value) loadTables(e.target.value);
          }}
        >
          <option value="">Select a connection profile...</option>
          {profiles.map((p) => (
            <option key={p.id} value={p.id}>
              {p.connectionProfileName} ({p.providerType})
            </option>
          ))}
        </StandardP2Select>
      </section>

      {/* STEP 2: table */}
      {step !== "source" && (
        <section className="sip-card">
          <div className="sip-step">
            <span className="sip-step__index">Step 2</span>
            <span className="sip-step__label">Source table</span>
            {tables.length > 0 && (
              <span className="sip-faint">{tables.length} discovered</span>
            )}
          </div>
          {loading && tables.length === 0 ? (
            <p className="sip-muted">Discovering tables...</p>
          ) : (
            <div className="sip-table-grid">
              {tables.map((t) => {
                const active = selectedTable?.tableName === t.tableName;
                return (
                  <StandardPageButton
                    key={`${t.schemaName}.${t.tableName}`}
                    onClick={() => loadColumns(t)}
                    className={"sip-table-card" + (active ? " sip-table-card--active" : "")}
                  >
                    <span className="sip-table-card__name">{t.tableName}</span>
                    <span className="sip-table-card__meta">
                      {t.schemaName} - {t.kind}
                    </span>
                  </StandardPageButton>
                );
              })}
            </div>
          )}
        </section>
      )}

      {/* STEP 3: columns + prep */}
      {step === "columns" && selectedTable && (
        <section className="sip-card">
          <div className="sip-step">
            <span className="sip-step__index">Step 3</span>
            <span className="sip-step__label">Columns and preparation</span>
            <span className="sip-step__target">
              {selectedTable.schemaName}.{selectedTable.tableName}
            </span>
          </div>
          <StandardTable<SourceColumnRecord>
            data={columns}
            getRowKey={(c) => c.columnName}
            columns={[
              {
                key: "import",
                header: "Import",
                cell: (c) => (
                  <StandardP2Input
                    type="checkbox"
                    aria-label={`Import ${c.columnName}`}
                    checked={selectedColumns.has(c.columnName)}
                    onChange={() => toggle(selectedColumns, c.columnName, setSelectedColumns)}
                  />
                ),
              },
              {
                key: "column",
                header: "Column",
                cell: (c) => (
                  <span className="sip-colname">
                    {c.columnName}
                    {c.isPrimaryKeyCandidate && <span className="sip-pill">pk?</span>}
                    {c.isTimestampCandidate && <span className="sip-pill">ts?</span>}
                  </span>
                ),
              },
              { key: "type", header: "Type", cell: (c) => c.dataType },
              {
                key: "pk",
                header: "PK",
                cell: (c) => (
                  <StandardP2Input
                    type="checkbox"
                    aria-label={`Primary key ${c.columnName}`}
                    checked={primaryKeys.has(c.columnName)}
                    onChange={() => toggle(primaryKeys, c.columnName, setPrimaryKeys)}
                  />
                ),
              },
              {
                key: "watermark",
                header: "Watermark",
                cell: (c) => (
                  <StandardP2Input
                    type="radio"
                    name="watermark"
                    aria-label={`Watermark ${c.columnName}`}
                    checked={watermark === c.columnName}
                    onChange={() => setWatermark(c.columnName)}
                  />
                ),
              },
            ]}
          />
          <div className="sip-field-row">
            <div className="sip-step">
              <span className="sip-step__label">Optional row filter</span>
              <span className="sip-faint">SQL WHERE, without the keyword</span>
            </div>
            <StandardP2Input
              aria-label="Optional row filter"
              value={rowFilter}
              onChange={(e) => setRowFilter(e.target.value)}
              placeholder="e.g. status = 'CLOSED'"
            />
          </div>
          <div className="sip-actions">
            <StandardPageButton className="ppiq-std-button--primary" onClick={register} isLoading={loading}>
              Register and Prepare
            </StandardPageButton>
            <span className="sip-summary">
              {selectedColumns.size}/{columns.length} columns - {primaryKeys.size} PK - watermark: {watermark || "none"}
            </span>
          </div>
        </section>
      )}

      {/* STEP 4: done */}
      {step === "done" && (
        <section className="sip-card sip-card--done">
          <div className="sip-step">
            <span className="sip-step__index">Step 4</span>
            <span className="sip-done-title">Registered</span>
          </div>
          <p className="sip-muted">{resultMsg}</p>
          <p className="sip-muted">
            This source is now bound to the two-stage import job. Run Stage-1 from the Importing Data area to load it.
          </p>
          <div className="sip-actions">
            <StandardPageButton
              onClick={() => {
                setStep("source");
                setProfileId("");
                setResultMsg(null);
              }}
            >
              Prepare another
            </StandardPageButton>
          </div>
        </section>
      )}
    </div>
  );
}

function isDbProvider(p: string): boolean {
  const v = (p ?? "").toLowerCase();
  return ["postgresql", "mysql", "sqlserver", "oracle"].includes(v);
}

function describeError(e: unknown): string {
  if (e && typeof e === "object" && "message" in e) return String((e as { message: unknown }).message);
  return String(e);
}
