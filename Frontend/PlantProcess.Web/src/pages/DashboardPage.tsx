import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import {
  Activity,
  AlertTriangle,
  BarChart3,
  Database,
  Factory,
  Gauge,
  Grid3X3,
  Maximize2,
  RefreshCw,
  ScatterChart as ScatterIcon,
  ShieldAlert,
  Workflow,
} from "lucide-react";
import { plantProcessApi } from "../api/plantProcessApi";
import type {
  DashboardMaterialRow,
  DashboardWorkspace,
  SortDirection,
} from "../api/plantProcessApi";
import { ActiveFilterChips } from "../components/ActiveFilterChips";
import { DashboardFilterBar } from "../components/DashboardFilterBar";
import { ErrorPanel, LoadingPanel } from "../components/AsyncState";
import { SortableDataTable } from "../components/SortableDataTable";
import type { SortableColumn } from "../components/SortableDataTable";
import { DashboardWidgetCard } from "../components/dashboard/DashboardWidgetCard";
import { DashboardGridLayout } from "../components/dashboard/DashboardGridLayout";
import { DrilldownDrawer } from "../components/dashboard/DrilldownDrawer";
import { SelectionBreadcrumb } from "../components/dashboard/SelectionBreadcrumb";
import {
  InteractiveBarChart,
  InteractiveHeatmap,
  InteractiveLineChart,
  InteractivePieChart,
  InteractiveScatterChart,
} from "../components/charts/InteractiveCharts";
import type { ChartRow } from "../components/charts/InteractiveCharts";
import { useDashboardFilters } from "../state/DashboardFilterContext";
import { useDashboardSelections } from "../state/DashboardSelectionContext";

