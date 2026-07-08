// ============================================================
// FILE: Frontend/PlantProcess.Web/src/pages/SourceImportPrepPage.tsx
// M1-03 Surface-1: Visual import-prep on LIVE discovery.
// Select connected profile -> live tables (M1-04 /tables) -> live columns (/columns)
// -> pick column subset + PK + watermark + row filter -> Register (/register) -> bound to import job.
// 100% generic: no table/column name is hardcoded; works for any registered schema/provider.
// Replaces the demo-only V5NoCodeMapper discover-demo path (golden rule: no demo in app).
// PPIQ-T11: table-picker uses StandardPageButton; column grid uses StandardTable.
// ============================================================
import { useEffect, useState, useCallback, type CSSProperties } from "react";
import { productApi, type ConnectionProfileRecord } from "../api/productApiClient";
import type {
  SourceTableRecord,
  SourceColumnRecord,
} from "../api/product-core/shared-types";
import { StandardPageButton } from "@/components/standard/StandardPageCompat";
import { StandardTable } from "@/components/standard/StandardTable";

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
      setError("Select at least one primary key column.");
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
    <div className="piq-surface1" style={{ maxWidth: 960, margin: "0 auto", padding: "1.5rem" }}>
      <header style={{ marginBottom: "1rem" }}>
        <h1 style={{ margin: 0 }}>Prepare Source for Import</h1>
        <p style={{ color: "var(--piq-text-dim, #8AA3C0)", marginTop: 4 }}>
          Select a connected source, choose a table and columns, and register it to drive the import job.
        </p>
      </header>

      {error && (
        <div role="alert" style={alertStyle}>
          {error}
        </div>
      )}

      {/* STEP 1: source */}
      <section style={cardStyle}>
        <label style={labelStyle}>1. Connected source</label>
        <select
          value={profileId}
          onChange={(e) => {
            setProfileId(e.target.value);
            if (e.target.value) loadTables(e.target.value);
          }}
          style={selectStyle}
        >
          <option value="">Select a connection profile...</option>
          {profiles.map((p) => (
            <option key={p.id} value={p.id}>
              {p.connectionProfileName} ({p.providerType})
            </option>
          ))}
        </select>
      </section>

      {/* STEP 2: table */}
      {step !== "source" && (
        <section style={cardStyle}>
          <label style={labelStyle}>2. Source table</label>
          {loading && tables.length === 0 ? (
            <p style={{ color: "var(--piq-text-dim,#8AA3C0)" }}>Discovering tables...</p>
          ) : (
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill,minmax(220px,1fr))", gap: 8 }}>
              {tables.map((t) => {
                const active = selectedTable?.tableName === t.tableName;
                return (
                  <StandardPageButton
                    key={`${t.schemaName}.${t.tableName}`}
                    onClick={() => loadColumns(t)}
                    style={{
                      ...tableBtnStyle,
                      borderColor: active ? "var(--piq-accent,#4F9CF9)" : "var(--piq-line,#27466B)",
                    }}
                  >
                    <strong>{t.tableName}</strong>
                    <span style={{ color: "var(--piq-text-dim,#8AA3C0)", fontSize: 12 }}>
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
        <section style={cardStyle}>
          <label style={labelStyle}>
            3. Columns &amp; preparation - {selectedTable.schemaName}.{selectedTable.tableName}
          </label>
          <StandardTable<SourceColumnRecord>
            data={columns}
            getRowKey={(c) => c.columnName}
            columns={[
              {
                key: "import",
                header: "Import",
                cell: (c) => (
                  <input
                    type="checkbox"
                    checked={selectedColumns.has(c.columnName)}
                    onChange={() => toggle(selectedColumns, c.columnName, setSelectedColumns)}
                  />
                ),
              },
              {
                key: "column",
                header: "Column",
                cell: (c) => (
                  <span>
                    {c.columnName}
                    {c.isPrimaryKeyCandidate && <span style={pill}>pk?</span>}
                    {c.isTimestampCandidate && <span style={pill}>ts?</span>}
                  </span>
                ),
              },
              { key: "type", header: "Type", cell: (c) => c.dataType },
              {
                key: "pk",
                header: "PK",
                cell: (c) => (
                  <input
                    type="checkbox"
                    checked={primaryKeys.has(c.columnName)}
                    onChange={() => toggle(primaryKeys, c.columnName, setPrimaryKeys)}
                  />
                ),
              },
              {
                key: "watermark",
                header: "Watermark",
                cell: (c) => (
                  <input
                    type="radio"
                    name="watermark"
                    checked={watermark === c.columnName}
                    onChange={() => setWatermark(c.columnName)}
                  />
                ),
              },
            ]}
          />
          <div style={{ marginTop: 12 }}>
            <label style={labelStyle}>Optional row filter (SQL WHERE, no keyword)</label>
            <input
              value={rowFilter}
              onChange={(e) => setRowFilter(e.target.value)}
              placeholder="e.g. steel_grade = 'S355J2'"
              style={selectStyle}
            />
          </div>
          <div style={{ marginTop: 16, display: "flex", gap: 8 }}>
            <StandardPageButton className="ppiq-std-button--primary" onClick={register} isLoading={loading}>
              Register &amp; Prepare
            </StandardPageButton>
            <span style={{ color: "var(--piq-text-dim,#8AA3C0)", alignSelf: "center", fontSize: 12 }}>
              {selectedColumns.size}/{columns.length} columns - {primaryKeys.size} PK - watermark: {watermark || "none"}
            </span>
          </div>
        </section>
      )}

      {/* STEP 4: done */}
      {step === "done" && (
        <section style={{ ...cardStyle, borderColor: "var(--piq-accent,#30C48D)" }}>
          <strong>Registered.</strong> {resultMsg}
          <p style={{ color: "var(--piq-text-dim,#8AA3C0)", marginTop: 8 }}>
            This source is now bound to the two-stage import job. Run Stage-1 from the Importing Data area to load it.
          </p>
          <StandardPageButton
            onClick={() => {
              setStep("source");
              setProfileId("");
              setResultMsg(null);
            }}
          >
            Prepare another
          </StandardPageButton>
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

const cardStyle: CSSProperties = {
  background: "var(--piq-surface,#0E2238)",
  border: "1px solid var(--piq-line,#27466B)",
  borderRadius: 8,
  padding: 16,
  marginBottom: 12,
};
const labelStyle: CSSProperties = { display: "block", fontWeight: 600, marginBottom: 8 };
const selectStyle: CSSProperties = {
  width: "100%",
  padding: "8px 10px",
  background: "var(--piq-bg,#0B1B2E)",
  color: "var(--piq-text,#D7E5F7)",
  border: "1px solid var(--piq-line,#27466B)",
  borderRadius: 6,
};
const tableBtnStyle: CSSProperties = {
  display: "flex",
  flexDirection: "column",
  alignItems: "flex-start",
  gap: 2,
  padding: 10,
  background: "var(--piq-bg,#0B1B2E)",
  color: "var(--piq-text,#D7E5F7)",
  border: "1px solid var(--piq-line,#27466B)",
  borderRadius: 6,
  cursor: "pointer",
  textAlign: "left",
};
const pill: CSSProperties = {
  marginLeft: 6,
  fontSize: 10,
  padding: "1px 5px",
  borderRadius: 8,
  background: "var(--piq-line,#27466B)",
  color: "var(--piq-text,#D7E5F7)",
};
const alertStyle: CSSProperties = {
  background: "rgba(229,72,77,0.12)",
  border: "1px solid #E5484D",
  color: "#E5484D",
  padding: "10px 12px",
  borderRadius: 6,
  marginBottom: 12,
};