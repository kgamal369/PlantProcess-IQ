import { FormEvent, useEffect, useMemo, useState } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  DatabaseZap,
  GitBranch,
  Layers3,
  Play,
  RefreshCw,
  Search,
  ShieldCheck,
} from "lucide-react";
import { apiClient } from "@/api/http";
import "./p03p04-workbench.css";
import { StandardButton } from "@/components/standard";

type ApiRows<T> = {
  generatedAtUtc?: string;
  rowCount: number;
  rows: T[];
};

type ProofRow = Record<string, unknown>;

type TabKey =
  | "source-catalog"
  | "business-key"
  | "canonical-mapper"
  | "sql-builder"
  | "genealogy"
  | "publish"
  | "material";

const tabs: Array<{ key: TabKey; label: string; subtitle: string }> = [
  {
    key: "source-catalog",
    label: "Source Catalog",
    subtitle: "Source systems and lifecycle truth",
  },
  {
    key: "business-key",
    label: "Business-Key Dictionary",
    subtitle: "Identity normalization and conflict checks",
  },
  {
    key: "canonical-mapper",
    label: "Canonical Mapper",
    subtitle: "Required fields, types and unit checks",
  },
  {
    key: "sql-builder",
    label: "SQL View Builder",
    subtitle: "Typed safe-SQL resolver proof",
  },
  {
    key: "genealogy",
    label: "Genealogy Builder",
    subtitle: "Cycle/orphan validation",
  },
  {
    key: "publish",
    label: "Validate & Publish",
    subtitle: "Dry-run, publish and rollback lifecycle",
  },
  {
    key: "material",
    label: "Material Investigation",
    subtitle: "Golden-thread trace",
  },
];

function stringifyCell(value: unknown) {
  if (value === null || value === undefined) return "-";
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "boolean") return String(value);
  return JSON.stringify(value);
}