export function DashboardPage() {
  const { filters, setFilter } = useDashboardFilters();
  const { applySelection, openDrilldown, getWidgetState } =
    useDashboardSelections();

  const [workspace, setWorkspace] = useState<DashboardWorkspace | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshingReadModels, setIsRefreshingReadModels] = useState(false);
  const [error, setError] = useState<unknown>(null);

  async function load() {
    setIsLoading(true);
    setError(null);

    try {
      const result = await plantProcessApi.getDashboardWorkspace({
        ...filters,
        page: filters.page ?? 1,
        pageSize: filters.pageSize ?? 25,
      });
      setWorkspace(result);
    } catch (loadError) {
      setError(loadError);
    } finally {
      setIsLoading(false);
    }
  }

  async function refreshReadModels() {
    setIsRefreshingReadModels(true);
    setError(null);

    try {
      await plantProcessApi.refreshDashboardReadModels();
      await load();
    } catch (refreshError) {
      setError(refreshError);
    } finally {
      setIsRefreshingReadModels(false);
    }
  }

  useEffect(() => {
    let ignore = false;

    async function loadSafe() {
      setIsLoading(true);
      setError(null);

      try {
        const result = await plantProcessApi.getDashboardWorkspace({
          ...filters,
          page: filters.page ?? 1,
          pageSize: filters.pageSize ?? 25,
        });

        if (!ignore) setWorkspace(result);
      } catch (loadError) {
        if (!ignore) setError(loadError);
      } finally {
        if (!ignore) setIsLoading(false);
      }
    }

    loadSafe();

    return () => {
      ignore = true;
    };
  }, [filters]);

  const overview = workspace?.overview;
  const quality = workspace?.quality;
  const risk = workspace?.risk;
  const dataQuality = workspace?.dataQuality;
  const materials = workspace?.materials;

  const trendData = useMemo<ChartRow[]>(() => {
    return (overview?.defectTrend ?? []).map((x: any) => ({
      date: new Date(x.dateUtc).toLocaleDateString(),
      isoDate: x.dateUtc,
      defectRate: Number(x.defectRatePercent ?? 0),
      defects: Number(x.defectEventCount ?? 0),
      materials: Number(x.materialCount ?? 0),
    }));
  }, [overview]);

  const defectData = useMemo<ChartRow[]>(() => {
    return (quality?.defectBreakdown ?? []).map((x: any) => ({
      name: x.defectCode ?? x.defectName ?? "Unknown",
      label: x.defectName ?? x.defectCode ?? "Unknown",
      count: Number(x.count ?? 0),
      percent: Number(x.percentOfDefects ?? 0),
    }));
  }, [quality]);

  const riskClassData = useMemo<ChartRow[]>(() => {
    return (risk?.riskClassBreakdown ?? []).map((x: any) => ({
      riskClass: x.riskClass ?? "Unknown",
      count: Number(x.count ?? 0),
      percent: Number(x.percent ?? 0),
    }));
  }, [risk]);

  const sourceContributionData = useMemo<ChartRow[]>(() => {
    const grouped = new Map<string, number>();

    for (const row of materials?.items ?? []) {
      const source = row.sourceSystem || "Unknown";
      grouped.set(source, (grouped.get(source) ?? 0) + 1);
    }

    return Array.from(grouped.entries()).map(([sourceSystem, count]) => ({
      sourceSystem,
      count,
    }));
  }, [materials]);

  const riskScatterData = useMemo<ChartRow[]>(() => {
    return (materials?.items ?? [])
      .filter(
        (row) =>
          row.latestRiskScore !== undefined && row.latestRiskScore !== null
      )
      .map((row) => ({
        materialCode: row.materialCode,
        riskScore: Number(row.latestRiskScore ?? 0),
        defects: row.defectEventCount,
        processSteps: row.processStepCount,
        parameterObservations: row.parameterObservationCount,
      }));
  }, [materials]);

  const heatmapData = useMemo<ChartRow[]>(() => {
    const grouped = new Map<string, number>();

    for (const row of materials?.items ?? []) {
      const riskClass = row.latestRiskClass || "Unknown";
      const materialType = row.materialUnitType || "Unknown";
      const key = `${riskClass}||${materialType}`;
      grouped.set(key, (grouped.get(key) ?? 0) + 1);
    }

    return Array.from(grouped.entries()).map(([key, count]) => {
      const [riskClass, materialType] = key.split("||");
      return {
        riskClass,
        materialType,
        count,
      };
    });
  }, [materials]);

  const dataQualityRows = useMemo<Record<string, unknown>[]>(() => {
    return (dataQuality?.issueTypeBreakdown ?? []).map((x: any) => ({
      issueType: x.code,
      count: x.count,
      percent: x.percent,
    }));
  }, [dataQuality]);

  const topContributors = useMemo<ChartRow[]>(() => {
    return (overview?.topRiskContributors ?? []).map((x: any) => ({
      contributorCode: x.contributorCode ?? x.contributorType ?? "Unknown",
      count: Number(x.count ?? 0),
      averageRiskScore: Number(x.averageRiskScore ?? 0),
    }));
  }, [overview]);

  const materialColumns: SortableColumn<DashboardMaterialRow>[] = [
    {
      key: "materialCode",
      title: "Material",
      sortable: true,
      render: (row) => (
        <button
          className="link-button"
          onClick={() => {
            applySelection({
              type: "material",
              field: "materialCode",
              value: row.materialCode,
              label: row.materialCode,
              sourceWidget: "Material Explorer",
            });

            openDrilldown({
              title: row.materialCode,
              subtitle: "Material drilldown",
              type: "material",
              payload: row,
            });
          }}
          type="button"
        >
          {row.materialCode}
        </button>
      ),
    },
    {
      key: "materialUnitType",
      title: "Type",
      sortable: true,
      render: (row) => row.materialUnitType,
    },
    {
      key: "productFamily",
      title: "Family",
      sortable: true,
      render: (row) => row.productFamily ?? "-",
    },
    {
      key: "gradeOrRecipe",
      title: "Grade / Recipe",
      render: (row) => row.gradeOrRecipe ?? "-",
    },
    {
      key: "sourceSystem",
      title: "Source",
      render: (row) => (
        <button
          className="link-button"
          onClick={() =>
            applySelection({
              type: "sourceSystem",
              field: "sourceSystem",
              value: row.sourceSystem ?? "Unknown",
              label: row.sourceSystem ?? "Unknown",
              sourceWidget: "Material Explorer",
            })
          }
          type="button"
        >
          {row.sourceSystem ?? "-"}
        </button>
      ),
    },
    {
      key: "processStepCount",
      title: "Steps",
      align: "right",
      render: (row) => row.processStepCount,
    },
    {
      key: "parameterObservationCount",
      title: "Parameters",
      align: "right",
      render: (row) => row.parameterObservationCount,
    },
    {
      key: "defectEventCount",
      title: "Defects",
      align: "right",
      render: (row) => row.defectEventCount,
    },
    {
      key: "latestRiskScore",
      title: "Risk",
      align: "right",
      render: (row) =>
        row.latestRiskScore == null ? "-" : formatNumber(row.latestRiskScore),
    },
    {
      key: "latestRiskClass",
      title: "Risk Class",
      render: (row) =>
        row.latestRiskClass ? (
          <button
            className={`risk-pill risk-${row.latestRiskClass.toLowerCase()}`}
            onClick={() =>
              applySelection({
                type: "riskClass",
                field: "riskClass",
                value: row.latestRiskClass!,
                label: row.latestRiskClass!,
                sourceWidget: "Material Explorer",
              })
            }
            type="button"
          >
            {row.latestRiskClass}
          </button>
        ) : (
          "-"
        ),
    },
    {
      key: "productionStartUtc",
      title: "Start",
      sortable: true,
      render: (row) =>
        row.productionStartUtc
          ? new Date(row.productionStartUtc).toLocaleString()
          : "-",
    },
  ];

  function updateSort(sortBy: string, sortDirection: SortDirection) {
    setFilter("sortBy", sortBy);
    setFilter("sortDirection", sortDirection);
    setFilter("page", 1);
  }

  const defectChartType = getWidgetState("defectBreakdown").chartType ?? "bar";
  const trendChartType = getWidgetState("defectTrend").chartType ?? "line";
  const riskChartType = getWidgetState("riskDistribution").chartType ?? "donut";
  const sourceChartType =
    getWidgetState("sourceContribution").chartType ?? "bar";

  return (
    <main className="page-shell advanced-dashboard-shell">
      <section className="dashboard-hero">
        <div>
          <div className="eyebrow">
            <Maximize2 size={14} />
            Advanced interactive workspace
          </div>
          <h1>PlantProcess IQ Manufacturing Intelligence</h1>
          <p>
            Click any chart, widget, or material row to filter the full
            dashboard. Drag and resize widgets to build your own analysis
            layout.
          </p>
        </div>

        <button
          className="primary-button"
          onClick={refreshReadModels}
          disabled={isRefreshingReadModels}
          type="button"
        >
          <RefreshCw size={16} />
          {isRefreshingReadModels ? "Refreshing..." : "Refresh read models"}
        </button>
      </section>

      <DashboardFilterBar />
      <ActiveFilterChips />
      <SelectionBreadcrumb />

      {isLoading ? <LoadingPanel /> : null}
      {error ? <ErrorPanel error={error} /> : null}

      {!isLoading && !error && workspace ? (
        <>
          <section className="metric-grid">
            <InteractiveMetric
              icon={<Factory size={20} />}
              label="Materials"
              value={overview?.materials ?? 0}
              note="Filtered canonical material population"
            />

            <InteractiveMetric
              icon={<Workflow size={20} />}
              label="Process Steps"
              value={overview?.processSteps ?? 0}
              note="Filtered process executions"
            />

            <InteractiveMetric
              icon={<Database size={20} />}
              label="Parameter Observations"
              value={overview?.parameterObservations ?? 0}
              note="Aggregated dashboard data"
            />

            <InteractiveMetric
              icon={<ShieldAlert size={20} />}
              label="Defect Rate"
              value={`${formatNumber(overview?.defectRatePercent ?? 0)}%`}
              note="Click to focus default defect"
              accent="danger"
              onClick={() =>
                applySelection({
                  type: "defect",
                  field: "defectType",
                  value: filters.defectType || "SurfaceCrack",
                  label: filters.defectType || "SurfaceCrack",
                  sourceWidget: "KPI Defect Rate",
                })
              }
            />

            <InteractiveMetric
              icon={<Gauge size={20} />}
              label="High Risk Materials"
              value={overview?.highRiskMaterials ?? 0}
              note={`${formatNumber(
                overview?.highRiskRatePercent ?? 0
              )}% high risk`}
              accent="warning"
              onClick={() =>
                applySelection({
                  type: "riskClass",
                  field: "riskClass",
                  value: "High",
                  label: "High",
                  sourceWidget: "KPI High Risk Materials",
                })
              }
            />

            <InteractiveMetric
              icon={<AlertTriangle size={20} />}
              label="Data Quality Issues"
              value={overview?.dataQualityIssues ?? 0}
              note="Detected validation findings"
            />
          </section>

          <DashboardGridLayout>
            <div key="defectTrend">
              <DashboardWidgetCard
                widgetId="defectTrend"
                title="Defect Trend"
                subtitle="Click a date point to filter from that date."
                icon={<Activity size={18} />}
                chartTypes={["line", "area", "bar", "table"]}
                exportRows={trendData as Record<string, unknown>[]}
              >
                {trendChartType === "line" ? (
                  <InteractiveLineChart
                    data={trendData}
                    categoryKey="date"
                    valueKey="defectRate"
                    selection={{
                      type: "dateRange",
                      field: "fromUtc",
                      sourceWidget: "Defect Trend",
                      valueKey: "isoDate",
                      labelKey: "date",
                    }}
                  />
                ) : trendChartType === "area" ? (
                  <InteractiveLineChart
                    data={trendData}
                    categoryKey="date"
                    valueKey="defectRate"
                    area
                    selection={{
                      type: "dateRange",
                      field: "fromUtc",
                      sourceWidget: "Defect Trend",
                      valueKey: "isoDate",
                      labelKey: "date",
                    }}
                  />
                ) : trendChartType === "bar" ? (
                  <InteractiveBarChart
                    data={trendData}
                    categoryKey="date"
                    valueKey="defects"
                    selection={{
                      type: "dateRange",
                      field: "fromUtc",
                      sourceWidget: "Defect Trend",
                      valueKey: "isoDate",
                      labelKey: "date",
                    }}
                  />
                ) : (
                  <MiniTable rows={trendData} />
                )}
              </DashboardWidgetCard>
            </div>

            <div key="defectBreakdown">
              <DashboardWidgetCard
                widgetId="defectBreakdown"
                title="Defect Breakdown"
                subtitle="Click bar/pie/donut slice to filter defect type."
                icon={<BarChart3 size={18} />}
                chartTypes={["bar", "pie", "donut", "table"]}
                exportRows={defectData as Record<string, unknown>[]}
              >
                {defectChartType === "bar" ? (
                  <InteractiveBarChart
                    data={defectData}
                    categoryKey="name"
                    valueKey="count"
                    selection={{
                      type: "defect",
                      field: "defectType",
                      sourceWidget: "Defect Breakdown",
                      valueKey: "name",
                      labelKey: "label",
                    }}
                  />
                ) : defectChartType === "pie" ? (
                  <InteractivePieChart
                    data={defectData}
                    categoryKey="name"
                    valueKey="count"
                    selection={{
                      type: "defect",
                      field: "defectType",
                      sourceWidget: "Defect Breakdown",
                      valueKey: "name",
                      labelKey: "label",
                    }}
                  />
                ) : defectChartType === "donut" ? (
                  <InteractivePieChart
                    data={defectData}
                    categoryKey="name"
                    valueKey="count"
                    donut
                    selection={{
                      type: "defect",
                      field: "defectType",
                      sourceWidget: "Defect Breakdown",
                      valueKey: "name",
                      labelKey: "label",
                    }}
                  />
                ) : (
                  <MiniTable rows={defectData} />
                )}
              </DashboardWidgetCard>
            </div>

            <div key="riskDistribution">
              <DashboardWidgetCard
                widgetId="riskDistribution"
                title="Risk Class Distribution"
                subtitle="Click class to filter materials by risk."
                icon={<Gauge size={18} />}
                chartTypes={["donut", "pie", "bar", "table"]}
                exportRows={riskClassData as Record<string, unknown>[]}
              >
                {riskChartType === "donut" ? (
                  <InteractivePieChart
                    data={riskClassData}
                    categoryKey="riskClass"
                    valueKey="count"
                    donut
                    selection={{
                      type: "riskClass",
                      field: "riskClass",
                      sourceWidget: "Risk Distribution",
                      valueKey: "riskClass",
                      labelKey: "riskClass",
                    }}
                  />
                ) : riskChartType === "pie" ? (
                  <InteractivePieChart
                    data={riskClassData}
                    categoryKey="riskClass"
                    valueKey="count"
                    selection={{
                      type: "riskClass",
                      field: "riskClass",
                      sourceWidget: "Risk Distribution",
                      valueKey: "riskClass",
                      labelKey: "riskClass",
                    }}
                  />
                ) : riskChartType === "bar" ? (
                  <InteractiveBarChart
                    data={riskClassData}
                    categoryKey="riskClass"
                    valueKey="count"
                    selection={{
                      type: "riskClass",
                      field: "riskClass",
                      sourceWidget: "Risk Distribution",
                      valueKey: "riskClass",
                      labelKey: "riskClass",
                    }}
                  />
                ) : (
                  <MiniTable rows={riskClassData} />
                )}
              </DashboardWidgetCard>
            </div>

            <div key="sourceContribution">
              <DashboardWidgetCard
                widgetId="sourceContribution"
                title="Source System Contribution"
                subtitle="Click source to filter by source system."
                icon={<Database size={18} />}
                chartTypes={["bar", "pie", "donut", "table"]}
                exportRows={sourceContributionData as Record<string, unknown>[]}
              >
                {sourceChartType === "bar" ? (
                  <InteractiveBarChart
                    data={sourceContributionData}
                    categoryKey="sourceSystem"
                    valueKey="count"
                    selection={{
                      type: "sourceSystem",
                      field: "sourceSystem",
                      sourceWidget: "Source Contribution",
                      valueKey: "sourceSystem",
                      labelKey: "sourceSystem",
                    }}
                  />
                ) : sourceChartType === "pie" ? (
                  <InteractivePieChart
                    data={sourceContributionData}
                    categoryKey="sourceSystem"
                    valueKey="count"
                    selection={{
                      type: "sourceSystem",
                      field: "sourceSystem",
                      sourceWidget: "Source Contribution",
                      valueKey: "sourceSystem",
                      labelKey: "sourceSystem",
                    }}
                  />
                ) : sourceChartType === "donut" ? (
                  <InteractivePieChart
                    data={sourceContributionData}
                    categoryKey="sourceSystem"
                    valueKey="count"
                    donut
                    selection={{
                      type: "sourceSystem",
                      field: "sourceSystem",
                      sourceWidget: "Source Contribution",
                      valueKey: "sourceSystem",
                      labelKey: "sourceSystem",
                    }}
                  />
                ) : (
                  <MiniTable rows={sourceContributionData} />
                )}
              </DashboardWidgetCard>
            </div>

            <div key="riskScatter">
              <DashboardWidgetCard
                widgetId="riskScatter"
                title="Risk vs Defect Scatter"
                subtitle="Click a material point to filter and open drilldown."
                icon={<ScatterIcon size={18} />}
                chartTypes={["scatter", "table"]}
                exportRows={riskScatterData as Record<string, unknown>[]}
              >
                {(getWidgetState("riskScatter").chartType ?? "scatter") ===
                "scatter" ? (
                  <InteractiveScatterChart
                    data={riskScatterData}
                    xKey="defects"
                    yKey="riskScore"
                    zKey="parameterObservations"
                    labelKey="materialCode"
                    selection={{
                      type: "material",
                      field: "materialCode",
                      sourceWidget: "Risk vs Defect Scatter",
                      valueKey: "materialCode",
                      labelKey: "materialCode",
                    }}
                  />
                ) : (
                  <MiniTable rows={riskScatterData} />
                )}
              </DashboardWidgetCard>
            </div>

            <div key="qualityHeatmap">
              <DashboardWidgetCard
                widgetId="qualityHeatmap"
                title="Quality Heatmap"
                subtitle="Material type vs risk class density from current result set."
                icon={<Grid3X3 size={18} />}
                chartTypes={["heatmap", "table"]}
                exportRows={heatmapData as Record<string, unknown>[]}
              >
                {(getWidgetState("qualityHeatmap").chartType ?? "heatmap") ===
                "heatmap" ? (
                  <InteractiveHeatmap
                    data={heatmapData}
                    xKey="materialType"
                    yKey="riskClass"
                    valueKey="count"
                    selection={{
                      type: "riskClass",
                      field: "riskClass",
                      sourceWidget: "Quality Heatmap",
                      valueKey: "riskClass",
                      labelKey: "riskClass",
                    }}
                  />
                ) : (
                  <MiniTable rows={heatmapData} />
                )}
              </DashboardWidgetCard>
            </div>

            <div key="topContributors">
              <DashboardWidgetCard
                widgetId="topContributors"
                title="Top Risk Contributors"
                subtitle="Click contributor to use it as selected parameter."
                icon={<Activity size={18} />}
                chartTypes={["bar", "table"]}
                exportRows={topContributors as Record<string, unknown>[]}
              >
                {(getWidgetState("topContributors").chartType ?? "bar") ===
                "bar" ? (
                  <InteractiveBarChart
                    data={topContributors}
                    categoryKey="contributorCode"
                    valueKey="count"
                    selection={{
                      type: "parameter",
                      field: "parameterCode",
                      sourceWidget: "Top Risk Contributors",
                      valueKey: "contributorCode",
                      labelKey: "contributorCode",
                    }}
                  />
                ) : (
                  <MiniTable rows={topContributors} />
                )}
              </DashboardWidgetCard>
            </div>

            <div key="dataQuality">
              <DashboardWidgetCard
                widgetId="dataQuality"
                title="Data Quality"
                subtitle="Readiness signals for selected material population."
                icon={<AlertTriangle size={18} />}
                chartTypes={["table", "bar"]}
                exportRows={dataQualityRows}
              >
                {(getWidgetState("dataQuality").chartType ?? "table") ===
                "bar" ? (
                  <InteractiveBarChart
                    data={dataQualityRows as ChartRow[]}
                    categoryKey="issueType"
                    valueKey="count"
                    selection={{
                      type: "generic",
                      field: "materialCode",
                      sourceWidget: "Data Quality",
                      valueKey: "issueType",
                      labelKey: "issueType",
                    }}
                  />
                ) : (
                  <MiniTable rows={dataQualityRows as ChartRow[]} />
                )}
              </DashboardWidgetCard>
            </div>

            <div key="materialExplorer">
              <DashboardWidgetCard
                widgetId="materialExplorer"
                title="Material Explorer"
                subtitle="Backend paginated and sortable. Click material/source/risk values to filter."
                icon={<Factory size={18} />}
                chartTypes={["table"]}
                exportRows={
                  (materials?.items ?? []) as unknown as Record<
                    string,
                    unknown
                  >[]
                }
              >
                <SortableDataTable
                  rows={materials?.items ?? []}
                  columns={materialColumns}
                  sortBy={filters.sortBy}
                  sortDirection={filters.sortDirection ?? "desc"}
                  onSort={updateSort}
                  emptyText="No materials match the selected filters."
                />

                <div className="pagination-row">
                  <button
                    className="secondary-button"
                    disabled={(materials?.page ?? 1) <= 1}
                    onClick={() =>
                      setFilter(
                        "page",
                        Math.max((materials?.page ?? 1) - 1, 1)
                      )
                    }
                    type="button"
                  >
                    Previous
                  </button>

                  <span>
                    Page {materials?.page ?? 1} / {materials?.totalPages ?? 0} —
                    {materials?.totalCount ?? 0} materials
                  </span>

                  <button
                    className="secondary-button"
                    disabled={
                      (materials?.page ?? 1) >= (materials?.totalPages ?? 0)
                    }
                    onClick={() =>
                      setFilter("page", (materials?.page ?? 1) + 1)
                    }
                    type="button"
                  >
                    Next
                  </button>

                  <select
                    value={filters.pageSize ?? 25}
                    onChange={(event) => {
                      setFilter("pageSize", Number(event.target.value));
                      setFilter("page", 1);
                    }}
                  >
                    <option value={10}>10 rows</option>
                    <option value={25}>25 rows</option>
                    <option value={50}>50 rows</option>
                    <option value={100}>100 rows</option>
                  </select>
                </div>
              </DashboardWidgetCard>
            </div>
          </DashboardGridLayout>

          <DrilldownDrawer />
        </>
      ) : null}
    </main>
  );
}

