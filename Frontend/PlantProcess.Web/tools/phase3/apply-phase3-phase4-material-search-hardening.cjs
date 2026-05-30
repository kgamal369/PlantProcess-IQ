const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const srcRoot = path.join(root, "src");

function abs(relativePath) {
  return path.join(root, relativePath.split("/").join(path.sep));
}

function ensureDir(filePath) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
}

function write(relativePath, content) {
  const target = abs(relativePath);
  ensureDir(target);
  fs.writeFileSync(target, content.replace(/^\n/, ""), "utf8");
  console.log("Wrote " + relativePath);
}

function remove(relativePath) {
  const target = abs(relativePath);
  if (fs.existsSync(target)) {
    fs.rmSync(target, { force: true });
    console.log("Deleted " + relativePath);
  }
}

function removeEmptyDir(relativePath) {
  const target = abs(relativePath);
  if (fs.existsSync(target) && fs.readdirSync(target).length === 0) {
    fs.rmdirSync(target);
    console.log("Removed empty directory " + relativePath);
  }
}

function walk(dir) {
  if (!fs.existsSync(dir)) return [];
  const result = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (["node_modules", "dist", "build", "coverage", "storybook-static"].includes(entry.name)) continue;
      result.push(...walk(full));
      continue;
    }
    if (entry.isFile() && /\.(ts|tsx|js|jsx)$/.test(entry.name)) result.push(full);
  }
  return result;
}

function replaceInSourceFiles(replacements) {
  for (const file of walk(srcRoot)) {
    let text = fs.readFileSync(file, "utf8");
    const before = text;
    for (const [pattern, replacement] of replacements) text = text.replace(pattern, replacement);
    if (text !== before) {
      fs.writeFileSync(file, text, "utf8");
      console.log("Rewrote imports in " + path.relative(root, file).split(path.sep).join("/"));
    }
  }
}

write("src/components/standard/DataFetchBoundary.tsx", String.raw`
import type { ReactNode } from "react";
import { AlertTriangle, CheckCircle2, RefreshCw } from "lucide-react";
import { StandardButton } from "./StandardButton";
import "./standard-components.css";

export type DataFetchBoundaryStatus = "idle" | "loading" | "success" | "error" | "empty";

export type DataFetchBoundaryProps = {
  title?: ReactNode;
  status?: DataFetchBoundaryStatus;
  isLoading?: boolean;
  error?: unknown;
  isEmpty?: boolean;
  emptyTitle?: ReactNode;
  emptyMessage?: ReactNode;
  loadingMessage?: ReactNode;
  successMessage?: ReactNode;
  errorMessage?: ReactNode;
  onRetry?: () => void;
  retryLabel?: string;
  children: ReactNode;
};

function messageFrom(error: unknown): string {
  if (!error) return "The request did not complete. Retry when the data source is available.";
  if (error instanceof Error) return error.message;
  return String(error);
}

export function DataFetchBoundary({
  title = "Data",
  status = "idle",
  isLoading = false,
  error,
  isEmpty = false,
  emptyTitle = "No records available",
  emptyMessage = "Adjust filters or refresh the data source.",
  loadingMessage = "Loading data...",
  successMessage,
  errorMessage,
  onRetry,
  retryLabel = "Retry",
  children,
}: DataFetchBoundaryProps) {
  const normalizedStatus: DataFetchBoundaryStatus = isLoading
    ? "loading"
    : error
      ? "error"
      : isEmpty
        ? "empty"
        : status;

  if (normalizedStatus === "loading") {
    return (
      <div className="ppiq-std-table-shell" aria-busy="true">
        <div className="ppiq-std-table-state">
          <strong>{loadingMessage}</strong>
          <span>{title}</span>
          <div className="ppiq-std-table-skeleton" />
          <div className="ppiq-std-table-skeleton" />
          <div className="ppiq-std-table-skeleton" />
        </div>
      </div>
    );
  }

  if (normalizedStatus === "error") {
    return (
      <div className="ppiq-std-table-shell" role="alert">
        <div className="ppiq-std-table-state">
          <strong>
            <AlertTriangle size={16} aria-hidden="true" /> {title}
          </strong>
          <span>{errorMessage ?? messageFrom(error)}</span>
          {onRetry ? (
            <div style={{ marginTop: 14 }}>
              <StandardButton variant="secondary" leadingIcon={<RefreshCw size={16} />} onClick={onRetry}>
                {retryLabel}
              </StandardButton>
            </div>
          ) : null}
        </div>
      </div>
    );
  }

  if (normalizedStatus === "empty") {
    return (
      <div className="ppiq-std-table-shell">
        <div className="ppiq-std-table-state">
          <strong>{emptyTitle}</strong>
          <span>{emptyMessage}</span>
        </div>
      </div>
    );
  }

  return (
    <>
      {normalizedStatus === "success" && successMessage ? (
        <div className="ppiq-std-table-shell" role="status" style={{ marginBottom: 12 }}>
          <div className="ppiq-std-table-state">
            <strong>
              <CheckCircle2 size={16} aria-hidden="true" /> Ready
            </strong>
            <span>{successMessage}</span>
          </div>
        </div>
      ) : null}
      {children}
    </>
  );
}

export default DataFetchBoundary;
`);