function ProofTable({ rows }: { rows: ProofRow[] }) {
  const columns = useMemo(() => {
    const keys = new Set<string>();

    for (const row of rows) {
      Object.keys(row).forEach((key) => keys.add(key));
    }

    return Array.from(keys).slice(0, 8);
  }, [rows]);

  if (rows.length === 0) {
    return <div className="p03p04-empty">No proof rows returned yet.</div>;
  }

  return (
    <div className="p03p04-table-wrap">
      <table className="p03p04-table">
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column}>{column}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={`${index}-${JSON.stringify(row).slice(0, 30)}`}>
              {columns.map((column) => (
                <td key={column}>
                  <span title={stringifyCell(row[column])}>{stringifyCell(row[column])}</span>
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function StatusStrip({ rows }: { rows: ProofRow[] }) {
  const green = rows.filter((row) => row.is_green === true || row.isGreen === true).length;
  const total = rows.length;

  return (
    <div className="p03p04-status-strip">
      <div>
        <span className="p03p04-kpi-value">{total > 0 ? `${green}/${total}` : "-"}</span>
        <span className="p03p04-kpi-label">completion gates green</span>
      </div>
      <div>
        <span className="p03p04-kpi-value">P03/P04</span>
        <span className="p03p04-kpi-label">mapping + genealogy proof</span>
      </div>
      <div>
        <span className="p03p04-kpi-value">Generic</span>
        <span className="p03p04-kpi-label">not steel-hardcoded</span>
      </div>
    </div>
  );
}

async function getRows(path: string): Promise<ProofRow[]> {
  const result = await apiClient.get<ApiRows<ProofRow>>(path, undefined, { suppressToast: true });
  return result.rows ?? [];
}

async function postRows(path: string, body: unknown): Promise<ProofRow[]> {
  const result = await apiClient.post<ApiRows<ProofRow>>(path, body, { suppressToast: true });
  return result.rows ?? [];
}

export function MaterialInvestigationPage() {
  const [activeTab, setActiveTab] = useState<TabKey>("business-key");
  const [statusRows, setStatusRows] = useState<ProofRow[]>([]);
  const [businessKeyRows, setBusinessKeyRows] = useState<ProofRow[]>([]);
  const [genealogyRows, setGenealogyRows] = useState<ProofRow[]>([]);
  const [mappingRows, setMappingRows] = useState<ProofRow[]>([]);
  const [materialRows, setMaterialRows] = useState<ProofRow[]>([]);
  const [safeSqlRows, setSafeSqlRows] = useState<ProofRow[]>([]);
  const [materialKey, setMaterialKey] = useState("ADV_COIL4002");
  const [sqlText, setSqlText] = useState("SELECT * FROM public.v_ppiq_p04_completion_golden_thread");
  const [isLoading, setIsLoading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  async function loadStatus() {
    const rows = await getRows("/admin/p03p04/completion/status");
    setStatusRows(rows);
  }

  async function loadBusinessKeys() {
    const rows = await getRows("/admin/p03p04/completion/business-key-validation");
    setBusinessKeyRows(rows);
  }

  async function loadGenealogy() {
    const rows = await getRows("/admin/p03p04/completion/genealogy-validation");
    setGenealogyRows(rows);
  }

  async function runMappingLifecycle() {
    const rows = await postRows("/admin/p03p04/completion/mapping-lifecycle-proof/run", {});
    setMappingRows(rows);
  }

  async function resolveSql(event?: FormEvent) {
    event?.preventDefault();

    const rows = await postRows("/admin/p03p04/completion/safe-sql/resolve", {
      sql: sqlText,
      rowLimit: 100,
      timeoutMs: 3000,
    });

    setSafeSqlRows(rows);
  }

  async function investigateMaterial(event?: FormEvent) {
    event?.preventDefault();

    const encoded = encodeURIComponent(materialKey.trim() || "UNKNOWN");
    const rows = await getRows(`/admin/p03p04/completion/material-investigation/${encoded}`);

    setMaterialRows(rows);
  }

  async function refreshAll() {
    setIsLoading(true);
    setMessage(null);

    try {
      await loadStatus();
      await loadBusinessKeys();
      await loadGenealogy();
      await resolveSql();
      await investigateMaterial();
      setMessage("P03/P04 workbench proof refreshed successfully.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Failed to refresh P03/P04 proof.");
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    void refreshAll();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const activeRows =
    activeTab === "business-key"
      ? businessKeyRows
      : activeTab === "sql-builder"
        ? safeSqlRows
        : activeTab === "genealogy"
          ? genealogyRows
          : activeTab === "publish" || activeTab === "canonical-mapper"
            ? mappingRows
            : activeTab === "material"
              ? materialRows
              : statusRows;

  const hasFatalRows = activeRows.some(
    (row) =>
      row.severity === "Error" ||
      row.is_green === false ||
      row.isGreen === false ||
      row.is_valid === false ||
      row.isValid === false,
  );

  return (
    <main className="p03p04-page">
      <section className="p03p04-hero">
        <div>
          <p className="p03p04-eyebrow">P03/P04 Completion Workbench</p>
          <h1>Mapping, Genealogy and Material Investigation Proof</h1>
          <p>
            A single operational workbench for business keys, canonical mapping, safe SQL,
            genealogy validation, mapping lifecycle, and golden-thread material investigation.
          </p>
        </div>

        <StandardButton className="p03p04-primary" type="button" onClick={refreshAll} isDisabled={isLoading}>
          <RefreshCw size={16} />
          {isLoading ? "Refreshing..." : "Refresh proof"}
        </StandardButton>
      </section>

      <StatusStrip rows={statusRows} />

      {message ? (
        <div className="p03p04-message">
          <ShieldCheck size={16} />
          <span>{message}</span>
        </div>
      ) : null}

      <section className="p03p04-tabs">
        {tabs.map((tab) => (
          <StandardButton
            key={tab.key}
            className={activeTab === tab.key ? "p03p04-tab p03p04-tab--active" : "p03p04-tab"}
            type="button"
            onClick={() => setActiveTab(tab.key)}
          >
            <strong>{tab.label}</strong>
            <span>{tab.subtitle}</span>
          </StandardButton>
        ))}
      </section>

      <section className="p03p04-panel">
        {activeTab === "source-catalog" ? (
          <div className="p03p04-panel-grid">
            <article>
              <DatabaseZap size={24} />
              <h2>Source Catalog</h2>
              <p>
                Source-specific demo systems stay as fixtures. The product layer remains generic
                through business keys, canonical mapping, recursive genealogy and quality events.
              </p>
            </article>
            <article>
              <Layers3 size={24} />
              <h2>Canonical Output</h2>
              <p>
                Mapping output is consumed as canonical material, process, parameter, quality,
                downtime and value-impact rows.
              </p>
            </article>
          </div>
        ) : null}

        {activeTab === "business-key" ? (
          <>
            <div className="p03p04-panel-header">
              <div>
                <h2>Business-Key Dictionary Validation</h2>
                <p>Detects missing members, duplicate normalized samples and nullable key members.</p>
              </div>
              <StandardButton className="p03p04-secondary" type="button" onClick={loadBusinessKeys}>
                <RefreshCw size={14} />
                Validate
              </StandardButton>
            </div>
            <ProofTable rows={businessKeyRows} />
          </>
        ) : null}

        {activeTab === "canonical-mapper" ? (
          <>
            <div className="p03p04-panel-header">
              <div>
                <h2>Canonical Entity Mapper</h2>
                <p>Runs required-field, type, unit-conversion and mapping lifecycle proof.</p>
              </div>
              <StandardButton className="p03p04-secondary" type="button" onClick={runMappingLifecycle}>
                <Play size={14} />
                Run proof
              </StandardButton>
            </div>
            <ProofTable rows={mappingRows} />
          </>
        ) : null}

        {activeTab === "sql-builder" ? (
          <>
            <div className="p03p04-panel-header">
              <div>
                <h2>SQL View Builder</h2>
                <p>Read-only SQL validation with typed resolver errors and EXPLAIN proof.</p>
              </div>
            </div>

            <form className="p03p04-sql-form" onSubmit={resolveSql}>
              <textarea
                value={sqlText}
                onChange={(event) => setSqlText(event.target.value)}
                spellCheck={false}
              />
              <StandardButton className="p03p04-primary" type="submit">
                <Search size={14} />
                Resolve SQL
              </StandardButton>
            </form>

            <ProofTable rows={safeSqlRows} />
          </>
        ) : null}

        {activeTab === "genealogy" ? (
          <>
            <div className="p03p04-panel-header">
              <div>
                <h2>Genealogy Builder Proof</h2>
                <p>Recursive graph validation with cycle and orphan checks.</p>
              </div>
              <StandardButton className="p03p04-secondary" type="button" onClick={loadGenealogy}>
                <GitBranch size={14} />
                Validate graph
              </StandardButton>
            </div>
            <ProofTable rows={genealogyRows} />
          </>
        ) : null}

        {activeTab === "publish" ? (
          <>
            <div className="p03p04-panel-header">
              <div>
                <h2>Validation & Publish</h2>
                <p>Dry-run, publish and rollback proof for versioned mappings.</p>
              </div>
              <StandardButton className="p03p04-primary" type="button" onClick={runMappingLifecycle}>
                <Play size={14} />
                Run lifecycle
              </StandardButton>
            </div>
            <ProofTable rows={mappingRows} />
          </>
        ) : null}

        {activeTab === "material" ? (
          <>
            <div className="p03p04-panel-header">
              <div>
                <h2>Material Investigation</h2>
                <p>Trace material through genealogy, process, parameters, quality and downtime.</p>
              </div>
            </div>

            <form className="p03p04-search-form" onSubmit={investigateMaterial}>
              <input
                value={materialKey}
                onChange={(event) => setMaterialKey(event.target.value)}
                placeholder="Material key, for example ADV_COIL4002"
              />
              <StandardButton className="p03p04-primary" type="submit">
                <Search size={14} />
                Investigate
              </StandardButton>
            </form>

            <ProofTable rows={materialRows} />
          </>
        ) : null}

        {hasFatalRows ? (
          <div className="p03p04-warning">
            <AlertTriangle size={16} />
            <span>One or more validation rows needs review before claiming 100%.</span>
          </div>
        ) : (
          <div className="p03p04-ok">
            <CheckCircle2 size={16} />
            <span>Current tab has no fatal proof blocker returned by the backend.</span>
          </div>
        )}
      </section>
    </main>
  );
}

export default MaterialInvestigationPage;
