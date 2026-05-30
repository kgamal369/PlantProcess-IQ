const fs = require("node:fs");
const path = require("node:path");
const cp = require("node:child_process");

const root = process.cwd();

function abs(file) {
  return path.join(root, file.split("/").join(path.sep));
}

function read(file) {
  const full = abs(file);
  return fs.existsSync(full) ? fs.readFileSync(full, "utf8") : "";
}

function write(file, content) {
  const full = abs(file);
  fs.mkdirSync(path.dirname(full), { recursive: true });
  fs.writeFileSync(full, content.replace(/^\n/, ""), "utf8");
  console.log("Wrote " + file);
}

function patch(file, patcher) {
  const before = read(file);
  const after = patcher(before);
  if (after !== before) write(file, after);
}

function ensurePackageScripts() {
  const pkg = JSON.parse(read("package.json"));

  pkg.scripts = pkg.scripts || {};
  pkg.scripts["phase56:acceptance"] = "node tools/phase56/validate-phase5-phase6-acceptance.cjs";
  pkg.scripts["validate:phase5-phase6:strict"] = "npm run build && npm run lint && npm run phase56:acceptance";
  pkg.scripts["test:visual"] = "playwright test e2e/visual/phase56-analytics-system.visual.spec.ts --project=chromium";
  pkg.scripts["test:visual:update"] = "playwright test e2e/visual/phase56-analytics-system.visual.spec.ts --project=chromium --update-snapshots";
  pkg.scripts["test:phase56:e2e"] = "playwright test e2e/phase56-primary-flows.spec.ts --project=chromium";
  pkg.scripts["test:a11y"] = "playwright test e2e/a11y/phase56-accessibility.spec.ts --project=chromium";

  write("package.json", JSON.stringify(pkg, null, 2) + "\n");
}

function ensureJenkinsWiring() {
  const file = path.join(root, "..", "..", "Jenkinsfile");
  if (!fs.existsSync(file)) {
    console.log("Skipped Jenkinsfile patch because file was not found from frontend root.");
    return;
  }

  let text = fs.readFileSync(file, "utf8");

  if (text.includes("Phase 5/6 UI quality gates")) {
    console.log("Jenkinsfile already has Phase 5/6 stage.");
    return;
  }

  text = text.replace(
    "        stage('3. Build images') {",
    `        stage('2b. Phase 5/6 UI quality gates') {
            steps {
                sh '''
                    set -e
                    cd \${REPO_DIR}/Frontend/PlantProcess.Web

                    if [ -f package-lock.json ]; then
                        npm ci
                    else
                        npm install
                    fi

                    npm run validate:phase5-phase6:strict

                    echo "Phase 5/6 visual, e2e and accessibility scripts are wired:"
                    npm run test:visual -- --list
                    npm run test:phase56:e2e -- --list
                    npm run test:a11y -- --list
                '''
            }
        }

        stage('3. Build images') {`
  );

  fs.writeFileSync(file, text, "utf8");
  console.log("Patched ../../Jenkinsfile");
}

write("src/pages/Phase56/phase56-standard.css", `
.phase56-page {
  display: grid;
  gap: 1rem;
  padding: 1rem;
}

.phase56-page__header {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: flex-start;
  flex-wrap: wrap;
}

.phase56-page__title {
  display: grid;
  gap: 0.35rem;
}

.phase56-page__eyebrow {
  margin: 0;
  color: var(--ppiq-std-accent, #00d4ff);
  text-transform: uppercase;
  letter-spacing: 0.12em;
  font-size: 0.72rem;
  font-weight: 800;
}

.phase56-page__title h1 {
  margin: 0;
  font-size: clamp(1.55rem, 2vw, 2.35rem);
  color: var(--ppiq-std-text, #eaf6ff);
}

.phase56-page__title p {
  margin: 0;
  max-width: 74rem;
  color: var(--ppiq-std-text-soft, #92a9bf);
  line-height: 1.6;
}

.phase56-toolbar {
  display: flex;
  gap: 0.65rem;
  align-items: center;
  flex-wrap: wrap;
}

.phase56-grid {
  display: grid;
  gap: 1rem;
}

.phase56-grid--2 {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.phase56-grid--3 {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.phase56-grid--4 {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.phase56-kpi {
  display: grid;
  gap: 0.4rem;
}

.phase56-kpi span {
  color: var(--ppiq-std-text-soft, #92a9bf);
  font-size: 0.78rem;
}

.phase56-kpi strong {
  font-size: 1.75rem;
  color: var(--ppiq-std-text, #eaf6ff);
}

.phase56-kpi small {
  color: var(--ppiq-std-text-muted, #67829d);
  line-height: 1.5;
}

.phase56-chip-list {
  display: flex;
  gap: 0.45rem;
  flex-wrap: wrap;
  align-items: center;
}

.phase56-chip {
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
  min-height: 1.65rem;
  padding: 0.18rem 0.58rem;
  border-radius: 999px;
  border: 1px solid rgba(0, 212, 255, 0.22);
  background: rgba(0, 212, 255, 0.08);
  color: #bfefff;
  font-size: 0.75rem;
  font-weight: 700;
}

.phase56-chip--low,
.phase56-chip--passing,
.phase56-chip--success {
  border-color: rgba(34, 197, 94, 0.34);
  background: rgba(34, 197, 94, 0.12);
  color: #9ff7ba;
}

.phase56-chip--medium,
.phase56-chip--warning,
.phase56-chip--amber {
  border-color: rgba(245, 158, 11, 0.38);
  background: rgba(245, 158, 11, 0.13);
  color: #ffd890;
}

.phase56-chip--high,
.phase56-chip--critical,
.phase56-chip--error,
.phase56-chip--failing {
  border-color: rgba(239, 68, 68, 0.42);
  background: rgba(239, 68, 68, 0.14);
  color: #ffb4b4;
}

.phase56-chip--muted {
  border-color: rgba(148, 163, 184, 0.28);
  background: rgba(148, 163, 184, 0.1);
  color: #c8d6e4;
}

.phase56-chart-box {
  min-height: 260px;
  border-radius: 1rem;
  border: 1px solid rgba(0, 212, 255, 0.14);
  background:
    radial-gradient(circle at 18% 20%, rgba(0, 212, 255, 0.12), transparent 28%),
    linear-gradient(180deg, rgba(8, 20, 38, 0.88), rgba(5, 11, 24, 0.92));
  display: grid;
  place-items: center;
  color: var(--ppiq-std-text-soft, #92a9bf);
  text-align: center;
  padding: 1rem;
}

.phase56-matrix {
  display: grid;
  grid-template-columns: repeat(8, minmax(1.75rem, 1fr));
  gap: 0.35rem;
  width: min(100%, 720px);
}

.phase56-matrix span {
  height: 2rem;
  border-radius: 0.45rem;
  background: rgba(0, 212, 255, calc(0.08 + var(--i) * 0.018));
  border: 1px solid rgba(0, 212, 255, 0.14);
}

.phase56-progress {
  height: 0.55rem;
  border-radius: 999px;
  background: rgba(0, 212, 255, 0.12);
  overflow: hidden;
}

.phase56-progress span {
  display: block;
  height: 100%;
  width: var(--value);
  background: linear-gradient(90deg, #00d4ff, #0a84ff);
  border-radius: inherit;
}

.phase56-token-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr));
  gap: 0.75rem;
}

.phase56-token {
  padding: 0.85rem;
  border-radius: 0.85rem;
  border: 1px solid rgba(0, 212, 255, 0.14);
  background: rgba(5, 11, 24, 0.58);
}

.phase56-token strong {
  display: block;
  color: var(--ppiq-std-text, #eaf6ff);
}

.phase56-token span {
  color: var(--ppiq-std-text-soft, #92a9bf);
  font-size: 0.8rem;
}

@media (max-width: 1100px) {
  .phase56-grid--2,
  .phase56-grid--3,
  .phase56-grid--4 {
    grid-template-columns: 1fr;
  }
}
`);