write("src/components/standard/index.ts", String.raw`
export * from "./tokens";
export * from "./StandardButton";
export * from "./StandardFields";
export * from "./StandardTabs";
export * from "./StandardTable";
export * from "./StandardSurface";
export * from "./DataFetchBoundary";
`);

write("src/main.tsx", String.raw`
import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import { ErrorBoundary } from "@/components/ErrorBoundary";
import { ToastRoot } from "@/notifications/ToastRoot";
import "./index.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <ErrorBoundary routePath="app-root" fallbackTitle="The application shell is refreshing">
      <BrowserRouter>
        <App />
        <ToastRoot />
      </BrowserRouter>
    </ErrorBoundary>
  </React.StrictMode>,
);
`);

write("src/pages/MaterialInvestigationPage.tsx", String.raw`
import { useEffect, useMemo, useState, type MouseEvent } from "react";
import { Download, FileSearch, Search, ShieldCheck } from "lucide-react";
import { plantProcessApi, type DashboardMaterialRow } from "@/api/plantProcessApi";
import { MetricCard } from "@/components/MetricCard";
import {
  DataFetchBoundary,
  StandardButton,
  StandardInput,
  StandardTable,
  type StandardTableColumn,
} from "@/components/standard";
import { useDashboardFilters } from "@/state/DashboardFilterContext";

const MATERIAL_PAGE_SIZE = 25;
const PARAMETER_PAGE_SIZE = 500;

type AsyncStatus = "idle" | "loading" | "success" | "error";

type ParameterObservationPageInfo = {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasMore: boolean;
};

type ParameterObservationRow = {
  id: string;
  materialUnitId?: string | null;
  processStepExecutionId?: string | null;
  parameterDefinitionId?: string | null;
  equipmentId?: string | null;
  observedAtUtc?: string | null;
  observedAtLocal?: string | null;
  numericValue?: number | null;
  textValue?: string | null;
  booleanValue?: boolean | null;
  unit?: string | null;
  plantTimeZoneId?: string | null;
  plantUtcOffsetMinutes?: number | null;
  sourceSystem?: string | null;
  sourceRecordId?: string | null;
  isSynthetic?: boolean | null;
};

type MaterialInvestigationResponse = {
  requestedMaterialUnitId?: string;
  maxDepth?: number;
  license?: {
    tier?: string;
    feature?: string;
    status?: string;
  };
  summary?: {
    materials?: number;
    aliases?: number;
    genealogyEdges?: number;
    processSteps?: number;
    parameterObservations?: number;
    parameterObservationsReturned?: number;
    processEvents?: number;
    downtimeEvents?: number;
    qualityEvents?: number;
    riskScores?: number;
    dataQualityIssues?: number;
  };
  parameterObservationPage?: ParameterObservationPageInfo;
  materials?: unknown[];
  aliases?: unknown[];
  genealogyEdges?: unknown[];
  processSteps?: unknown[];
  parameterObservations?: ParameterObservationRow[];
  processEvents?: unknown[];
  downtimeEvents?: unknown[];
  qualityEvents?: unknown[];
  riskScores?: unknown[];
  dataQualityIssues?: unknown[];
};

type MaterialSearchResultState = {
  status: AsyncStatus;
  error: string;
  lastSuccessfulQuery: string;
  totalCount: number;
};

export function MaterialInvestigationPage() {
  const { filters, mergeFilters } = useDashboardFilters();

  const [materials, setMaterials] = useState<DashboardMaterialRow[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [materialSearch, setMaterialSearch] = useState(filters.materialCode ?? "");

  const [investigation, setInvestigation] = useState<MaterialInvestigationResponse | null>(null);
  const [features, setFeatures] = useState<unknown>(null);
  const [riskResult, setRiskResult] = useState<unknown>(null);

  const [parameterRows, setParameterRows] = useState<ParameterObservationRow[]>([]);
  const [parameterPageInfo, setParameterPageInfo] = useState<ParameterObservationPageInfo | null>(null);

  const [materialState, setMaterialState] = useState<MaterialSearchResultState>({
    status: "idle",
    error: "",
    lastSuccessfulQuery: filters.materialCode ?? "",
    totalCount: 0,
  });
  const [actionError, setActionError] = useState("");
  const [isLoadingInvestigation, setIsLoadingInvestigation] = useState(false);
  const [isLoadingMoreParameters, setIsLoadingMoreParameters] = useState(false);
  const [isCalculatingRisk, setIsCalculatingRisk] = useState(false);

  useEffect(() => {
    let ignore = false;

    async function loadFromGlobalFilters() {
      setMaterialState((current) => ({ ...current, status: "loading", error: "" }));

      try {
        const result = await plantProcessApi.searchDashboardMaterials({
          ...filters,
          materialCode: materialSearch || filters.materialCode,
          page: 1,
          pageSize: MATERIAL_PAGE_SIZE,
          sortBy: "materialCode",
          sortDirection: "asc",
        });

        if (ignore) return;

        setMaterials(result.items);
        setSelectedId((current) => current || result.items[0]?.materialUnitId || "");
        setMaterialState({
          status: "success",
          error: "",
          lastSuccessfulQuery: materialSearch || filters.materialCode || "",
          totalCount: result.totalCount,
        });
      } catch (err) {
        if (ignore) return;

        setMaterialState((current) => ({
          ...current,
          status: "error",
          error: err instanceof Error ? err.message : "Failed to load materials.",
        }));
      }
    }

    void loadFromGlobalFilters();

    return () => {
      ignore = true;
    };

    // This effect intentionally tracks only global filters that change the material universe.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters.siteId, filters.sourceSystem, filters.riskClass, filters.fromUtc, filters.toUtc]);

  const selectedMaterial = useMemo(
    () => materials.find((x) => x.materialUnitId === selectedId),
    [materials, selectedId],
  );

  const pdfUrl = selectedId ? plantProcessApi.getInvestigationPdfUrl(selectedId) : "#";

  const materialColumns: StandardTableColumn<DashboardMaterialRow>[] = [
    {
      key: "materialCode",
      header: "Material",
      accessor: "materialCode",
      sortable: true,
      minWidth: 180,
      cell: (row) => (
        <StandardButton
          variant={row.materialUnitId === selectedId ? "secondary" : "ghost"}
          size="sm"
          onClick={(event: MouseEvent<HTMLButtonElement>) => {
            event.stopPropagation();
            selectMaterial(row);
          }}
        >
          {row.materialCode}
        </StandardButton>
      ),
    },
    {
      key: "materialUnitType",
      header: "Type",
      accessor: "materialUnitType",
      sortable: true,
      minWidth: 110,
    },
    {
      key: "productFamily",
      header: "Family",
      accessor: (row) => row.productFamily ?? "-",
      sortable: true,
      minWidth: 140,
    },
    {
      key: "gradeOrRecipe",
      header: "Grade / Recipe",
      accessor: (row) => row.gradeOrRecipe ?? "-",
      sortable: true,
      minWidth: 150,
    },
    {
      key: "latestRiskClass",
      header: "Risk",
      accessor: (row) => row.latestRiskClass ?? "-",
      sortable: true,
      minWidth: 110,
    },
    {
      key: "defectEventCount",
      header: "Defects",
      accessor: "defectEventCount",
      sortable: true,
      align: "right",
      minWidth: 100,
    },
    {
      key: "parameterObservationCount",
      header: "Parameters",
      accessor: "parameterObservationCount",
      sortable: true,
      align: "right",
      minWidth: 120,
    },
    {
      key: "sourceSystem",
      header: "Source",
      accessor: (row) => row.sourceSystem ?? "-",
      sortable: true,
      minWidth: 160,
    },
  ];

  const parameterColumns: StandardTableColumn<ParameterObservationRow>[] = [
    {
      key: "observedAtUtc",
      header: "Observed At",
      accessor: (row) => formatDate(row.observedAtUtc),
      sortable: true,
      minWidth: 180,
    },
    {
      key: "materialUnitId",
      header: "Material Id",
      accessor: (row) => shortenId(row.materialUnitId),
      minWidth: 140,
    },
    {
      key: "parameterDefinitionId",
      header: "Parameter",
      accessor: (row) => shortenId(row.parameterDefinitionId),
      minWidth: 150,
    },
    {
      key: "numericValue",
      header: "Numeric",
      accessor: (row) => formatNullableNumber(row.numericValue),
      sortable: true,
      align: "right",
      minWidth: 120,
    },
    {
      key: "textValue",
      header: "Text",
      accessor: (row) => row.textValue ?? "-",
      minWidth: 120,
    },
    {
      key: "unit",
      header: "Unit",
      accessor: (row) => row.unit ?? "-",
      minWidth: 90,
    },
    {
      key: "sourceSystem",
      header: "Source",
      accessor: (row) => row.sourceSystem ?? "-",
      sortable: true,
      minWidth: 150,
    },
  ];

  function clearInvestigationState() {
    setInvestigation(null);
    setFeatures(null);
    setRiskResult(null);
    setParameterRows([]);
    setParameterPageInfo(null);
    setActionError("");
  }

  function selectMaterial(row: DashboardMaterialRow) {
    setSelectedId(row.materialUnitId);
    mergeFilters({ materialCode: row.materialCode });
    clearInvestigationState();
  }

  async function searchMaterials() {
    setMaterialState((current) => ({ ...current, status: "loading", error: "" }));
    clearInvestigationState();

    try {
      const result = await plantProcessApi.searchDashboardMaterials({
        ...filters,
        materialCode: materialSearch.trim() || undefined,
        page: 1,
        pageSize: MATERIAL_PAGE_SIZE,
        sortBy: "materialCode",
        sortDirection: "asc",
      });

      setMaterials(result.items);
      setSelectedId(result.items[0]?.materialUnitId ?? "");
      setMaterialState({
        status: "success",
        error: "",
        lastSuccessfulQuery: materialSearch.trim(),
        totalCount: result.totalCount,
      });
      mergeFilters({ materialCode: materialSearch.trim() || undefined, page: 1 });
    } catch (err) {
      setMaterialState((current) => ({
        ...current,
        status: "error",
        error: err instanceof Error ? err.message : "Failed to search materials.",
      }));
    }
  }

  async function loadInvestigation() {
    if (!selectedId) {
      setActionError("Select a material before loading the investigation.");
      return;
    }

    setActionError("");
    setRiskResult(null);
    setIsLoadingInvestigation(true);
    setParameterRows([]);
    setParameterPageInfo(null);

    try {
      const [investigationResult, featuresResult] = await Promise.all([
        plantProcessApi.getMaterialInvestigation(selectedId, {
          maxDepth: 5,
          parameterPage: 1,
          parameterPageSize: PARAMETER_PAGE_SIZE,
        }),
        plantProcessApi.getMaterialFeatures(selectedId),
      ]);

      const typedInvestigation = investigationResult as MaterialInvestigationResponse;

      setInvestigation(typedInvestigation);
      setFeatures(featuresResult);
      setParameterRows(typedInvestigation.parameterObservations ?? []);
      setParameterPageInfo(typedInvestigation.parameterObservationPage ?? null);
    } catch (err) {
      setActionError(err instanceof Error ? err.message : "Failed to load material investigation.");
    } finally {
      setIsLoadingInvestigation(false);
    }
  }

  async function loadMoreParameters() {
    if (!selectedId || !parameterPageInfo?.hasMore) return;

    setActionError("");
    setIsLoadingMoreParameters(true);

    try {
      const nextPage = parameterPageInfo.page + 1;
      const investigationResult = (await plantProcessApi.getMaterialInvestigation(selectedId, {
        maxDepth: 5,
        parameterPage: nextPage,
        parameterPageSize: PARAMETER_PAGE_SIZE,
      })) as MaterialInvestigationResponse;

      const nextRows = investigationResult.parameterObservations ?? [];

      setParameterRows((currentRows) => {
        const mergedRows = [...currentRows, ...nextRows];

        setInvestigation((currentInvestigation) => ({
          ...(currentInvestigation ?? investigationResult),
          summary: investigationResult.summary ?? currentInvestigation?.summary ?? undefined,
          parameterObservationPage:
            investigationResult.parameterObservationPage ?? currentInvestigation?.parameterObservationPage,
          parameterObservations: mergedRows,
        }));

        return mergedRows;
      });

      setParameterPageInfo(investigationResult.parameterObservationPage ?? null);
    } catch (err) {
      setActionError(err instanceof Error ? err.message : "Failed to load more parameter observations.");
    } finally {
      setIsLoadingMoreParameters(false);
    }
  }

  async function calculateRisk() {
    if (!selectedId) {
      setActionError("Select a material before calculating risk.");
      return;
    }

    setActionError("");
    setIsCalculatingRisk(true);

    try {
      const result = await plantProcessApi.calculateRisk(selectedId);
      setRiskResult(result);
    } catch (err) {
      setActionError(err instanceof Error ? err.message : "Failed to calculate risk.");
    } finally {
      setIsCalculatingRisk(false);
    }
  }

  const isMaterialLoading = materialState.status === "loading";
  const hasMaterialError = materialState.status === "error";
  const hasSuccessfulSearch = materialState.status === "success";

  return (
    <section className="page-shell">
      <div className="page-title">
        <div>
          <h1>Material Search and Drilldown</h1>
          <p>
            Search material, batch, coil, lot or product unit, then inspect genealogy, process history,
            quality events, feature vector and risk.
          </p>
        </div>
      </div>

      {actionError ? <div className="error-box">{actionError}</div> : null}

      <div className="toolbar" role="search" aria-label="Material search actions">
        <StandardInput
          type="search"
          value={materialSearch}
          onChange={(value) => setMaterialSearch(value)}
          onKeyDown={(event) => {
            if (event.key === "Enter") void searchMaterials();
          }}
          placeholder="Search material code..."
          aria-label="Search material code"
          isLoading={isMaterialLoading}
        />

        <StandardButton
          variant="primary"
          leadingIcon={<Search size={16} />}
          isLoading={isMaterialLoading}
          onClick={searchMaterials}
        >
          Search
        </StandardButton>

        <StandardButton
          variant="secondary"
          leadingIcon={<FileSearch size={16} />}
          isDisabled={!selectedId}
          isLoading={isLoadingInvestigation}
          onClick={loadInvestigation}
        >
          Load Investigation
        </StandardButton>

        <StandardButton
          variant="primary"
          leadingIcon={<ShieldCheck size={16} />}
          isDisabled={!selectedId}
          isLoading={isCalculatingRisk}
          onClick={calculateRisk}
        >
          Calculate Risk
        </StandardButton>

        <StandardButton
          as="a"
          href={pdfUrl}
          target="_blank"
          rel="noreferrer"
          variant="ghost"
          leadingIcon={<Download size={16} />}
          isDisabled={!selectedId}
          ariaLabel="Open material investigation PDF report"
        >
          PDF Report
        </StandardButton>
      </div>

      <section className="dashboard-panel">
        <div className="panel-header">
          <div>
            <h3>Search Results</h3>
            <p>Click one material to drill down and synchronize global filters.</p>
          </div>
        </div>

        {hasSuccessfulSearch ? (
          <p className="muted-text" role="status">
            Showing {formatNumber(materials.length)} of {formatNumber(materialState.totalCount)} materials
            {materialState.lastSuccessfulQuery ? " for " + materialState.lastSuccessfulQuery : ""}.
          </p>
        ) : null}

        <StandardTable
          data={materials}
          columns={materialColumns}
          getRowKey={(row) => row.materialUnitId}
          caption="Material search results"
          isLoading={isMaterialLoading && materials.length === 0}
          hasError={hasMaterialError}
          errorMessage={materialState.error}
          onRetry={searchMaterials}
          emptyTitle="No materials found"
          emptyDescription="Try another material code or clear the global dashboard filters."
          selectionMode="single"
          selectedRowKeys={selectedId ? [selectedId] : []}
          onSelectionChange={(keys) => {
            const next = materials.find((row) => row.materialUnitId === keys[0]);
            if (next) selectMaterial(next);
          }}
          onRowClick={(row) => selectMaterial(row)}
          enableFiltering
          enableExport
          enableDensityToggle
          enablePagination
          defaultPageSize={25}
        />
      </section>

      <div className="metric-grid">
        <MetricCard title="Selected Material" value={selectedMaterial?.materialCode ?? "-"} />
        <MetricCard title="Type" value={selectedMaterial?.materialUnitType ?? "-"} />
        <MetricCard title="Risk Class" value={selectedMaterial?.latestRiskClass ?? "-"} />
        <MetricCard title="Defects" value={selectedMaterial?.defectEventCount ?? "-"} />
      </div>

      <DataFetchBoundary
        title="Investigation"
        isLoading={isLoadingInvestigation}
        isEmpty={!investigation && !isLoadingInvestigation}
        emptyTitle="No investigation loaded"
        emptyMessage="Select a material and use Load Investigation to fetch genealogy, process, quality and parameter data."
      >
        {investigation ? (
          <>
            <section className="dashboard-panel">
              <div className="panel-header">
                <div>
                  <h3>Investigation Summary</h3>
                  <p>
                    Genealogy-aware investigation for the selected material. Parameter observations are paginated to
                    keep the page safe for large customer datasets.
                  </p>
                </div>
              </div>

              <div className="metric-grid">
                <MetricCard title="Related Materials" value={investigation.summary?.materials ?? "-"} />
                <MetricCard title="Genealogy Edges" value={investigation.summary?.genealogyEdges ?? "-"} />
                <MetricCard title="Process Steps" value={investigation.summary?.processSteps ?? "-"} />
                <MetricCard
                  title="Parameters"
                  value={
                    investigation.summary?.parameterObservations !== undefined
                      ? formatNumber(parameterRows.length) + " / " + formatNumber(investigation.summary.parameterObservations)
                      : "-"
                  }
                  subtitle="Loaded / total"
                />
                <MetricCard title="Process Events" value={investigation.summary?.processEvents ?? "-"} />
                <MetricCard title="Downtime Events" value={investigation.summary?.downtimeEvents ?? "-"} />
                <MetricCard title="Quality Events" value={investigation.summary?.qualityEvents ?? "-"} />
                <MetricCard title="Risk Scores" value={investigation.summary?.riskScores ?? "-"} />
              </div>
            </section>

            <section className="dashboard-panel">
              <div className="panel-header">
                <div>
                  <h3>Parameter Observations</h3>
                  <p>
                    Showing {formatNumber(parameterRows.length)} of{" "}
                    {formatNumber(parameterPageInfo?.totalCount ?? investigation.summary?.parameterObservations ?? parameterRows.length)}
                    {" "}observations.
                  </p>
                </div>

                {parameterPageInfo?.hasMore ? (
                  <StandardButton
                    variant="secondary"
                    isLoading={isLoadingMoreParameters}
                    onClick={loadMoreParameters}
                  >
                    Show more
                  </StandardButton>
                ) : null}
              </div>

              <StandardTable
                data={parameterRows}
                columns={parameterColumns}
                getRowKey={(row, rowIndex) => row.id || row.sourceRecordId || String(rowIndex)}
                caption="Parameter observations for selected material"
                emptyTitle="No parameter observations"
                emptyDescription="This material scope has no mapped parameter observations in the current dataset."
                enableFiltering
                enableExport
                enableDensityToggle
                enablePagination
                enableVirtualization
                defaultPageSize={25}
              />

              {parameterPageInfo ? (
                <p className="muted-text">
                  Page {parameterPageInfo.page} of {parameterPageInfo.totalPages || 1}. Page size: {parameterPageInfo.pageSize}.
                </p>
              ) : null}
            </section>

            <section className="dashboard-panel">
              <div className="panel-header">
                <div>
                  <h3>Investigation Raw Payload</h3>
                  <p>Kept for engineering validation. Parameter rows above are the customer-safe paginated view.</p>
                </div>
              </div>

              <pre>{JSON.stringify(investigation, null, 2)}</pre>
            </section>
          </>
        ) : null}
      </DataFetchBoundary>

      {riskResult ? (
        <section className="dashboard-panel">
          <div className="panel-header">
            <div>
              <h3>Latest Risk Calculation</h3>
              <p>Rule-based risk score for the selected material.</p>
            </div>
          </div>

          <pre>{JSON.stringify(riskResult, null, 2)}</pre>
        </section>
      ) : null}

      {features ? (
        <section className="dashboard-panel">
          <div className="panel-header">
            <div>
              <h3>Feature Vector</h3>
              <p>Customer-like engineered feature vector used for risk, readiness and future ML workflows.</p>
            </div>
          </div>

          <pre>{JSON.stringify(features, null, 2)}</pre>
        </section>
      ) : null}
    </section>
  );
}

function formatDate(value?: string | null) {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString();
}

function formatNullableNumber(value?: number | null) {
  if (value === null || value === undefined) return "-";
  if (!Number.isFinite(value)) return String(value);
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 4 }).format(value);
}

function formatNumber(value?: number | null) {
  if (value === null || value === undefined) return "-";
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }).format(value);
}

function shortenId(value?: string | null) {
  if (!value) return "-";
  if (value.length <= 12) return value;
  return value.slice(0, 8) + "..." + value.slice(-4);
}
`);

