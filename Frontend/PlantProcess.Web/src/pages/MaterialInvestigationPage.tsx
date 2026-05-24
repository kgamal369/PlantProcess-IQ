import { useEffect, useMemo, useState } from "react";
import { Download, Search, ShieldCheck } from "lucide-react";
import { plantProcessApi, type DashboardMaterialRow } from "@/api/plantProcessApi";
import { MetricCard } from "@/components/MetricCard";
import { SortableDataTable } from "@/components/SortableDataTable";
import type { SortableColumn } from "@/components/SortableDataTable";
import { useDashboardFilters } from "@/state/DashboardFilterContext";

const PARAMETER_PAGE_SIZE = 500;

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

export function MaterialInvestigationPage() {
  const { filters, mergeFilters } = useDashboardFilters();

  const [materials, setMaterials] = useState<DashboardMaterialRow[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [materialSearch, setMaterialSearch] = useState(filters.materialCode ?? "");

  const [investigation, setInvestigation] =
    useState<MaterialInvestigationResponse | null>(null);
  const [features, setFeatures] = useState<any>(null);
  const [riskResult, setRiskResult] = useState<any>(null);

  const [parameterRows, setParameterRows] = useState<ParameterObservationRow[]>([]);
  const [parameterPageInfo, setParameterPageInfo] =
    useState<ParameterObservationPageInfo | null>(null);

  const [error, setError] = useState("");
  const [isSearching, setIsSearching] = useState(false);
  const [isLoadingInvestigation, setIsLoadingInvestigation] = useState(false);
  const [isLoadingMoreParameters, setIsLoadingMoreParameters] = useState(false);
  const [isCalculatingRisk, setIsCalculatingRisk] = useState(false);

  useEffect(() => {
    let ignore = false;

    plantProcessApi
      .searchDashboardMaterials({
        ...filters,
        materialCode: materialSearch || filters.materialCode,
        page: 1,
        pageSize: 25,
        sortBy: "materialCode",
        sortDirection: "asc",
      })
      .then((result) => {
        if (ignore) return;

        setMaterials(result.items);

        if (result.items.length > 0 && !selectedId) {
          setSelectedId(result.items[0].materialUnitId);
        }
      })
      .catch((err) => {
        if (ignore) return;
        setError(err instanceof Error ? err.message : "Failed to load materials.");
      });

    return () => {
      ignore = true;
    };

    // Intentionally not depending on selectedId/materialSearch.
    // This effect reloads the material list when global filters change.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters.siteId, filters.sourceSystem, filters.riskClass]);

  const selectedMaterial = useMemo(
    () => materials.find((x) => x.materialUnitId === selectedId),
    [materials, selectedId]
  );

  const pdfUrl = selectedId
    ? plantProcessApi.getInvestigationPdfUrl(selectedId)
    : "#";

  const materialColumns: SortableColumn<DashboardMaterialRow>[] = [
    {
      key: "materialCode",
      title: "Material",
      render: (row) => (
        <button
          type="button"
          className="link-button"
          onClick={() => selectMaterial(row)}
        >
          {row.materialCode}
        </button>
      ),
    },
    {
      key: "materialUnitType",
      title: "Type",
      render: (row) => row.materialUnitType,
    },
    {
      key: "productFamily",
      title: "Family",
      render: (row) => row.productFamily ?? "-",
    },
    {
      key: "latestRiskClass",
      title: "Risk",
      render: (row) => row.latestRiskClass ?? "-",
    },
    {
      key: "defectEventCount",
      title: "Defects",
      align: "right",
      render: (row) => row.defectEventCount,
    },
  ];

  const parameterColumns: SortableColumn<ParameterObservationRow>[] = [
    {
      key: "observedAtUtc",
      title: "Observed At",
      render: (row) => formatDate(row.observedAtUtc),
    },
    {
      key: "materialUnitId",
      title: "Material Id",
      render: (row) => shortenId(row.materialUnitId),
    },
    {
      key: "parameterDefinitionId",
      title: "Parameter",
      render: (row) => shortenId(row.parameterDefinitionId),
    },
    {
      key: "numericValue",
      title: "Numeric",
      align: "right",
      render: (row) => formatNullableNumber(row.numericValue),
    },
    {
      key: "textValue",
      title: "Text",
      render: (row) => row.textValue ?? "-",
    },
    {
      key: "unit",
      title: "Unit",
      render: (row) => row.unit ?? "-",
    },
    {
      key: "sourceSystem",
      title: "Source",
      render: (row) => row.sourceSystem ?? "-",
    },
  ];

  function clearInvestigationState() {
    setInvestigation(null);
    setFeatures(null);
    setRiskResult(null);
    setParameterRows([]);
    setParameterPageInfo(null);
  }

  function selectMaterial(row: DashboardMaterialRow) {
    setSelectedId(row.materialUnitId);
    mergeFilters({ materialCode: row.materialCode });
    clearInvestigationState();
  }

  async function searchMaterials() {
    setError("");
    setIsSearching(true);
    clearInvestigationState();

    try {
      const result = await plantProcessApi.searchDashboardMaterials({
        ...filters,
        materialCode: materialSearch,
        page: 1,
        pageSize: 25,
        sortBy: "materialCode",
        sortDirection: "asc",
      });

      setMaterials(result.items);

      if (result.items.length > 0) {
        setSelectedId(result.items[0].materialUnitId);
      } else {
        setSelectedId("");
      }

      mergeFilters({ materialCode: materialSearch || undefined, page: 1 });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to search materials.");
    } finally {
      setIsSearching(false);
    }
  }

  async function loadInvestigation() {
    if (!selectedId) return;

    setError("");
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

      const typedInvestigation =
        investigationResult as MaterialInvestigationResponse;

      setInvestigation(typedInvestigation);
      setFeatures(featuresResult);
      setParameterRows(typedInvestigation.parameterObservations ?? []);
      setParameterPageInfo(typedInvestigation.parameterObservationPage ?? null);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to load material investigation."
      );
    } finally {
      setIsLoadingInvestigation(false);
    }
  }

  async function loadMoreParameters() {
    if (!selectedId || !parameterPageInfo?.hasMore) return;

    setError("");
    setIsLoadingMoreParameters(true);

    try {
      const nextPage = parameterPageInfo.page + 1;

      const investigationResult = (await plantProcessApi.getMaterialInvestigation(
        selectedId,
        {
          maxDepth: 5,
          parameterPage: nextPage,
          parameterPageSize: PARAMETER_PAGE_SIZE,
        }
      )) as MaterialInvestigationResponse;

      const nextRows = investigationResult.parameterObservations ?? [];

      setParameterRows((currentRows) => {
        const mergedRows = [...currentRows, ...nextRows];

        setInvestigation((currentInvestigation) => ({
          ...(currentInvestigation ?? investigationResult),
          summary:
            investigationResult.summary ??
            currentInvestigation?.summary ??
            undefined,
          parameterObservationPage:
            investigationResult.parameterObservationPage ??
            currentInvestigation?.parameterObservationPage,
          parameterObservations: mergedRows,
        }));

        return mergedRows;
      });

      setParameterPageInfo(investigationResult.parameterObservationPage ?? null);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Failed to load more parameter observations."
      );
    } finally {
      setIsLoadingMoreParameters(false);
    }
  }

  async function calculateRisk() {
    if (!selectedId) return;

    setError("");
    setIsCalculatingRisk(true);

    try {
      const result = await plantProcessApi.calculateRisk(selectedId);
      setRiskResult(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to calculate risk.");
    } finally {
      setIsCalculatingRisk(false);
    }
  }

  return (
    <section className="page-shell">
      <div className="page-title">
        <div>
          <h1>Material Search and Drilldown</h1>
          <p>
            Search material, batch, coil, lot or product unit, then inspect
            genealogy, process history, quality events, feature vector and risk.
          </p>
        </div>
      </div>

      {error ? <div className="error-box">{error}</div> : null}

      <div className="toolbar">
        <input
          value={materialSearch}
          onChange={(event) => setMaterialSearch(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              void searchMaterials();
            }
          }}
          placeholder="Search material code..."
        />

        <button type="button" onClick={searchMaterials} disabled={isSearching}>
          <Search size={16} />
          {isSearching ? "Searching..." : "Search"}
        </button>

        <button
          type="button"
          onClick={loadInvestigation}
          disabled={!selectedId || isLoadingInvestigation}
        >
          <Search size={16} />
          {isLoadingInvestigation ? "Loading..." : "Load Investigation"}
        </button>

        <button
          type="button"
          onClick={calculateRisk}
          disabled={!selectedId || isCalculatingRisk}
        >
          <ShieldCheck size={16} />
          {isCalculatingRisk ? "Calculating..." : "Calculate Risk"}
        </button>

        <a
          className="button-link"
          href={pdfUrl}
          target="_blank"
          rel="noreferrer"
          aria-disabled={!selectedId}
        >
          <Download size={16} />
          PDF Report
        </a>
      </div>

      <section className="dashboard-panel">
        <div className="panel-header">
          <div>
            <h3>Search Results</h3>
            <p>Click one material to drill down and synchronize global filters.</p>
          </div>
        </div>

        <SortableDataTable
          rows={materials}
          columns={materialColumns}
          emptyText="No materials found."
        />
      </section>

      <div className="metric-grid">
        <MetricCard
          title="Selected Material"
          value={selectedMaterial?.materialCode ?? "-"}
        />
        <MetricCard
          title="Type"
          value={selectedMaterial?.materialUnitType ?? "-"}
        />
        <MetricCard
          title="Risk Class"
          value={selectedMaterial?.latestRiskClass ?? "-"}
        />
        <MetricCard
          title="Defects"
          value={selectedMaterial?.defectEventCount ?? "-"}
        />
      </div>

      {investigation ? (
        <>
          <section className="dashboard-panel">
            <div className="panel-header">
              <div>
                <h3>Investigation Summary</h3>
                <p>
                  Genealogy-aware investigation for the selected material.
                  Parameter observations are paginated to keep the page safe
                  for large customer datasets.
                </p>
              </div>
            </div>

            <div className="metric-grid">
              <MetricCard
                title="Related Materials"
                value={investigation.summary?.materials ?? "-"}
              />
              <MetricCard
                title="Genealogy Edges"
                value={investigation.summary?.genealogyEdges ?? "-"}
              />
              <MetricCard
                title="Process Steps"
                value={investigation.summary?.processSteps ?? "-"}
              />
              <MetricCard
                title="Parameters"
                value={
                  investigation.summary?.parameterObservations !== undefined
                    ? `${formatNumber(parameterRows.length)} / ${formatNumber(
                        investigation.summary.parameterObservations
                      )}`
                    : "-"
                }
                subtitle="Loaded / total"
              />
              <MetricCard
                title="Process Events"
                value={investigation.summary?.processEvents ?? "-"}
              />
              <MetricCard
                title="Downtime Events"
                value={investigation.summary?.downtimeEvents ?? "-"}
              />
              <MetricCard
                title="Quality Events"
                value={investigation.summary?.qualityEvents ?? "-"}
              />
              <MetricCard
                title="Risk Scores"
                value={investigation.summary?.riskScores ?? "-"}
              />
            </div>
          </section>

          <section className="dashboard-panel">
            <div className="panel-header">
              <div>
                <h3>Parameter Observations</h3>
                <p>
                  Showing {formatNumber(parameterRows.length)} of{" "}
                  {formatNumber(
                    parameterPageInfo?.totalCount ??
                      investigation.summary?.parameterObservations ??
                      parameterRows.length
                  )}{" "}
                  observations.
                </p>
              </div>

              {parameterPageInfo?.hasMore ? (
                <button
                  type="button"
                  onClick={loadMoreParameters}
                  disabled={isLoadingMoreParameters}
                >
                  {isLoadingMoreParameters ? "Loading..." : "Show more"}
                </button>
              ) : null}
            </div>

            <SortableDataTable
              rows={parameterRows}
              columns={parameterColumns}
              emptyText="No parameter observations found for this material scope."
            />

            {parameterPageInfo ? (
              <p className="muted-text">
                Page {parameterPageInfo.page} of{" "}
                {parameterPageInfo.totalPages || 1}. Page size:{" "}
                {parameterPageInfo.pageSize}.
              </p>
            ) : null}
          </section>

          <section className="dashboard-panel">
            <div className="panel-header">
              <div>
                <h3>Investigation Raw Payload</h3>
                <p>
                  Kept for engineering validation. Parameter rows above are the
                  customer-safe paginated view.
                </p>
              </div>
            </div>

            <pre>{JSON.stringify(investigation, null, 2)}</pre>
          </section>
        </>
      ) : null}

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
              <p>
                Customer-like engineered feature vector used for risk,
                readiness and future ML workflows.
              </p>
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

  return new Intl.NumberFormat(undefined, {
    maximumFractionDigits: 4,
  }).format(value);
}

function formatNumber(value?: number | null) {
  if (value === null || value === undefined) return "-";

  return new Intl.NumberFormat(undefined, {
    maximumFractionDigits: 0,
  }).format(value);
}

function shortenId(value?: string | null) {
  if (!value) return "-";

  if (value.length <= 12) return value;

  return `${value.slice(0, 8)}...${value.slice(-4)}`;
}