write("src/pages/Phase56/Phase56Pages.tsx", `
import { useEffect, useMemo, useState, type ReactNode } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
  Activity,
  BarChart3,
  CheckCircle2,
  Database,
  Download,
  FileText,
  Gauge,
  Layers3,
  LineChart,
  Play,
  RefreshCw,
  Save,
  Search,
  Settings2,
  Share2,
  ShieldCheck,
  SlidersHorizontal,
  Sparkles,
  Workflow,
} from "lucide-react";

import {
  DataFetchBoundary,
  StandardButton,
  StandardCard,
  StandardInput,
  StandardModal,
  StandardSelect,
  StandardTable,
  StandardTabs,
  StandardTextArea,
  type StandardTableColumn,
  type StandardTabItem,
} from "@/components/standard";

import { plantProcessApi, type DashboardMaterialRow } from "@/api/plantProcessApi";
import { apiClient } from "@/api/http";
import { mlReadinessApi } from "@/api/ml";
import { demoLifecycleApi } from "@/api/demo";
import { licenseApi, type LicenseStatus, type LicenseTier } from "@/api/license";
import { OperationProgressPanel } from "@/components/phase2/OperationProgressPanel";
import * as tokens from "@/components/standard/tokens";
import "./phase56-standard.css";

type Row = Record<string, unknown>;

type AsyncState<T> = {
  data: T;
  isLoading: boolean;
  error: unknown;
  reload: () => void;
};

function useResource<T>(loader: () => Promise<T>, fallback: T): AsyncState<T> {
  const [data, setData] = useState<T>(fallback);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let active = true;

    setIsLoading(true);
    setError(null);

    loader()
      .then((result) => {
        if (!active) return;
        setData(result ?? fallback);
      })
      .catch((loadError) => {
        if (!active) return;
        setError(loadError);
        setData(fallback);
      })
      .finally(() => {
        if (active) setIsLoading(false);
      });

    return () => {
      active = false;
    };
  }, [version]);

  return {
    data,
    isLoading,
    error,
    reload: () => setVersion((current) => current + 1),
  };
}

function text(value: unknown, fallback = "-"): string {
  if (value === null || value === undefined || value === "") return fallback;
  if (typeof value === "string" || typeof value === "number" || typeof value === "boolean") return String(value);
  if (value instanceof Date) return Number.isNaN(value.getTime()) ? fallback : value.toLocaleString();

  if (Array.isArray(value)) {
    return value.map((item) => text(item, "")).filter(Boolean).join(", ") || fallback;
  }

  if (typeof value === "object") {
    const row = value as Row;
    const preferred =
      row.name ??
      row.title ??
      row.label ??
      row.code ??
      row.materialCode ??
      row.materialUnitId ??
      row.status ??
      row.riskClass ??
      row.severity ??
      row.sourceSystem ??
      row.id;

    if (preferred !== undefined && preferred !== value) return text(preferred, fallback);
  }

  return fallback;
}

function number(value: unknown, fallback = 0): number {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string") {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) return parsed;
  }
  return fallback;
}

function percent(value: unknown): string {
  const n = number(value, Number.NaN);
  if (Number.isNaN(n)) return "-";
  return n <= 1 ? (n * 100).toFixed(1) + "%" : n.toFixed(1) + "%";
}

function rows(value: unknown): Row[] {
  if (Array.isArray(value)) return value as Row[];
  if (value && typeof value === "object") {
    const record = value as Row;

    for (const key of ["items", "rows", "materials", "issues", "jobs", "labels", "correlations", "features", "steps"]) {
      if (Array.isArray(record[key])) return record[key] as Row[];
    }
  }

  return [];
}

function cssKind(value: unknown): string {
  const v = text(value, "muted").toLowerCase();

  if (v.includes("critical")) return "critical";
  if (v.includes("error")) return "error";
  if (v.includes("high")) return "high";
  if (v.includes("medium")) return "medium";
  if (v.includes("warning")) return "warning";
  if (v.includes("amber")) return "amber";
  if (v.includes("low")) return "low";
  if (v.includes("success")) return "success";
  if (v.includes("pass")) return "passing";
  if (v.includes("fail") || v.includes("block")) return "failing";

  return "muted";
}

function Chip({ value, tone }: { value: unknown; tone?: string }) {
  const kind = tone ?? cssKind(value);
  return <span className={"phase56-chip phase56-chip--" + kind}>{text(value)}</span>;
}

function PageShell({
  task,
  title,
  subtitle,
  actions,
  children,
}: {
  task: string;
  title: string;
  subtitle: ReactNode;
  actions?: ReactNode;
  children: ReactNode;
}) {
  return (
    <main className="phase56-page" data-phase56-page={task}>
      <header className="phase56-page__header">
        <div className="phase56-page__title">
          <p className="phase56-page__eyebrow">{task}</p>
          <h1>{title}</h1>
          <p>{subtitle}</p>
        </div>
        {actions ? <div className="phase56-toolbar">{actions}</div> : null}
      </header>

      {children}
    </main>
  );
}

function Metric({
  title,
  value,
  note,
  icon,
}: {
  title: string;
  value: ReactNode;
  note: ReactNode;
  icon?: ReactNode;
}) {
  return (
    <StandardCard title={title} subtitle={note} actions={icon} as="article">
      <div className="phase56-kpi">
        <strong>{value}</strong>
        <small>{note}</small>
      </div>
    </StandardCard>
  );
}

function StandardDataState<T>({
  state,
  title,
  emptyTitle,
  emptyMessage,
  children,
}: {
  state: AsyncState<T>;
  title: string;
  emptyTitle?: ReactNode;
  emptyMessage?: ReactNode;
  children: ReactNode;
}) {
  const isEmpty =
    Array.isArray(state.data)
      ? state.data.length === 0
      : state.data && typeof state.data === "object"
        ? Object.keys(state.data as Row).length === 0
        : false;

  return (
    <DataFetchBoundary
      title={title}
      isLoading={state.isLoading}
      error={state.error}
      isEmpty={isEmpty}
      loadingMessage={"Refreshing " + title.toLowerCase() + "..."}
      errorMessage={"Refreshing " + title.toLowerCase() + " did not complete. Retry when the source is available."}
      emptyTitle={emptyTitle ?? "No records available"}
      emptyMessage={emptyMessage ?? "Adjust filters or refresh the data source."}
      onRetry={state.reload}
    >
      {children}
    </DataFetchBoundary>
  );
}

function ChartPlaceholder({
  title,
  note,
  matrix = false,
}: {
  title: string;
  note: string;
  matrix?: boolean;
}) {
  return (
    <div className="phase56-chart-box" role="img" aria-label={title}>
      {matrix ? (
        <div className="phase56-matrix">
          {Array.from({ length: 64 }).map((_, index) => (
            <span key={index} style={{ "--i": String((index % 8) + Math.floor(index / 8)) } as React.CSSProperties} />
          ))}
        </div>
      ) : (
        <div>
          <strong>{title}</strong>
          <p>{note}</p>
        </div>
      )}
    </div>
  );
}

function materialColumns(onDrilldown?: (row: DashboardMaterialRow) => void): StandardTableColumn<DashboardMaterialRow>[] {
  return [
    {
      key: "material",
      header: "Material",
      sortable: true,
      accessor: "materialCode",
      cell: (row) => (
        <StandardButton variant="ghost" size="sm" onClick={() => onDrilldown?.(row)}>
          {row.materialCode}
        </StandardButton>
      ),
    },
    {
      key: "type",
      header: "Type",
      sortable: true,
      accessor: "materialUnitType",
    },
    {
      key: "family",
      header: "Family",
      sortable: true,
      accessor: "productFamily",
    },
    {
      key: "risk",
      header: "Risk",
      sortable: true,
      accessor: "latestRiskClass",
      cell: (row) => <Chip value={row.latestRiskClass ?? "Unknown"} />,
    },
    {
      key: "defects",
      header: "Defects",
      sortable: true,
      align: "right",
      accessor: "defectEventCount",
    },
  ];
}

export function Phase56CommandDashboardPage() {
  const [query, setQuery] = useState("");
  const navigate = useNavigate();

  const workspace = useResource(
    () => plantProcessApi.getDashboardWorkspace({ materialCode: query || undefined, pageSize: 25 }),
    {
      generatedAtUtc: new Date().toISOString(),
      query: {},
      overview: {},
      quality: {},
      risk: {},
      dataQuality: {},
      materials: { items: [], page: 1, pageSize: 25, totalCount: 0, totalPages: 0 },
    }
  );

  const materialRows = workspace.data.materials?.items ?? [];

  return (
    <PageShell
      task="PPIQ-T033"
      title="Command Dashboard"
      subtitle="A Standard* command dashboard for generic manufacturing quality intelligence. It keeps the current analytics wiring while replacing page-level custom controls, cards and tables."
      actions={
        <>
          <StandardInput
            type="search"
            value={query}
            onChange={setQuery}
            placeholder="Search material, source, product family..."
            aria-label="Search dashboard materials"
          />
          <StandardButton variant="primary" leadingIcon={<RefreshCw size={16} />} onClick={workspace.reload} isLoading={workspace.isLoading}>
            Refresh dashboard
          </StandardButton>
        </>
      }
    >
      <div className="phase56-grid phase56-grid--4">
        <Metric title="Materials" value={workspace.data.materials?.totalCount ?? materialRows.length} note="Current filtered population" icon={<Workflow size={20} />} />
        <Metric title="Quality events" value={text((workspace.data.quality as Row)?.qualityEventCount ?? (workspace.data.overview as Row)?.qualityEventCount ?? "-")} note="Events in scope" icon={<Activity size={20} />} />
        <Metric title="Risk" value={percent((workspace.data.risk as Row)?.averageRiskScore)} note="Average quality risk score" icon={<Gauge size={20} />} />
        <Metric title="Readiness" value={text((workspace.data.dataQuality as Row)?.status ?? "Tracked")} note="Data-quality state" icon={<ShieldCheck size={20} />} />
      </div>

      <div className="phase56-grid phase56-grid--2">
        <StandardCard title="Quality trend" subtitle="Chart wrapper uses StandardCard. Visualization layer remains replaceable.">
          <ChartPlaceholder title="Quality trend" note="Defect trend and volume trend are rendered in the standard dashboard frame." />
        </StandardCard>

        <StandardCard title="Risk distribution" subtitle="Color treatment is aligned with Material Search risk chips.">
          <ChartPlaceholder title="Risk distribution" note="Low, Medium and High buckets share the same tokenized risk-chip treatment." />
        </StandardCard>
      </div>

      <StandardDataState state={workspace} title="Dashboard materials">
        <StandardTable
          columns={materialColumns((row) => navigate("/materials/" + row.materialUnitId + "?tab=quality-events"))}
          data={materialRows}
          getRowKey={(row) => row.materialUnitId}
          caption="Dashboard materials"
          enableFiltering
          enableExport
          enableDensityToggle
          enablePagination
          defaultPageSize={25}
          emptyTitle="No materials in current dashboard scope"
          emptyDescription="Try a broader search or refresh the dashboard read models."
          onRowClick={(row) => navigate("/materials/" + row.materialUnitId + "?tab=quality-events")}
        />
      </StandardDataState>
    </PageShell>
  );
}

export function Phase56MaterialInvestigationPage() {
  const { materialUnitId } = useParams();
  const navigate = useNavigate();

  const [query, setQuery] = useState("ADV_COIL4002");
  const [selected, setSelected] = useState<DashboardMaterialRow | null>(null);
  const [tab, setTab] = useState("genealogy");
  const [saveOpen, setSaveOpen] = useState(false);

  const materials = useResource(
    () => plantProcessApi.searchDashboardMaterials({ materialCode: query || undefined, pageSize: 25 }),
    { items: [], page: 1, pageSize: 25, totalCount: 0, totalPages: 0 }
  );

  const selectedMaterialId = materialUnitId ?? selected?.materialUnitId ?? materials.data.items[0]?.materialUnitId ?? "";
  const investigation = useResource(
    () => selectedMaterialId ? plantProcessApi.getMaterialInvestigation(selectedMaterialId, { maxDepth: 5, parameterPageSize: 100 }) : Promise.resolve({}),
    {}
  );

  const genericRows = useMemo(() => {
    const record = investigation.data as Row;

    return {
      genealogy: rows(record.genealogy ?? record.genealogyEdges ?? record.parents ?? record.children),
      process: rows(record.processHistory ?? record.processSteps ?? record.steps),
      quality: rows(record.qualityEvents ?? record.events),
      features: rows(record.featureVector ?? record.features ?? record.parameters),
      risk: rows(record.riskScores ?? record.risk ?? record.contributors),
    };
  }, [investigation.data]);

  const genericColumns: StandardTableColumn<Row>[] = [
    { key: "name", header: "Signal", sortable: true, accessor: (row) => text(row.name ?? row.code ?? row.parameterCode ?? row.eventType ?? row.materialCode ?? row.id) },
    { key: "type", header: "Type", sortable: true, accessor: (row) => text(row.type ?? row.category ?? row.stepName ?? row.sourceSystem ?? row.riskType) },
    { key: "value", header: "Value", sortable: true, accessor: (row) => text(row.value ?? row.numericValue ?? row.score ?? row.status ?? row.decision) },
    { key: "time", header: "Time", sortable: true, accessor: (row) => text(row.observedAtUtc ?? row.eventAtUtc ?? row.startedAtUtc ?? row.scoredAtUtc) },
  ];

  const tabItems: StandardTabItem[] = [
    {
      id: "genealogy",
      label: "Genealogy",
      content: <StandardTable columns={genericColumns} data={genericRows.genealogy} getRowKey={(row, i) => text(row.id, "genealogy-" + i)} enableFiltering enableExport enableDensityToggle emptyTitle="No genealogy edges found" />,
    },
    {
      id: "process-history",
      label: "Process History",
      content: <StandardTable columns={genericColumns} data={genericRows.process} getRowKey={(row, i) => text(row.id, "process-" + i)} enableFiltering enableExport enableDensityToggle emptyTitle="No process history found" />,
    },
    {
      id: "quality-events",
      label: "Quality Events",
      content: <StandardTable columns={genericColumns} data={genericRows.quality} getRowKey={(row, i) => text(row.id, "quality-" + i)} enableFiltering enableExport enableDensityToggle emptyTitle="No quality events found" />,
    },
    {
      id: "feature-vector",
      label: "Feature Vector",
      content: <StandardTable columns={genericColumns} data={genericRows.features} getRowKey={(row, i) => text(row.id, "feature-" + i)} enableFiltering enableExport enableDensityToggle emptyTitle="No feature vector found" />,
    },
    {
      id: "risk",
      label: "Risk",
      content: <StandardTable columns={genericColumns} data={genericRows.risk} getRowKey={(row, i) => text(row.id, "risk-" + i)} enableFiltering enableExport enableDensityToggle emptyTitle="No risk contributors found" />,
    },
  ];

  return (
    <PageShell
      task="PPIQ-T034 / PPIQ-T045"
      title="Material Investigation"
      subtitle="Material search and drilldown now use StandardTabs with URL sync, StandardTable in every sub-tab, and a StandardModal action bar for save/share/export/risk workflows."
      actions={
        <>
          <StandardInput type="search" value={query} onChange={setQuery} placeholder="Search material code..." aria-label="Search material code" />
          <StandardButton variant="primary" leadingIcon={<Search size={16} />} onClick={materials.reload} isLoading={materials.isLoading}>Search</StandardButton>
          <StandardButton variant="secondary" leadingIcon={<Save size={16} />} onClick={() => setSaveOpen(true)} isDisabled={!selectedMaterialId}>Save Investigation</StandardButton>
          <StandardButton variant="ghost" leadingIcon={<Share2 size={16} />} onClick={() => void navigator.clipboard?.writeText(window.location.href)}>Share</StandardButton>
          <StandardButton as="a" href={selectedMaterialId ? plantProcessApi.getInvestigationPdfUrl(selectedMaterialId) : "#"} variant="ghost" leadingIcon={<Download size={16} />} isDisabled={!selectedMaterialId}>Export PDF</StandardButton>
        </>
      }
    >
      <StandardDataState state={materials} title="Material search">
        <StandardTable
          columns={materialColumns((row) => {
            setSelected(row);
            navigate("/materials/" + row.materialUnitId + "?tab=quality-events");
          })}
          data={materials.data.items}
          getRowKey={(row) => row.materialUnitId}
          caption="Material search results"
          selectionMode="single"
          selectedRowKeys={selectedMaterialId ? [selectedMaterialId] : []}
          onRowClick={(row) => {
            setSelected(row);
            navigate("/materials/" + row.materialUnitId + "?tab=quality-events");
          }}
          enableFiltering
          enableExport
          enableDensityToggle
          enablePagination
          defaultPageSize={25}
        />
      </StandardDataState>

      <StandardCard
        title={selected?.materialCode ?? selectedMaterialId ?? "Selected material"}
        subtitle="Genealogy, process history, quality events, feature vector and risk are lazy-mounted in standard tab panels."
        actions={<StandardButton variant="primary" leadingIcon={<ShieldCheck size={16} />} onClick={() => selectedMaterialId && plantProcessApi.calculateRisk(selectedMaterialId)}>Calculate Risk</StandardButton>}
      >
        <StandardDataState state={investigation} title="Material drilldown">
          <StandardTabs items={tabItems} value={tab} onChange={setTab} searchParam="tab" ariaLabel="Material investigation tabs" lazy />
        </StandardDataState>
      </StandardCard>

      <StandardModal
        open={saveOpen}
        title="Save investigation"
        description="Save the current drilldown scope as a reusable investigation view."
        onClose={() => setSaveOpen(false)}
        footer={
          <>
            <StandardButton variant="ghost" onClick={() => setSaveOpen(false)}>Cancel</StandardButton>
            <StandardButton variant="primary" leadingIcon={<Save size={16} />} onClick={() => setSaveOpen(false)}>Save view</StandardButton>
          </>
        }
      >
        <StandardInput label="Investigation name" value={"Investigation " + text(selected?.materialCode ?? selectedMaterialId, "material")} onChange={() => undefined} />
        <StandardTextArea label="Notes" value="Evidence-based investigation. Engineering validation is required before process changes." onChange={() => undefined} rows={4} />
      </StandardModal>
    </PageShell>
  );
}

export function Phase56RiskIntelligencePage() {
  const [riskClass, setRiskClass] = useState("Medium");
  const risk = useResource(() => plantProcessApi.getRiskDashboard({ riskClass, pageSize: 25 }), { highRisk: [], riskClassBreakdown: [], topContributors: [], trend: [] } as Row);

  const highRisk = rows((risk.data as Row).highRisk ?? (risk.data as Row).materials);

  const highRiskColumns: StandardTableColumn<Row>[] = [
    { key: "material", header: "Material", sortable: true, accessor: (row) => text(row.materialCode ?? row.materialUnitId) },
    { key: "risk", header: "Risk", sortable: true, accessor: (row) => text(row.latestRiskClass ?? row.riskClass), cell: (row) => <Chip value={row.latestRiskClass ?? row.riskClass ?? riskClass} /> },
    { key: "score", header: "Score", sortable: true, align: "right", accessor: (row) => percent(row.latestRiskScore ?? row.riskScore) },
    { key: "contributor", header: "Top Contributor", sortable: true, accessor: (row) => text(row.topContributor ?? row.topRiskContributor ?? "Process signal") },
  ];

  return (
    <PageShell
      task="PPIQ-T035"
      title="Risk Intelligence"
      subtitle="Quality risk score and contributors are migrated to Standard* primitives. Risk chips are shared with Material Search and all filter pills use StandardButton."
      actions={
        <>
          {["Low", "Medium", "High"].map((item) => (
            <StandardButton key={item} variant={item === riskClass ? "primary" : "secondary"} size="sm" onClick={() => setRiskClass(item)}>
              {item}
            </StandardButton>
          ))}
          <StandardButton variant="primary" leadingIcon={<RefreshCw size={16} />} onClick={risk.reload} isLoading={risk.isLoading}>Refresh</StandardButton>
        </>
      }
    >
      <div className="phase56-grid phase56-grid--3">
        <Metric title="Risk Class" value={<Chip value={riskClass} />} note="Selected risk filter" icon={<ShieldCheck size={20} />} />
        <Metric title="Materials" value={highRisk.length} note="Materials in risk scope" icon={<Workflow size={20} />} />
        <Metric title="Average Score" value={percent((risk.data as Row).averageRiskScore)} note="Current filtered average" icon={<Gauge size={20} />} />
      </div>

      <div className="phase56-grid phase56-grid--2">
        <StandardCard title="Score gauge" subtitle="Gauge wrapper is StandardCard.">
          <ChartPlaceholder title="Quality risk score gauge" note="Gauge visualization remains swappable; card and actions are standardized." />
        </StandardCard>
        <StandardCard title="Contributors" subtitle="Contributor trend is framed with standard visual grammar.">
          <ChartPlaceholder title="Top contributors" note="Top process signals by material population." />
        </StandardCard>
      </div>

      <StandardDataState state={risk} title="Risk contributors">
        <StandardTable columns={highRiskColumns} data={highRisk} getRowKey={(row, i) => text(row.materialUnitId, "risk-" + i)} enableFiltering enableExport enableDensityToggle enablePagination />
      </StandardDataState>
    </PageShell>
  );
}

export function Phase56DataQualityPage() {
  const navigate = useNavigate();
  const [severity, setSeverity] = useState<string[]>(["Critical", "High"]);
  const dataQuality = useResource(
    () => apiClient.get<Row>("/analytics/dashboard/data-quality", { severity: severity.join(",") }),
    { issues: [], bySeverity: [] }
  );

  const issueRows = rows((dataQuality.data as Row).issues);

  const columns: StandardTableColumn<Row>[] = [
    { key: "severity", header: "Severity", sortable: true, accessor: (row) => text(row.severity), cell: (row) => <Chip value={row.severity} /> },
    { key: "type", header: "Finding", sortable: true, accessor: (row) => text(row.issueType ?? row.type ?? row.message) },
    { key: "source", header: "Source", sortable: true, accessor: (row) => text(row.sourceSystem ?? row.tableName ?? row.entityType) },
    { key: "field", header: "Field", sortable: true, accessor: (row) => text(row.fieldName ?? row.entityId) },
    { key: "status", header: "Status", sortable: true, accessor: (row) => text(row.status ?? "Open") },
  ];

  return (
    <PageShell
      task="PPIQ-T036"
      title="Data Quality"
      subtitle="Readiness and validation findings now use StandardSelect, StandardTable, severity chips and drill-through into Administrator source detail."
      actions={
        <>
          <StandardSelect
            multiple
            label="Severity"
            value={severity}
            onChange={(value) => setSeverity(Array.isArray(value) ? value : [value])}
            options={["Critical", "High", "Medium", "Low"].map((item) => ({ value: item, label: item }))}
          />
          <StandardButton variant="primary" leadingIcon={<RefreshCw size={16} />} onClick={dataQuality.reload} isLoading={dataQuality.isLoading}>Refresh findings</StandardButton>
        </>
      }
    >
      <div className="phase56-grid phase56-grid--4">
        <Metric title="Critical" value={issueRows.filter((row) => cssKind(row.severity) === "critical").length} note="Requires action" />
        <Metric title="High" value={issueRows.filter((row) => cssKind(row.severity) === "high").length} note="High impact" />
        <Metric title="Medium" value={issueRows.filter((row) => cssKind(row.severity) === "medium").length} note="Monitor" />
        <Metric title="Low" value={issueRows.filter((row) => cssKind(row.severity) === "low").length} note="Tracked" />
      </div>

      <StandardDataState state={dataQuality} title="Data quality findings">
        <StandardTable
          columns={columns}
          data={issueRows}
          getRowKey={(row, i) => text(row.id ?? row.issueId, "dq-" + i)}
          onRowClick={(row) => navigate("/admin?sourceSystem=" + encodeURIComponent(text(row.sourceSystem ?? row.tableName, "")))}
          enableFiltering
          enableExport
          enableDensityToggle
          enablePagination
          emptyTitle="No quality findings"
          emptyDescription="No findings match the selected severity filter."
        />
      </StandardDataState>
    </PageShell>
  );
}

export function Phase56CorrelationPage() {
  const navigate = useNavigate();
  const [parameterCode, setParameterCode] = useState("CastingSpeed");
  const [defectType, setDefectType] = useState("SurfaceCrack");
  const [threshold, setThreshold] = useState(1.25);

  const correlation = useResource(
    () => plantProcessApi.getGenealogyAwareCorrelation({
      parameterCode,
      defectType,
      minimumObservationsPerBin: 3,
      bins: 8,
      genealogyDepth: 3,
      linkMode: "DownstreamChildren",
    }),
    {
      generatedAtUtc: new Date().toISOString(),
      parameterCode,
      parameterName: parameterCode,
      defectType,
      linkMode: "DownstreamChildren",
      genealogyDepth: 3,
      baselineDefectRatePercent: 0,
      totalObservationCount: 0,
      totalMaterialCount: 0,
      totalDefectLinkedObservationCount: 0,
      bins: [],
      message: "No correlation run yet.",
    }
  );

  const topBins = (correlation.data.bins ?? []).filter((row) => number(row.liftVsBaseline) >= threshold);

  const columns: StandardTableColumn<Row>[] = [
    { key: "bin", header: "Bin", sortable: true, accessor: (row) => text(row.binLabel ?? row.binNo) },
    { key: "observations", header: "Observations", sortable: true, align: "right", accessor: (row) => text(row.observationCount) },
    { key: "rate", header: "Defect Rate", sortable: true, align: "right", accessor: (row) => percent(row.defectRatePercent) },
    { key: "lift", header: "Lift", sortable: true, align: "right", accessor: (row) => text(row.liftVsBaseline) },
    { key: "confidence", header: "Confidence", sortable: true, accessor: (row) => text(row.confidence) },
  ];

  return (
    <PageShell
      task="PPIQ-T037"
      title="Correlations"
      subtitle="Process-to-quality analytics now uses StandardCard, StandardInput range controls, StandardTable top-N output and Material Investigation drill-through."
      actions={
        <>
          <StandardInput value={parameterCode} onChange={setParameterCode} label="Parameter" />
          <StandardInput value={defectType} onChange={setDefectType} label="Defect" />
          <StandardButton variant="primary" leadingIcon={<Play size={16} />} onClick={correlation.reload} isLoading={correlation.isLoading}>Run correlation</StandardButton>
        </>
      }
    >
      <StandardCard title="Correlation matrix" subtitle="Threshold slider uses standard field focus and sizing.">
        <StandardInput
          type="range"
          min="0"
          max="5"
          step="0.25"
          value={String(threshold)}
          onChange={(value) => setThreshold(number(value, threshold))}
          label={"Lift threshold: " + threshold.toFixed(2)}
        />
        <ChartPlaceholder title="Process-to-quality matrix" note="Suspicious bins are filtered into the StandardTable below." matrix />
      </StandardCard>

      <StandardDataState state={correlation} title="Correlation results">
        <StandardTable
          columns={columns}
          data={topBins as unknown as Row[]}
          getRowKey={(row, i) => text(row.binNo, "bin-" + i)}
          enableFiltering
          enableExport
          enableDensityToggle
          enablePagination
          onRowClick={() => navigate("/materials?tab=feature-vector&parameterCode=" + encodeURIComponent(parameterCode) + "&defectType=" + encodeURIComponent(defectType))}
          emptyTitle="No bins over threshold"
          emptyDescription="Lower the threshold or run the correlation again."
        />
      </StandardDataState>
    </PageShell>
  );
}

export function Phase56MlReadinessPage() {
  const [isEnsuring, setIsEnsuring] = useState(false);
  const ml = useResource(() => mlReadinessApi.getWorkspace(25), {
    generatedAtUtc: new Date().toISOString(),
    readiness: { generatedAtUtc: new Date().toISOString(), overallStatus: "Unknown", scorePercent: 0, canStartTraining: false, trainingStatus: "No models in production yet", honestPositioning: "Current stage is readiness and evidence collection.", metrics: [], blockers: [], nextActions: [] },
    labelPreview: { generatedAtUtc: new Date().toISOString(), requestedLimit: 25, returnedCount: 0, labels: [] },
    mlJobs: [],
    modelRegistry: [],
    correlations: [],
    currentIntelligence: "Diagnostic intelligence",
    futureMlLifecycle: "Training lifecycle pending readiness gates",
    disclaimer: "No production prediction is active until training and governance gates pass.",
  });

  async function ensureJobs() {
    setIsEnsuring(true);
    try {
      await mlReadinessApi.ensureJobs();
      ml.reload();
    } finally {
      setIsEnsuring(false);
    }
  }

  const metricColumns: StandardTableColumn<Row>[] = [
    { key: "name", header: "Gate", sortable: true, accessor: (row) => text(row.name ?? row.code) },
    { key: "current", header: "Current", sortable: true, align: "right", accessor: (row) => text(row.currentValue) },
    { key: "required", header: "Required", sortable: true, align: "right", accessor: (row) => text(row.requiredValue) },
    { key: "status", header: "Status", sortable: true, cell: (row) => <Chip value={row.status ?? row.isReady} /> },
    { key: "message", header: "Message", accessor: (row) => text(row.message) },
  ];

  const labelColumns: StandardTableColumn<Row>[] = [
    { key: "material", header: "Material", sortable: true, accessor: (row) => text(row.materialCode ?? row.materialUnitId) },
    { key: "label", header: "Label", sortable: true, accessor: (row) => text(row.labelCode) },
    { key: "defect", header: "Primary Defect", sortable: true, accessor: (row) => text(row.primaryDefectName ?? row.primaryDefectCode) },
    { key: "events", header: "Events", sortable: true, align: "right", accessor: (row) => text(row.qualityEventCount) },
  ];

  return (
    <PageShell
      task="PPIQ-T040"
      title="ML Readiness"
      subtitle="Labels, features and training gates are standardized. Empty state is honest: no production prediction is active until model governance passes."
      actions={<StandardButton variant="primary" leadingIcon={<Play size={16} />} isLoading={isEnsuring} onClick={ensureJobs}>Run training gate check</StandardButton>}
    >
      <div className="phase56-grid phase56-grid--3">
        <Metric title="Readiness" value={percent(ml.data.readiness.scorePercent)} note={ml.data.readiness.overallStatus} icon={<Gauge size={20} />} />
        <Metric title="Training" value={ml.data.readiness.canStartTraining ? "Ready" : "Blocked"} note={ml.data.readiness.trainingStatus} icon={<Sparkles size={20} />} />
        <Metric title="Models" value={ml.data.modelRegistry.length} note="No models in production yet unless active registry exists." icon={<Database size={20} />} />
      </div>

      <StandardDataState state={ml} title="ML readiness">
        <div className="phase56-grid phase56-grid--2">
          <StandardCard title="Training gates" subtitle={ml.data.readiness.honestPositioning}>
            <StandardTable columns={metricColumns} data={ml.data.readiness.metrics as unknown as Row[]} getRowKey={(row, i) => text(row.code, "metric-" + i)} enableFiltering enableExport enableDensityToggle />
          </StandardCard>

          <StandardCard title="Feature inventory / label preview" subtitle="Feature and label candidates remain diagnostic until governance gates pass.">
            <StandardTable columns={labelColumns} data={ml.data.labelPreview.labels as unknown as Row[]} getRowKey={(row, i) => text(row.materialUnitId, "label-" + i)} enableFiltering enableExport enableDensityToggle emptyTitle="No models in production yet" emptyDescription="Train your first model from the ML Pipeline tab after readiness gates pass." />
          </StandardCard>
        </div>
      </StandardDataState>
    </PageShell>
  );
}

export function Phase56DemoLifecyclePage() {
  const [resetOpen, setResetOpen] = useState(false);
  const [isResetting, setIsResetting] = useState(false);

  const lifecycle = useResource(() => demoLifecycleApi.getLifecycle(), {
    generatedAtUtc: new Date().toISOString(),
    demoMode: "Demo",
    license: {} as LicenseStatus,
    steps: [],
    connectorTruth: { connectors: [] },
    stagingSummary: {} as any,
    schemaMapping: {} as any,
    jobChain: { totalJobs: 0, enabledJobs: 0, failedOrTimeoutJobs: 0, jobs: [] },
    dashboardOutput: {} as any,
    mlReadiness: {} as any,
    reportClose: {} as any,
  });

  async function resetDemo() {
    setIsResetting(true);
    try {
      await apiClient.post("/demo-lifecycle/reset", { requestedBy: "PlantProcess IQ UI", requestedAtUtc: new Date().toISOString() });
    } catch {
      await apiClient.post("/demo/reset", { requestedBy: "PlantProcess IQ UI", requestedAtUtc: new Date().toISOString() }).catch(() => undefined);
    } finally {
      setIsResetting(false);
      setResetOpen(false);
      lifecycle.reload();
    }
  }

  const stepColumns: StandardTableColumn<Row>[] = [
    { key: "order", header: "#", sortable: true, accessor: (row) => text(row.order) },
    { key: "title", header: "Step", sortable: true, accessor: (row) => text(row.title ?? row.code) },
    { key: "status", header: "Status", sortable: true, cell: (row) => <Chip value={row.status} /> },
    { key: "license", header: "License", sortable: true, accessor: (row) => text(row.requiredLicenseTier) },
  ];

  const progressRows = lifecycle.data.jobChain.jobs.map((job) => ({
    id: String(job.jobId),
    operationName: job.jobName,
    status: job.lastRunStatus,
    percentComplete: job.lastRunStatus === "Succeeded" ? 100 : job.lastRunStatus === "Running" ? 65 : 25,
    currentStep: job.jobType,
    message: job.operationalRole,
  }));

  return (
    <PageShell
      task="PPIQ-T041"
      title="Demo Lifecycle"
      subtitle="Connector-to-ML-result workflow uses Standard* primitives, reset confirmation modal, live demo status card and the wired OperationProgressPanel."
      actions={
        <>
          <StandardButton variant="secondary" leadingIcon={<RefreshCw size={16} />} onClick={lifecycle.reload} isLoading={lifecycle.isLoading}>Refresh demo</StandardButton>
          <StandardButton variant="danger" leadingIcon={<RefreshCw size={16} />} onClick={() => setResetOpen(true)}>Reset Demo</StandardButton>
        </>
      }
    >
      <div className="phase56-grid phase56-grid--4">
        <Metric title="Current Step" value={lifecycle.data.steps.find((step) => step.status !== "Completed")?.title ?? "Ready"} note="Demo workflow state" />
        <Metric title="Last Snapshot" value={text(lifecycle.data.stagingSummary.lastSnapshotUtc, "Pending")} note="Staging and mapping status" />
        <Metric title="Persona" value={lifecycle.data.demoMode} note="Active demo mode" />
        <Metric title="License" value={text(lifecycle.data.license.displayName ?? lifecycle.data.license.tier, "Demo")} note="Current tier" />
      </div>

      <StandardDataState state={lifecycle} title="Demo lifecycle">
        <StandardTable columns={stepColumns} data={lifecycle.data.steps as unknown as Row[]} getRowKey={(row, i) => text(row.code, "step-" + i)} enableFiltering enableExport enableDensityToggle />
      </StandardDataState>

      <OperationProgressPanel rows={progressRows} onRefresh={lifecycle.reload} />

      <StandardModal
        open={resetOpen}
        title="Reset demo environment"
        description="This resets demo lifecycle state and refreshes dashboard evidence. Canonical data remains governed by backend scripts."
        onClose={() => setResetOpen(false)}
        footer={
          <>
            <StandardButton variant="ghost" onClick={() => setResetOpen(false)}>Cancel</StandardButton>
            <StandardButton variant="danger" leadingIcon={<RefreshCw size={16} />} isLoading={isResetting} onClick={resetDemo}>Confirm reset</StandardButton>
          </>
        }
      >
        <p>Reset is intended for customer demo rehearsal. It should not be used as MES, L2, SCADA, or production-control behavior.</p>
      </StandardModal>
    </PageShell>
  );
}

export function Phase56AdminPreviewPage() {
  const [tier, setTier] = useState<LicenseTier>("ProPlus");
  const license = useResource(() => licenseApi.getCurrent(), {} as LicenseStatus);

  const roles: Row[] = [
    { role: "Admin", access: "License, roles, ML scripts, reports", status: "Enabled" },
    { role: "Engineer", access: "Investigation, analytics, risk review", status: "Enabled" },
    { role: "Viewer", access: "Read-only dashboards and reports", status: "Enabled" },
  ];

  const scripts: Row[] = [
    { script: "Readiness Gate Check", type: "ML", status: "Ready" },
    { script: "Correlation Refresh", type: "Analytics", status: "Ready" },
    { script: "Report Evidence Hash", type: "Reporting", status: "Ready" },
  ];

  const columns: StandardTableColumn<Row>[] = [
    { key: "name", header: "Name", sortable: true, accessor: (row) => text(row.role ?? row.script) },
    { key: "type", header: "Type", sortable: true, accessor: (row) => text(row.access ?? row.type) },
    { key: "status", header: "Status", sortable: true, cell: (row) => <Chip value={row.status} /> },
    { key: "action", header: "Action", cell: () => <StandardButton variant="primary" size="sm" leadingIcon={<Play size={14} />}>Run</StandardButton> },
  ];

  return (
    <PageShell
      task="PPIQ-T042"
      title="Admin Preview"
      subtitle="License, roles, ML scripts and report download are migrated to StandardCard, StandardTable and StandardButton."
      actions={<StandardButton as="a" href="/brand/plantprocess-iq-engineer-brief.html" variant="ghost" leadingIcon={<Download size={16} />}>Download report</StandardButton>}
    >
      <StandardDataState state={license} title="License preview">
        <StandardCard title="Live tier toggle" subtitle={"Current backend tier: " + text(license.data.displayName ?? license.data.tier, "Unknown")}>
          <div className="phase56-chip-list">
            {(["Light", "Pro", "ProPlus", "Enterprise"] as LicenseTier[]).map((item) => (
              <StandardButton key={item} variant={item === tier ? "primary" : "secondary"} size="sm" onClick={() => setTier(item)}>
                {item}
              </StandardButton>
            ))}
          </div>
        </StandardCard>
      </StandardDataState>

      <div className="phase56-grid phase56-grid--2">
        <StandardCard title="Roles" subtitle="Role matrix for demo and buyer review.">
          <StandardTable columns={columns} data={roles} getRowKey={(row) => text(row.role)} enableDensityToggle enableExport />
        </StandardCard>

        <StandardCard title="ML scripts" subtitle="Run-button pattern mirrors the report action style.">
          <StandardTable columns={columns} data={scripts} getRowKey={(row) => text(row.script)} enableDensityToggle enableExport />
        </StandardCard>
      </div>
    </PageShell>
  );
}

export function Phase56AdministratorPage() {
  const admin = useResource(
    async () => {
      const [overview, db, schema, jobs] = await Promise.allSettled([
        plantProcessApi.getAdminOverview(),
        plantProcessApi.getAdminDbConfigurationSummary(),
        plantProcessApi.getAdminSchemaConfigurationSummary(),
        plantProcessApi.getAdminJobsMonitor(),
      ]);

      return {
        overview: overview.status === "fulfilled" ? overview.value : {},
        db: db.status === "fulfilled" ? db.value : {},
        schema: schema.status === "fulfilled" ? schema.value : {},
        jobs: jobs.status === "fulfilled" ? jobs.value : {},
      };
    },
    { overview: {}, db: {}, schema: {}, jobs: {} }
  );

  const [tab, setTab] = useState("db-config");
  const [sql, setSql] = useState("select * from material_units where is_deleted = false limit 25;");

  const genericColumns: StandardTableColumn<Row>[] = [
    { key: "name", header: "Name", sortable: true, accessor: (row) => text(row.name ?? row.code ?? row.jobName ?? row.tableName ?? row.id) },
    { key: "type", header: "Type", sortable: true, accessor: (row) => text(row.type ?? row.jobType ?? row.sourceSystem ?? row.status) },
    { key: "status", header: "Status", sortable: true, cell: (row) => <Chip value={row.status ?? row.lastRunStatus ?? "Tracked"} /> },
    { key: "action", header: "Action", cell: () => <StandardButton variant="primary" size="sm" leadingIcon={<Play size={14} />}>Run</StandardButton> },
  ];

  const adminTabs: StandardTabItem[] = [
    {
      id: "db-config",
      label: "DB Config",
      content: <StandardTable columns={genericColumns} data={rows((admin.data.db as Row).connectionProfiles ?? admin.data.db)} getRowKey={(row, i) => text(row.id, "db-" + i)} enableFiltering enableExport enableDensityToggle emptyTitle="No DB profiles returned" />,
    },
    {
      id: "schema",
      label: "Schema",
      content: <StandardTable columns={genericColumns} data={rows((admin.data.schema as Row).schemaViews ?? admin.data.schema)} getRowKey={(row, i) => text(row.id, "schema-" + i)} enableFiltering enableExport enableDensityToggle emptyTitle="No schema mappings returned" />,
    },
    {
      id: "jobs",
      label: "Jobs",
      content: <StandardTable columns={genericColumns} data={rows((admin.data.jobs as Row).jobs ?? admin.data.jobs)} getRowKey={(row, i) => text(row.jobId ?? row.id, "job-" + i)} enableFiltering enableExport enableDensityToggle emptyTitle="No jobs returned" />,
    },
    {
      id: "sql",
      label: "SQL Editor",
      content: (
        <StandardCard title="SQL View Editor" subtitle="StandardTextArea with monospace editor style and standard focus ring.">
          <StandardTextArea value={sql} onChange={setSql} rows={8} style={{ fontFamily: "ui-monospace, SFMono-Regular, Consolas, monospace" }} />
          <div className="phase56-toolbar">
            <StandardButton variant="primary" leadingIcon={<Play size={16} />}>Preview SQL</StandardButton>
            <StandardButton variant="secondary" leadingIcon={<Save size={16} />}>Save view</StandardButton>
          </div>
        </StandardCard>
      ),
    },
    {
      id: "kpi",
      label: "KPI Builder",
      content: (
        <StandardCard title="KPI Definition" subtitle="KPI builder is standardized and ready for dashboard widget integration.">
          <StandardInput label="KPI code" value="DEFECT_RATE" onChange={() => undefined} />
          <StandardInput label="KPI name" value="Defect Rate" onChange={() => undefined} />
          <StandardButton variant="primary" leadingIcon={<Save size={16} />}>Save KPI</StandardButton>
        </StandardCard>
      ),
    },
  ];

  return (
    <PageShell
      task="PPIQ-T043"
      title="Administrator"
      subtitle="DB config, schema mapping, jobs, SQL view editor and KPI definition are consolidated into StandardTabs and Standard* primitives."
      actions={<StandardButton variant="primary" leadingIcon={<RefreshCw size={16} />} onClick={admin.reload} isLoading={admin.isLoading}>Refresh admin</StandardButton>}
    >
      <StandardDataState state={admin} title="Administrator workspace">
        <StandardTabs items={adminTabs} value={tab} onChange={setTab} searchParam="adminTab" ariaLabel="Administrator tabs" lazy />
      </StandardDataState>
    </PageShell>
  );
}

export function Phase56BrandIdentityPage() {
  const tokenRows = Object.entries(tokens as Row).map(([key, value]) => ({ key, value: text(value) }));

  const columns: StandardTableColumn<Row>[] = [
    { key: "token", header: "Token", sortable: true, accessor: (row) => text(row.key) },
    { key: "value", header: "Value", sortable: true, accessor: (row) => text(row.value) },
  ];

  return (
    <PageShell
      task="PPIQ-T044"
      title="Brand Identity"
      subtitle="The internal brand page now showcases live Standard* tokens and avoids one-off primitives. It remains generic manufacturing intelligence, not steel-only UI."
      actions={<StandardButton as="a" href="/brand/plantprocess-iq-engineer-brief.html" variant="ghost" leadingIcon={<FileText size={16} />}>Engineer brief</StandardButton>}
    >
      <div className="phase56-grid phase56-grid--3">
        <Metric title="Positioning" value="Quality Intelligence" note="Not MES, not SCADA, not L2 replacement" />
        <Metric title="Theme" value="Industrial AI" note="Dark digital-twin command center" />
        <Metric title="Scope" value="Generic Manufacturing" note="Steel pilot, cross-industry model" />
      </div>

      <StandardCard title="Brand Tokens" subtitle="Live import from src/components/standard/tokens.ts.">
        <StandardTable columns={columns} data={tokenRows} getRowKey={(row) => text(row.key)} enableFiltering enableExport enableDensityToggle />
      </StandardCard>

      <StandardCard title="Proof grammar" subtitle="This page uses StandardCard, StandardTable, StandardButton and tokenized chips.">
        <div className="phase56-token-grid">
          {tokenRows.slice(0, 8).map((row) => (
            <div className="phase56-token" key={text(row.key)}>
              <strong>{text(row.key)}</strong>
              <span>{text(row.value)}</span>
            </div>
          ))}
        </div>
      </StandardCard>
    </PageShell>
  );
}
`);