write("eslint.config.js", String.raw`
import js from "@eslint/js";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import tseslint from "typescript-eslint";
import { defineConfig, globalIgnores } from "eslint/config";

const bannedLegacyUiImports = [
  { name: "@/components/hardening/StandardButton", message: "Use @/components/standard/StandardButton." },
  { name: "@/hardening/StandardButton", message: "Use @/components/standard/StandardButton." },
  { name: "@/components/hardening/DataFetchBoundary", message: "Use @/components/standard/DataFetchBoundary." },
  { name: "@/hardening/DataFetchBoundary", message: "Use @/components/standard/DataFetchBoundary." },
  { name: "@/components/table/StandardTable", message: "Use @/components/standard/StandardTable." },
  { name: "@/components/hardening/AppErrorBoundary", message: "Use @/components/ErrorBoundary." },
  { name: "@/hardening/RouteErrorBoundary", message: "Use @/components/ErrorBoundary." },
];

export default defineConfig([
  globalIgnores([
    "dist",
    "build",
    "coverage",
    "playwright-report",
    "test-results",
    "node_modules",
    "storybook-static",
  ]),

  {
    files: ["**/*.{ts,tsx}"],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      globals: {
        ...globals.browser,
        ...globals.node,
      },
    },
    rules: {
      "@typescript-eslint/no-explicit-any": "warn",
      "react-hooks/exhaustive-deps": "warn",
      "react-hooks/set-state-in-effect": "off",
      "no-restricted-imports": [
        "error",
        {
          paths: bannedLegacyUiImports,
          patterns: [
            {
              group: [
                "*/components/hardening/StandardButton",
                "*/hardening/StandardButton",
                "*/components/hardening/DataFetchBoundary",
                "*/hardening/DataFetchBoundary",
                "*/components/table/StandardTable",
                "*/components/hardening/AppErrorBoundary",
                "*/hardening/RouteErrorBoundary",
              ],
              message: "Use canonical UI components under src/components/standard or src/components/ErrorBoundary.",
            },
          ],
        },
      ],
    },
  },

  {
    files: ["src/pages/MaterialInvestigationPage.tsx"],
    rules: {
      "no-restricted-syntax": [
        "error",
        { selector: "JSXOpeningElement[name.name='input']", message: "Use StandardInput in Material Search." },
        { selector: "JSXOpeningElement[name.name='button']", message: "Use StandardButton in Material Search." },
        { selector: "JSXOpeningElement[name.name='a']", message: "Use StandardButton as='a' for PDF actions." },
        { selector: "JSXOpeningElement[name.name='SortableDataTable']", message: "Use StandardTable in Material Search." },
      ],
    },
  },

  {
    files: ["src/state/**/*.tsx"],
    rules: {
      "react-refresh/only-export-components": "off",
    },
  },
]);
`);