function InteractiveMetric({
  icon,
  label,
  value,
  note,
  accent,
  onClick,
}: {
  icon: ReactNode;
  label: string;
  value: string | number;
  note: string;
  accent?: "danger" | "warning";
  onClick?: () => void;
}) {
  return (
    <button
      className={`metric-tile ${accent ? `metric-tile--${accent}` : ""}`}
      onClick={onClick}
      type="button"
    >
      <div className="metric-icon">{icon}</div>
      <div>
        <span>{label}</span>
        <strong>{value}</strong>
        <small>{note}</small>
      </div>
    </button>
  );
}

function MiniTable({ rows }: { rows: ChartRow[] }) {
  if (!rows.length) {
    return (
      <div className="empty-insight">
        <strong>No data</strong>
        <p>No records are available for this widget and filter context.</p>
      </div>
    );
  }

  const columns = Object.keys(rows[0]);

  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column}>{column}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={index}>
              {columns.map((column) => (
                <td key={column}>{formatCell(row[column])}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function formatCell(value: unknown) {
  if (value === null || value === undefined) return "-";
  if (typeof value === "number") return formatNumber(value);
  return String(value);
}

function formatNumber(value: number) {
  return new Intl.NumberFormat(undefined, {
    maximumFractionDigits: 2,
  }).format(value);
}