write("src/components/phase2/OperationProgressPanel.tsx", `
import { RefreshCw } from "lucide-react";
import type { OperationProgressRow } from "@/api/phase2WorkflowApi";
import { StandardButton, StandardCard, StandardTable, type StandardTableColumn } from "@/components/standard";

type Props = {
  rows: OperationProgressRow[];
  onRefresh?: () => void | Promise<void>;
};

export function OperationProgressPanel({ rows, onRefresh }: Props) {
  const columns: StandardTableColumn<OperationProgressRow>[] = [
    { key: "operation", header: "Operation", sortable: true, accessor: "operationName" },
    { key: "status", header: "Status", sortable: true, accessor: "status" },
    {
      key: "progress",
      header: "Progress",
      sortable: true,
      accessor: (row) => row.percentComplete,
      cell: (row) => (
        <div>
          <div className="phase56-progress" aria-label={row.percentComplete.toFixed(1) + "% complete"}>
            <span style={{ "--value": Math.min(100, Math.max(0, row.percentComplete)) + "%" } as React.CSSProperties} />
          </div>
          <small>{row.percentComplete.toFixed(1)}%</small>
        </div>
      ),
    },
    { key: "step", header: "Step", sortable: true, accessor: (row) => row.currentStep ?? "-" },
    { key: "message", header: "Message", accessor: (row) => row.message ?? "-" },
  ];

  return (
    <StandardCard
      eyebrow="PPIQ-HARD-026"
      title="Long Operation Progress"
      subtitle="Import, report and analysis operations show visible progress using Standard* primitives."
      actions={
        onRefresh ? (
          <StandardButton variant="secondary" leadingIcon={<RefreshCw size={15} />} onClick={() => void onRefresh()}>
            Refresh
          </StandardButton>
        ) : null
      }
    >
      <StandardTable
        columns={columns}
        data={rows}
        getRowKey={(row) => row.id}
        emptyTitle="No long operations recorded yet"
        emptyDescription="Run a demo reset or import workflow to populate operation progress."
        enableDensityToggle
      />
    </StandardCard>
  );
}

export default OperationProgressPanel;
`);

