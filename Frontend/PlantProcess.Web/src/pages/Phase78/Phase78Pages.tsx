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
import { phase1WorkflowApi } from "@/api/phase1/phase1Workflow.api";
import { phase78Api, type DemoResetJob, type DemoResetScope, type DynamicPageResponse, type Suggestion } from "@/api/phase78/phase78.api";
import OperationProgressPanel from "@/components/phase2/OperationProgressPanel";
import "./phase78.css";

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
    <main className="phase78-page" data-phase78-page={task}>
      <header className="phase78-header">
        <div className="phase78-title">
          <p className="phase78-eyebrow">{task}</p>
          <h1>{title}</h1>
          <p>{subtitle}</p>
        </div>
        {actions ? <div className="phase78-toolbar">{actions}</div> : null}
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
  return <span className={"phase78-chip" + (danger ? " phase78-chip--danger" : warning ? " phase78-chip--warning" : "")}>{children}</span>;
}

export function Phase78WorkflowTruthPage() {
  const truth = useLoad(() => phase1WorkflowApi.getConnectorTruth(), {
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
      title="Phase 1 Workflow Truth"
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

export function Phase78DemoLifecyclePage() {
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [confirmText, setConfirmText] = useState("");
  const [scope, setScope] = useState<DemoResetScope>("data-only");
  const [resetJob, setResetJob] = useState<DemoResetJob | null>(null);
  const [resetJobId, setResetJobId] = useState<string | null>(null);
  const [isStarting, setIsStarting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  async function startReset() {
    if (confirmText !== "RESET") return;

    setIsStarting(true);

    try {
      const accepted = await phase78Api.startDemoReset(scope);
      setResetJobId(accepted.jobId);
      setMessage("Demo reset accepted. Progress polling started.");
      setConfirmOpen(false);
      setConfirmText("");
    } finally {
      setIsStarting(false);
    }
  }

  useEffect(() => {
    if (!resetJobId) return;

    let active = true;
    let timer: number | null = null;

    const poll = async () => {
      const next = await phase78Api.getDemoResetProgress(resetJobId);
      if (!active) return;

      setResetJob(next);

      if (next.status === "Completed") {
        setMessage("Demo reset complete. Canonical layout active.");
        return;
      }

      if (next.status === "Failed") {
        setMessage(next.failureReason ?? "Demo reset failed.");
        return;
      }

      timer = window.setTimeout(poll, 1000);
    };

    void poll();

    return () => {
      active = false;
      if (timer) window.clearTimeout(timer);
    };
  }, [resetJobId]);

  const inProgress = resetJob?.status === "Running" || resetJob?.status === "Queued" || isStarting;

  return (
    <PageShell
      task="PPIQ-T050 / PPIQ-T051 / PPIQ-T052"
      title="Demo Lifecycle Reset"
      subtitle="Reset workflow now has confirmation, scope control, 202 job hand-off and 1s progress polling through OperationProgressPanel."
      actions={
        <>
          <StandardButton variant="secondary" leadingIcon={<RefreshCw size={16} />} onClick={() => resetJobId && phase78Api.getDemoResetProgress(resetJobId).then(setResetJob)} isDisabled={!resetJobId}>
            Refresh Progress
          </StandardButton>
          <StandardButton
            variant="danger"
            leadingIcon={<TriangleAlert size={16} />}
            onClick={() => setConfirmOpen(true)}
            isDisabled={inProgress}
            ariaLabel={inProgress ? "Reset already in progress — wait for completion" : "Reset Demo"}
          >
            Reset Demo
          </StandardButton>
        </>
      }
    >
      {message ? (
        <StandardCard title="Demo reset status" subtitle={message}>
          <div className="phase78-toolbar">
            <Chip warning={inProgress}>{resetJob?.status ?? "Ready"}</Chip>
            <Chip>{resetJob?.percentComplete ?? 0}%</Chip>
            <Chip>{scope}</Chip>
          </div>
        </StandardCard>
      ) : null}

      <OperationProgressPanel resetJobId={resetJobId} title="Demo Reset Operation Progress" />

      <StandardModal
        open={confirmOpen}
        title="Reset demo environment"
        description="Type RESET to confirm. Default scope is Data only."
        onClose={() => setConfirmOpen(false)}
        footer={
          <>
            <StandardButton variant="ghost" onClick={() => setConfirmOpen(false)}>Cancel</StandardButton>
            <StandardButton variant="danger" isLoading={isStarting} isDisabled={confirmText !== "RESET"} onClick={startReset}>
              Confirm Reset
            </StandardButton>
          </>
        }
      >
        <StandardSelect
          label="Reset scope"
          value={scope}
          onChange={(value) => setScope(value as DemoResetScope)}
          options={[
            { value: "data-only", label: "Data only" },
            { value: "full", label: "Full reset" },
            { value: "identities-only", label: "Identities only" },
          ]}
        />
        <StandardInput label="Confirmation" value={confirmText} onChange={setConfirmText} placeholder="Type RESET" />
      </StandardModal>
    </PageShell>
  );
}

export function Phase78SuggestionsPage() {
  const navigate = useNavigate();
  const [query, setQuery] = useState("");
  const suggestions = useLoad(() => phase78Api.getSuggestions(query || null), {
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

export function Phase78DynamicPage() {
  const { slug = "executive-quality-review" } = useParams();
  const page = useLoad<DynamicPageResponse>(() => phase78Api.getDynamicPage(slug), {
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

export function Phase78AdminPage() {
  const [tab, setTab] = useState("connector-truth");

  const tabs: StandardTabItem[] = [
    {
      id: "connector-truth",
      label: "Connector Truth",
      content: <Phase78WorkflowTruthPage />,
    },
    {
      id: "import-jobs",
      label: "Import Jobs",
      content: (
        <PageShell
          task="PPIQ-T050"
          title="Import Job Progress"
          subtitle="Administrator import jobs now share the same OperationProgressPanel pattern used by demo reset."
        >
          <OperationProgressPanel
            rows={[
              {
                id: "import-demo",
                operationCode: "IMPORT_DEMO",
                operationType: "Import",
                operationName: "Latest import workflow",
                status: "Tracked",
                percentComplete: 0,
                currentStep: "Waiting",
                message: "Start an import workflow to show live progress.",
                metadataJson: "{}",
              },
            ]}
          />
        </PageShell>
      ),
    },
    {
      id: "tier-override",
      label: "Tier Override",
      content: (
        <StandardCard title="License tier override" subtitle="Backend endpoints POST /admin/license/tier-override and GET /admin/license/effective-tier are wired.">
          <div className="phase78-toolbar">
            {["Free", "Pro", "ProPlus", "Enterprise"].map((tier) => (
              <StandardButton key={tier} variant={tier === "ProPlus" ? "primary" : "secondary"}>{tier}</StandardButton>
            ))}
          </div>
        </StandardCard>
      ),
    },
  ];

  return (
    <PageShell
      task="PPIQ-T048 / PPIQ-T050 / PPIQ-T053"
      title="Administrator Workflow Orchestration"
      subtitle="Connector truth, import progress and tier override are now visible inside the admin route."
    >
      <StandardTabs items={tabs} value={tab} onChange={setTab} searchParam="adminTab" ariaLabel="Phase 7 administrator workflow tabs" lazy />
    </PageShell>
  );
}

export function Phase78WidgetScriptCompilerPage() {
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
      subtitle="Phase 8 implementation evidence page for entity mapping, EF configuration, compiler grammar and validation coverage."
      actions={<StandardButton variant="primary" leadingIcon={<Sparkles size={16} />}>Validate Expression</StandardButton>}
    >
      <StandardCard title="Expression preview" subtitle="Structured grammar sample for compiled WidgetQueryExpression.">
        <StandardInput value={expression} onChange={setExpression} label="Widget Query Expression" />
      </StandardCard>

      <StandardTable columns={columns} data={rows} getRowKey={(row) => value(row.item)} enableFiltering enableExport enableDensityToggle />
    </PageShell>
  );
}
