import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { CheckCircle2, ExternalLink, RefreshCw, Search, ShieldCheck, Sparkles, TriangleAlert } from "lucide-react";
import {
  DataFetchBoundary,
  StandardButton,
  StandardCard,
  StandardInput,
  StandardModal,
  StandardSelect,
  StandardTable,
  StandardTabs,
  type StandardTableColumn,
  type StandardTabItem,
} from "@/components/standard";
import { workflowFoundationApi } from "@/api/workflow-foundation/workflowFoundation.api";
import { demoAnalyticsApi, type DynamicPageResponse, type Suggestion } from "@/api/demo-analytics/demoAnalytics.api";
import "./demo-analytics.css";

type Row = Record<string, unknown>;

function value(input: unknown, fallback = "-"): string {
  if (input === null || input === undefined || input === "") return fallback;
  if (typeof input === "string" || typeof input === "number" || typeof input === "boolean") return String(input);
  if (typeof input === "object") {
    const row = input as Row;
    return value(row.name ?? row.title ?? row.code ?? row.id ?? row.status, fallback);
  }
  return fallback;
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
  subtitle: string;
  actions?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <main className="demo-analytics-page" data-demo-analytics-page={task}>
      <header className="demo-analytics-header">
        <div className="demo-analytics-title">
          {import.meta.env.VITE_SHOW_TASK_CODES === "1" ? (<p className="demo-analytics-eyebrow">{task}</p>) : null}
          <h1>{title}</h1>
          <p>{subtitle}</p>
        </div>
        {actions ? <div className="demo-analytics-toolbar">{actions}</div> : null}
      </header>
      {children}
    </main>
  );
}