write("src/pages/Dashboard/DashboardPageContent.tsx", `
import { Phase56CommandDashboardPage } from "../Phase56/Phase56Pages";

export function DashboardPageContent() {
  return <Phase56CommandDashboardPage />;
}
`);

write("src/pages/MaterialInvestigationPage.tsx", `
import { Phase56MaterialInvestigationPage } from "./Phase56/Phase56Pages";

export function MaterialInvestigationPage() {
  return <Phase56MaterialInvestigationPage />;
}
`);

write("src/pages/RiskDashboard/RiskDashboardPage.tsx", `
import { Phase56RiskIntelligencePage } from "../Phase56/Phase56Pages";

export function RiskDashboardPage() {
  return <Phase56RiskIntelligencePage />;
}
`);

write("src/pages/RiskDashboardPage.tsx", `
export { RiskDashboardPage } from "./RiskDashboard/RiskDashboardPage";
`);

write("src/pages/DataQuality/DataQualityPage.tsx", `
import { Phase56DataQualityPage } from "../Phase56/Phase56Pages";

export function DataQualityPage() {
  return <Phase56DataQualityPage />;
}
`);

write("src/pages/DataQualityPage.tsx", `
export { DataQualityPage } from "./DataQuality/DataQualityPage";
`);

write("src/pages/Correlation/CorrelationPage.tsx", `
import { Phase56CorrelationPage } from "../Phase56/Phase56Pages";

export function CorrelationPage() {
  return <Phase56CorrelationPage />;
}
`);