replaceInSourceFiles([
  [/from\s+["']@\/components\/hardening\/StandardButton["']/g, "from \"@/components/standard/StandardButton\""],
  [/from\s+["']@\/hardening\/StandardButton["']/g, "from \"@/components/standard/StandardButton\""],
  [/from\s+["']@\/components\/hardening\/DataFetchBoundary["']/g, "from \"@/components/standard/DataFetchBoundary\""],
  [/from\s+["']@\/hardening\/DataFetchBoundary["']/g, "from \"@/components/standard/DataFetchBoundary\""],
  [/from\s+["']@\/components\/table\/StandardTable["']/g, "from \"@/components/standard/StandardTable\""],
  [/from\s+["']@\/components\/hardening\/AppErrorBoundary["']/g, "from \"@/components/ErrorBoundary\""],
  [/from\s+["']@\/hardening\/RouteErrorBoundary["']/g, "from \"@/components/ErrorBoundary\""],
  [/from\s+["'](?:\.\.\/)+hardening\/StandardButton["']/g, "from \"@/components/standard/StandardButton\""],
  [/from\s+["'](?:\.\.\/)+hardening\/DataFetchBoundary["']/g, "from \"@/components/standard/DataFetchBoundary\""],
  [/from\s+["'](?:\.\.\/)+components\/table\/StandardTable["']/g, "from \"@/components/standard/StandardTable\""],
]);

remove("src/components/hardening/StandardButton.tsx");
remove("src/hardening/StandardButton.tsx");
remove("src/components/hardening/DataFetchBoundary.tsx");
remove("src/hardening/DataFetchBoundary.tsx");
remove("src/components/hardening/AppErrorBoundary.tsx");
remove("src/hardening/RouteErrorBoundary.tsx");
remove("src/components/table/StandardTable.tsx");
remove("src/pages/MaterialInvestigation/MaterialInvestigationPage.tsx");
removeEmptyDir("src/components/hardening");
removeEmptyDir("src/components/table");
removeEmptyDir("src/pages/MaterialInvestigation");

const packagePath = abs("package.json");
if (fs.existsSync(packagePath)) {
  const pkg = JSON.parse(fs.readFileSync(packagePath, "utf8"));
  pkg.scripts = pkg.scripts || {};
  pkg.scripts["validate:phase3-phase4:material"] = "npm run build && npm run lint";
  fs.writeFileSync(packagePath, JSON.stringify(pkg, null, 2) + "\n", "utf8");
  console.log("Updated package.json script validate:phase3-phase4:material");
}

console.log("Done. Run: npm run validate:phase3-phase4:material");