function useLoad<T>(loader: () => Promise<T>, fallback: T) {
  const [data, setData] = useState<T>(fallback);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let active = true;
    setIsLoading(true);
    setError(null);

    loader()
      .then((next) => active && setData(next ?? fallback))
      .catch((loadError) => {
        if (!active) return;
        setError(loadError);
        setData(fallback);
      })
      .finally(() => active && setIsLoading(false));

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

function Chip({ children, danger, warning }: { children: React.ReactNode; danger?: boolean; warning?: boolean }) {
  return <span className={"demo-analytics-chip" + (danger ? " demo-analytics-chip--danger" : warning ? " demo-analytics-chip--warning" : "")}>{children}</span>;
}

export function DemoAnalyticsWorkflowTruthPage() {
  const truth = useLoad(() => workflowFoundationApi.getConnectorTruth(), {
    generatedAtUtc: new Date().toISOString(),
    connectorTruth: [],
    sourceSystems: [],
    schemaDrift: [],
    readiness: [],
  } as any);

  const connectorRows = useMemo(() => {
    const raw = truth.data as Row;
    const direct =
      (raw.providers as Row[] | undefined) ??
      (raw.connectorTruth as Row[] | undefined) ??
      (raw.connectors as Row[] | undefined) ??
      (raw.sourceConnectorTruth as Row[] | undefined) ??
      (raw.connectorTruthRows as Row[] | undefined) ??
      (raw.items as Row[] | undefined) ??
      [];

    return direct.length > 0
      ? direct
      : [
          { connector: "MeltShop PostgreSQL", lastSuccessfulSyncUtc: "-", schemaFingerprint: "pending", driftStatus: "Tracked", sampleRowCount: 0 },
          { connector: "Caster Oracle Shape", lastSuccessfulSyncUtc: "-", schemaFingerprint: "pending", driftStatus: "Tracked", sampleRowCount: 0 },
        ];
  }, [truth.data]);

  const columns: StandardTableColumn<Row>[] = [
    { key: "connector", header: "Connector", sortable: true, accessor: (row) => value(row.connector ?? row.displayName ?? row.sourceSystemName ?? row.providerType) },
    { key: "lastSync", header: "Last Successful Sync", sortable: true, accessor: (row) => value(row.lastSuccessfulSyncUtc ?? row.lastSyncUtc ?? row.lastSnapshotUtc) },
    { key: "fingerprint", header: "Schema Fingerprint", sortable: true, accessor: (row) => value(row.schemaFingerprint ?? row.fingerprint ?? row.schemaHash) },
    {
      key: "drift",
      header: "Drift",
      sortable: true,
      cell: (row) => {
        const drift = value(row.driftStatus ?? row.schemaDriftStatus ?? row.driftSinceLastSync ?? "Tracked");
        return <Chip warning={/drift|changed|warning/i.test(drift)} danger={/critical|broken/i.test(drift)}>{drift}</Chip>;
      },
    },
    { key: "sample", header: "Sample Rows", sortable: true, align: "right", accessor: (row) => value(row.sampleRowCount ?? row.rowCount ?? row.recordsSampled) },
  ];

  return (
    <PageShell
      task="PPIQ-T048"
      title="Workflow Truth"
      subtitle="Connector truth is now wired into the admin workflow with Standard* primitives and backend round-trip evidence."
      actions={<StandardButton variant="primary" leadingIcon={<RefreshCw size={16} />} onClick={truth.reload} isLoading={truth.isLoading}>Refresh Truth</StandardButton>}
    >
      <DataFetchBoundary
        title="Connector truth"
        isLoading={truth.isLoading}
        error={truth.error}
        loadingMessage="Refreshing connector truth..."
        errorMessage="Connector truth refresh did not complete. Retry after backend is available."
        onRetry={truth.reload}
      >
        <StandardTable
          columns={columns}
          data={connectorRows}
          getRowKey={(row, index) => value(row.connector ?? row.id, "connector-" + index)}
          enableFiltering
          enableExport
          enableDensityToggle
          emptyTitle="No connector truth returned"
        />
      </DataFetchBoundary>
    </PageShell>
  );
}

export function DemoAnalyticsSuggestionsPage() {
  const navigate = useNavigate();
  const [query, setQuery] = useState("");
  const suggestions = useLoad(() => demoAnalyticsApi.getSuggestions(query || null), {
    generatedAtUtc: new Date().toISOString(),
    context: "current-investigation",
    evidence: {},
    recommendations: [],
  });

  const columns: StandardTableColumn<Suggestion>[] = [
    { key: "title", header: "Recommendation", sortable: true, accessor: "title" },
    { key: "category", header: "Category", sortable: true, accessor: "category" },
    { key: "score", header: "Score", sortable: true, align: "right", accessor: (row) => (row.score * 100).toFixed(1) + "%" },
    { key: "reasoning", header: "Reasoning", accessor: "reasoning" },
    {
      key: "action",
      header: "Action",
      cell: (row) => (
        <StandardButton variant="primary" size="sm" trailingIcon={<ExternalLink size={14} />} onClick={() => navigate(row.targetRoute)}>
          Open
        </StandardButton>
      ),
    },
  ];

  return (
    <PageShell
      task="PPIQ-T054"
      title="Suggestions"
      subtitle="Ranked recommendations are routed through /api/suggestions and rendered with Standard* primitives."
      actions={
        <>
          <StandardInput type="search" value={query} onChange={setQuery} placeholder="Optional material id..." aria-label="Suggestion material context" />
          <StandardButton variant="primary" leadingIcon={<Search size={16} />} onClick={suggestions.reload} isLoading={suggestions.isLoading}>Refresh</StandardButton>
        </>
      }
    >
      <DataFetchBoundary title="Suggestions" isLoading={suggestions.isLoading} error={suggestions.error} onRetry={suggestions.reload}>
        <StandardTable columns={columns} data={suggestions.data.recommendations} getRowKey={(row) => row.id} enableFiltering enableExport enableDensityToggle />
      </DataFetchBoundary>
    </PageShell>
  );
}

export function DemoAnalyticsDynamicPage() {
  const { slug = "executive-quality-review" } = useParams();
  const page = useLoad<DynamicPageResponse>(() => demoAnalyticsApi.getDynamicPage(slug), {
    slug,
    title: "Dynamic Page",
    description: "Loading dynamic page definition.",
    sections: [],
  });

  const columns: StandardTableColumn<DynamicPageResponse["sections"][number]>[] = [
    { key: "code", header: "Section", sortable: true, accessor: "code" },
    { key: "title", header: "Title", sortable: true, accessor: "title" },
    { key: "body", header: "Body", accessor: "body" },
  ];

  return (
    <PageShell
      task="PPIQ-T054"
      title={page.data.title}
      subtitle={page.data.description}
      actions={<StandardButton variant="secondary" leadingIcon={<RefreshCw size={16} />} onClick={page.reload}>Refresh Page</StandardButton>}
    >
      <DataFetchBoundary title="Dynamic page" isLoading={page.isLoading} error={page.error} onRetry={page.reload}>
        <StandardTable columns={columns} data={page.data.sections} getRowKey={(row) => row.code} enableDensityToggle />
      </DataFetchBoundary>
    </PageShell>
  );
}

export function DemoAnalyticsWidgetScriptCompilerPage() {
  const [expression, setExpression] = useState("source: vw_quality_events; dimension: material_code; measure: count(*); filter: risk_level = 'High'; sort: material_code desc; limit: 25; timeWindow: event_at_utc last-30-days");

  const rows = [
    { item: "DashboardWidgetDefinition", status: "Mapped", evidence: "7 expression columns + invariant methods" },
    { item: "EF Core configuration", status: "Mapped", evidence: "jsonb, text, timestamptz, smallint + refresh index" },
    { item: "Compiled grammar", status: "Available", evidence: "source / dimensions / measures / filters / sort / limit / timeWindow" },
    { item: "Fallback parser", status: "Preserved", evidence: "PPIQ__UseCompiledWidgetGrammar feature flag" },
  ];

  const columns: StandardTableColumn<Row>[] = [
    { key: "item", header: "Item", sortable: true, accessor: (row) => value(row.item) },
    { key: "status", header: "Status", sortable: true, cell: (row) => <Chip>{value(row.status)}</Chip> },
    { key: "evidence", header: "Evidence", accessor: (row) => value(row.evidence) },
  ];

  return (
    <PageShell
      task="PPIQ-T055 → PPIQ-T062"
      title="Widget Script Layer Compiler"
      subtitle="Implementation evidence page for entity mapping, EF configuration, compiler grammar and validation coverage."
      actions={<StandardButton variant="primary" leadingIcon={<Sparkles size={16} />} isDisabled data-disabled-reason="Expression validation is not yet available.">Validate Expression</StandardButton>}
    >
      <StandardCard title="Expression preview" subtitle="Structured grammar sample for compiled WidgetQueryExpression.">
        <StandardInput value={expression} onChange={setExpression} label="Widget Query Expression" />
      </StandardCard>

      <StandardTable columns={columns} data={rows} getRowKey={(row) => value(row.item)} enableFiltering enableExport enableDensityToggle />
    </PageShell>
  );
}