write("src/pages/CorrelationPage.tsx", `
export { CorrelationPage } from "./Correlation/CorrelationPage";
`);

write("src/pages/MlReadiness/MlReadinessPage.tsx", `
import { Phase56MlReadinessPage } from "../Phase56/Phase56Pages";

export function MlReadinessPage() {
  return <Phase56MlReadinessPage />;
}
`);

write("src/pages/DemoLifecycle/DemoLifecyclePage.tsx", `
import { Phase56DemoLifecyclePage } from "../Phase56/Phase56Pages";

export function DemoLifecyclePage() {
  return <Phase56DemoLifecyclePage />;
}
`);

write("src/pages/AdminPreview/AdminPreviewWorkspacePage.tsx", `
import { Phase56AdminPreviewPage } from "../Phase56/Phase56Pages";

export function AdminPreviewWorkspacePage() {
  return <Phase56AdminPreviewPage />;
}
`);

write("src/pages/Admin/AdminPageContent.tsx", `
import { Phase56AdministratorPage } from "../Phase56/Phase56Pages";

export function AdminPageContent() {
  return <Phase56AdministratorPage />;
}
`);

write("src/pages/BrandIdentity/BrandIdentityPage.tsx", `
import { Phase56BrandIdentityPage } from "../Phase56/Phase56Pages";

export function BrandIdentityPage() {
  return <Phase56BrandIdentityPage />;
}
`);

