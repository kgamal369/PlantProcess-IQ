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
import { SaveInspectionJobModal } from "@/components/phase2/SaveInspectionJobModal";
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

      <SaveInspectionJobModal
        isOpen={saveOpen}
        onClose={() => setSaveOpen(false)}
        materialUnitId={selectedMaterialId}
        materialCode={selected?.materialCode ?? null}
        filters={{ tab, source: "Phase56MaterialInvestigationPage" }}
      />

      <StandardModal
        open={false}
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

  const progressRows = lifecycle.data.jobChain.jobs.map((job, index) => ({
    id: String(job.jobId ?? "demo-operation-" + index),
    operationCode: String(job.jobCode ?? job.jobName ?? "DEMO_OPERATION_" + index)
      .replace(/[^a-zA-Z0-9_]/g, "_")
      .toUpperCase(),
    operationType: String(job.jobType ?? "DemoLifecycle"),
    operationName: String(job.jobName ?? "Demo lifecycle operation"),
    status: String(job.lastRunStatus ?? "Tracked"),
    percentComplete: job.lastRunStatus === "Succeeded" ? 100 : job.lastRunStatus === "Running" ? 65 : 25,
    currentStep: String(job.jobType ?? "Workflow"),
    totalSteps: 4,
    completedSteps: job.lastRunStatus === "Succeeded" ? 4 : job.lastRunStatus === "Running" ? 2 : 1,
    message: String(job.operationalRole ?? "Demo lifecycle operation is tracked."),
    startedAtUtc: String(job.lastRunStartedAtUtc ?? lifecycle.data.generatedAtUtc ?? new Date().toISOString()),
    completedAtUtc: job.lastRunFinishedAtUtc ?? null,
    failedAtUtc: job.lastRunStatus === "Failed" ? String(job.lastRunFinishedAtUtc ?? lifecycle.data.generatedAtUtc ?? new Date().toISOString()) : null,
    failureReason: job.lastRunStatus === "Failed" ? String(job.operationalRole ?? "Operation did not complete.") : null,
    correlationId: null,
    requestedBy: "PlantProcess IQ Demo",
    metadataJson: JSON.stringify({
      source: "Phase56DemoLifecyclePage",
      jobId: job.jobId ?? null,
      jobCode: job.jobCode ?? null,
      jobType: job.jobType ?? null,
      generatedAtUtc: lifecycle.data.generatedAtUtc,
    }),
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