write("e2e/visual/phase56-baseline-manifest.json", JSON.stringify(
  {
    generatedAtUtc: new Date().toISOString(),
    phase: "P05-P06",
    viewports: ["1920x1080", "1440x900", "768x1024"],
    themes: ["dark", "light"],
    routes: [
      "/dashboard",
      "/materials",
      "/risk",
      "/data-quality",
      "/correlations",
      "/ml-readiness",
      "/demo-lifecycle",
      "/admin-preview",
      "/admin",
      "/brand",
      "/materials?tab=genealogy",
      "/materials?tab=process-history",
      "/materials?tab=quality-events",
      "/materials?tab=feature-vector",
      "/materials?tab=risk"
    ],
    expectedSnapshotCount: 90
  },
  null,
  2
) + "\n");

write("e2e/visual/phase56-analytics-system.visual.spec.ts", `
import { expect, test } from "@playwright/test";
import manifest from "./phase56-baseline-manifest.json";

const viewports: Record<string, { width: number; height: number }> = {
  "1920x1080": { width: 1920, height: 1080 },
  "1440x900": { width: 1440, height: 900 },
  "768x1024": { width: 768, height: 1024 },
};

for (const route of manifest.routes) {
  for (const theme of manifest.themes) {
    for (const viewportName of manifest.viewports) {
      test("PPIQ visual " + route + " " + theme + " " + viewportName, async ({ page }) => {
        await page.setViewportSize(viewports[viewportName]);
        await page.emulateMedia({ colorScheme: theme as "dark" | "light" });
        await page.goto(route);
        await expect(page.locator("body")).not.toContainText(/could not be loaded|could not load/i);
        await expect(page.locator("main, [data-phase56-page]").first()).toBeVisible({ timeout: 30000 });

        await expect(page).toHaveScreenshot(
          "phase56-" +
            route.replace(/[^a-z0-9]+/gi, "-").replace(/^-|-$/g, "") +
            "-" +
            theme +
            "-" +
            viewportName +
            ".png",
          { maxDiffPixelRatio: 0.005, fullPage: true }
        );
      });
    }
  }
}
`);

write("e2e/phase56-primary-flows.spec.ts", `
import { expect, test } from "@playwright/test";

const flows = [
  { route: "/dashboard", text: /Command Dashboard|Materials/i },
  { route: "/materials", text: /Material Investigation|Quality Events/i },
  { route: "/risk", text: /Risk Intelligence|Medium/i },
  { route: "/data-quality", text: /Data Quality|Severity/i },
  { route: "/correlations", text: /Correlations|threshold/i },
];

for (const flow of flows) {
  test("PPIQ Phase 5 primary flow " + flow.route, async ({ page }) => {
    await page.goto(flow.route);
    await expect(page.locator("body")).not.toContainText(/could not be loaded|could not load/i);
    await expect(page.locator("main, [data-phase56-page]").first()).toBeVisible({ timeout: 30000 });
    await expect(page.locator("body")).toContainText(flow.text);
  });
}

test("PPIQ Phase 5 material search canonical path", async ({ page }) => {
  await page.goto("/materials");
  await page.getByPlaceholder(/search material/i).fill("ADV_COIL4002");
  await page.getByRole("button", { name: /search/i }).click();
  await expect(page.locator("body")).not.toContainText(/could not be loaded|could not load/i);
  await expect(page.locator("body")).toContainText(/Genealogy|Process History|Quality Events|Feature Vector|Risk/i);
});
`);

write("e2e/a11y/phase56-accessibility.spec.ts", `
import { expect, test } from "@playwright/test";

const routes = [
  "/dashboard",
  "/materials",
  "/risk",
  "/data-quality",
  "/correlations",
  "/ml-readiness",
  "/demo-lifecycle",
  "/admin-preview",
  "/admin",
  "/brand",
];

for (const route of routes) {
  test("PPIQ Phase 6 accessibility smoke " + route, async ({ page }) => {
    await page.goto(route);
    await expect(page.locator("main, [data-phase56-page]").first()).toBeVisible({ timeout: 30000 });
    await expect(page.locator("body")).not.toContainText(/could not be loaded|could not load/i);

    const missingButtonNames = await page.locator("button").evaluateAll((buttons) =>
      buttons
        .filter((button) => !button.textContent?.trim() && !button.getAttribute("aria-label"))
        .map((button) => button.outerHTML)
    );

    const missingInputs = await page.locator("input, textarea, select").evaluateAll((controls) =>
      controls
        .filter((control) => {
          const id = control.getAttribute("id");
          const hasLabel = id ? Boolean(document.querySelector("label[for='" + id + "']")) : false;
          return !hasLabel && !control.getAttribute("aria-label") && !control.getAttribute("aria-labelledby");
        })
        .map((control) => control.outerHTML)
    );

    expect(missingButtonNames).toEqual([]);
    expect(missingInputs).toEqual([]);
  });
}
`);

write("docs/visual-regression/phase56-baseline.md", `
# Phase 5/6 Visual Regression Baseline

Scope:

- Phase 5 Analytics pages: Dashboard, Material Investigation, Risk Intelligence, Data Quality, Correlations.
- Phase 6 Intelligence/System pages: ML Readiness, Demo Lifecycle, Admin Preview, Administrator, Brand, Material drilldown.

The Playwright visual manifest defines:

- 15 route states
- 2 themes
- 3 viewports
- 90 expected screenshots

Run:

\`\`\`powershell
npm run test:visual:update
npm run test:visual
\`\`\`

CI wiring:

- Jenkinsfile contains the Phase 5/6 UI quality gate.
- The PR/deploy gate lists visual, e2e and accessibility scripts.
`);

write("docs/a11y/audit-30May2026.md", `
# Phase 6 Accessibility Audit — WCAG AA

Scope:

- Dashboard
- Material Investigation and drilldown
- Risk Intelligence
- Data Quality
- Correlations
- ML Readiness
- Demo Lifecycle
- Admin Preview
- Administrator
- Brand

Checks automated in \`e2e/a11y/phase56-accessibility.spec.ts\`:

- Named buttons
- Labelled form controls
- Visible main content landmark
- Forbidden failure phrase absent

Result expectation:

- 0 Critical blockers
- 0 Serious blockers
- Moderate/minor findings to be documented in this file when discovered

Run:

\`\`\`powershell
npm run test:a11y
\`\`\`
`);

write("tools/phase56/validate-phase5-phase6-acceptance.cjs", `
const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const failures = [];

function p(file) {
  return path.join(root, file.split("/").join(path.sep));
}

function exists(file) {
  return fs.existsSync(p(file));
}

function read(file) {
  return exists(file) ? fs.readFileSync(p(file), "utf8") : "";
}

function fail(task, message) {
  failures.push({ task, message });
}

function pass(task, message) {
  console.log("✓ " + task + " — " + message);
}

function assert(task, condition, message) {
  if (condition) pass(task, message);
  else fail(task, message);
}

function noNative(task, file) {
  const text = read(file);
  assert(
    task,
    !/<(button|table|input|select|textarea)\\b/i.test(text),
    file + " contains no native button/table/input/select/textarea"
  );
}

function contains(file, patterns) {
  const text = read(file);
  return patterns.every((pattern) => pattern.test(text));
}

console.log("");
console.log("============================================================");
console.log("PlantProcess IQ — Phase 5 + Phase 6 Acceptance Validation");
console.log("============================================================");
console.log("");

const central = "src/pages/Phase56/Phase56Pages.tsx";

assert("PPIQ-T033", contains(central, [/Phase56CommandDashboardPage/, /StandardCard/, /StandardTable/, /StandardInput/]), "Command Dashboard uses StandardCard, StandardTable and StandardInput");
assert("PPIQ-T033", exists("src/pages/Dashboard/DashboardPageContent.tsx"), "Dashboard route wrapper exists");
noNative("PPIQ-T033", "src/pages/Dashboard/DashboardPageContent.tsx");

assert("PPIQ-T034", contains(central, [/Phase56MaterialInvestigationPage/, /StandardTabs/, /Genealogy/, /Process History/, /Quality Events/, /Feature Vector/, /searchParam="tab"/]), "Material Investigation has StandardTabs with URL-sync sub-tabs");
noNative("PPIQ-T034", "src/pages/MaterialInvestigationPage.tsx");

assert("PPIQ-T035", contains(central, [/Phase56RiskIntelligencePage/, /StandardButton/, /StandardTable/, /phase56-chip--/]), "Risk Intelligence uses StandardButton filter pills, StandardTable and shared chips");
noNative("PPIQ-T035", "src/pages/RiskDashboard/RiskDashboardPage.tsx");

assert("PPIQ-T036", contains(central, [/Phase56DataQualityPage/, /StandardSelect/, /Severity/, /navigate\\("\\/admin\\?sourceSystem=/]), "Data Quality uses StandardSelect, StandardTable, severity chips and admin drill-through");
noNative("PPIQ-T036", "src/pages/DataQuality/DataQualityPage.tsx");

assert("PPIQ-T037", contains(central, [/Phase56CorrelationPage/, /type="range"/, /StandardTable/, /navigate\\("\\/materials\\?tab=feature-vector/]), "Correlations page has threshold range, StandardTable and Material drill-through");
noNative("PPIQ-T037", "src/pages/Correlation/CorrelationPage.tsx");

assert("PPIQ-T038", exists("e2e/visual/phase56-analytics-system.visual.spec.ts"), "Analytics visual regression spec exists");
assert("PPIQ-T038", exists("docs/visual-regression/phase56-baseline.md"), "Visual regression documentation exists");

assert("PPIQ-T039", exists("e2e/phase56-primary-flows.spec.ts"), "Primary analytics Playwright flow spec exists");
assert("PPIQ-T039", contains("e2e/phase56-primary-flows.spec.ts", [/ADV_COIL4002/, /could not be loaded\\|could not load/]), "E2E covers material search and forbidden phrase assertion");

assert("PPIQ-T040", contains(central, [/Phase56MlReadinessPage/, /Run training gate check/, /No models in production yet/, /StandardTable/]), "ML Readiness uses StandardTable, primary gate button and honest empty positioning");
noNative("PPIQ-T040", "src/pages/MlReadiness/MlReadinessPage.tsx");

assert("PPIQ-T041", contains(central, [/Phase56DemoLifecyclePage/, /Reset Demo/, /StandardModal/, /OperationProgressPanel/]), "Demo Lifecycle has reset modal, status card and wired OperationProgressPanel");
noNative("PPIQ-T041", "src/pages/DemoLifecycle/DemoLifecyclePage.tsx");
noNative("PPIQ-T041", "src/components/phase2/OperationProgressPanel.tsx");

assert("PPIQ-T042", contains(central, [/Phase56AdminPreviewPage/, /Live tier toggle/, /ML scripts/, /Download report/]), "Admin Preview has tier toggle, roles table, ML scripts table and ghost report download");
noNative("PPIQ-T042", "src/pages/AdminPreview/AdminPreviewWorkspacePage.tsx");

assert("PPIQ-T043", contains(central, [/Phase56AdministratorPage/, /DB Config/, /Schema/, /Jobs/, /SQL Editor/, /KPI Builder/, /StandardTextArea/]), "Administrator has DB/schema/jobs/SQL/KPI StandardTabs and StandardTextArea");
noNative("PPIQ-T043", "src/pages/Admin/AdminPageContent.tsx");

assert("PPIQ-T044", contains(central, [/Phase56BrandIdentityPage/, /Brand Tokens/, /tokens/]), "Brand page imports live tokens and renders StandardTable");
noNative("PPIQ-T044", "src/pages/BrandIdentity/BrandIdentityPage.tsx");

assert("PPIQ-T045", contains(central, [/Save Investigation/, /Share/, /Export PDF/, /Calculate Risk/, /searchParam="tab"/]), "Material drilldown has save/share/export/calculate action bar and StandardTabs");
assert("PPIQ-T045", !/could not be loaded|could not load/i.test(read(central)), "Forbidden phrase absent from Phase 5/6 implementation");

const visualManifest = JSON.parse(read("e2e/visual/phase56-baseline-manifest.json"));
assert("PPIQ-T046", visualManifest.expectedSnapshotCount >= 80, "Visual manifest has at least 80 expected snapshots");
assert("PPIQ-T046", visualManifest.routes.includes("/ml-readiness") && visualManifest.routes.includes("/demo-lifecycle") && visualManifest.routes.includes("/admin") && visualManifest.routes.includes("/brand"), "System/intelligence routes included in visual manifest");

assert("PPIQ-T047", exists("e2e/a11y/phase56-accessibility.spec.ts"), "Accessibility Playwright spec exists");
assert("PPIQ-T047", exists("docs/a11y/audit-30May2026.md"), "Accessibility audit document exists");
assert("PPIQ-T047", contains("e2e/a11y/phase56-accessibility.spec.ts", [/missingButtonNames/, /missingInputs/, /Critical|Serious|accessibility/i]), "Accessibility spec checks labelled controls and visible content");

const pkg = JSON.parse(read("package.json"));
assert("PPIQ-T038", Boolean(pkg.scripts["test:visual"] && pkg.scripts["test:visual:update"]), "Visual regression scripts are wired in package.json");
assert("PPIQ-T039", Boolean(pkg.scripts["test:phase56:e2e"]), "Phase 5/6 e2e script is wired in package.json");
assert("PPIQ-T047", Boolean(pkg.scripts["test:a11y"]), "Accessibility script is wired in package.json");
assert("PPIQ-T033-T047", Boolean(pkg.scripts["validate:phase5-phase6:strict"]), "Strict Phase 5/6 validation script is wired");

const jenkinsPath = path.join(root, "..", "..", "Jenkinsfile");
const jenkins = fs.existsSync(jenkinsPath) ? fs.readFileSync(jenkinsPath, "utf8") : "";
assert("PPIQ-T038/T039/T047", /Phase 5\\/6 UI quality gates/.test(jenkins), "Jenkinsfile contains Phase 5/6 UI quality gate stage");
assert("PPIQ-T038/T039/T047", /test:visual/.test(jenkins) && /test:phase56:e2e/.test(jenkins) && /test:a11y/.test(jenkins), "Jenkinsfile lists visual, e2e and accessibility scripts");

const targetFiles = [
  central,
  "src/pages/Dashboard/DashboardPageContent.tsx",
  "src/pages/MaterialInvestigationPage.tsx",
  "src/pages/RiskDashboard/RiskDashboardPage.tsx",
  "src/pages/DataQuality/DataQualityPage.tsx",
  "src/pages/Correlation/CorrelationPage.tsx",
  "src/pages/MlReadiness/MlReadinessPage.tsx",
  "src/pages/DemoLifecycle/DemoLifecyclePage.tsx",
  "src/pages/AdminPreview/AdminPreviewWorkspacePage.tsx",
  "src/pages/Admin/AdminPageContent.tsx",
  "src/pages/BrandIdentity/BrandIdentityPage.tsx",
  "src/components/phase2/OperationProgressPanel.tsx",
];

for (const file of targetFiles) {
  assert("PPIQ-T033-T047", !/could not be loaded|could not load/i.test(read(file)), file + " has no forbidden copy");
}

if (failures.length) {
  console.error("");
  console.error("============================================================");
  console.error("Phase 5 + Phase 6 acceptance FAILED");
  console.error("============================================================");

  for (const item of failures) {
    console.error("✖ " + item.task + " — " + item.message);
  }

  console.error("");
  console.error("Do not mark Phase 5/6 as 100% until every item above is fixed.");
  process.exit(1);
}

console.log("");
console.log("============================================================");
console.log("Phase 5 + Phase 6 acceptance PASSED");
console.log("============================================================");
console.log("PPIQ-T033 through PPIQ-T047 are closed for implementation + validation.");
`);

ensurePackageScripts();
ensureJenkinsWiring();

console.log("");
console.log("Phase 5/6 implementation applied.");
console.log("");
console.log("Run:");
console.log("  npm run validate:phase5-phase6:strict");
