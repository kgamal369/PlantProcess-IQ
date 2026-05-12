import { useEffect, useMemo, useState } from "react";
import { plantProcessApi } from "../api/plantProcessApi";

import {
  Activity,
  AlertTriangle,
  BarChart3,
  Database,
  Factory,
  Gauge,
  RefreshCw,
  ShieldAlert,
  Workflow,
} from "lucide-react";

import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";

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
import { useDashboardFilters } from "../state/DashboardFilterContext";

export function DashboardPage() {
  const { filters, mergeFilters, setFilter } = useDashboardFilters();
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

  const trendData = useMemo(() => {
    return (overview?.defectTrend ?? []).map((x: any) => ({
      date: new Date(x.dateUtc).toLocaleDateString(),
      defectRate: Number(x.defectRatePercent ?? 0),
      defects: Number(x.defectEventCount ?? 0),
      materials: Number(x.materialCount ?? 0),
    }));
  }, [overview]);

  const defectData = useMemo(() => {
    return (quality?.defectBreakdown ?? []).map((x: any) => ({
      name: x.defectCode ?? x.defectName ?? "Unknown",
      count: Number(x.count ?? 0),
      rate: Number(x.percentOfDefects ?? 0),
    }));
  }, [quality]);

  const materialColumns: SortableColumn<DashboardMaterialRow>[] = [
    {
      key: "materialCode",
      title: "Material",
      sortable: true,
      render: (row) => (
        <button
          className="link-button"
          onClick={() => mergeFilters({ materialCode: row.materialCode })}
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
      render: (row) => row.sourceSystem ?? "-",
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
          <span className={`risk-pill risk-${row.latestRiskClass.toLowerCase()}`}>
            {row.latestRiskClass}
          </span>
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

  return (
    <main className="page-shell">
      <DashboardFilterBar />
      <ActiveFilterChips />

      <section className="toolbar-row">
        <button
          className="secondary-button"
          onClick={refreshReadModels}
          disabled={isRefreshingReadModels}
        >
          <RefreshCw size={16} />
          {isRefreshingReadModels ? "Refreshing..." : "Refresh read models"}
        </button>
      </section>

      {isLoading ? <LoadingPanel /> : null}
      {error ? <ErrorPanel error={error} /> : null}

      {!isLoading && !error && workspace ? (
        <>
          <section className="metric-grid">
            <Metric
              icon={<Factory size={20} />}
              label="Materials"
              value={overview?.materials ?? 0}
              note="Filtered canonical material population"
            />
            <Metric
              icon={<Workflow size={20} />}
              label="Process Steps"
              value={overview?.processSteps ?? 0}
              note="Filtered process executions"
            />
            <Metric
              icon={<Database size={20} />}
              label="Parameter Observations"
              value={overview?.parameterObservations ?? 0}
              note="Aggregated dashboard data"
            />
            <Metric
              icon={<ShieldAlert size={20} />}
              label="Defect Rate"
              value={`${formatNumber(overview?.defectRatePercent ?? 0)}%`}
              note="Quality defect ratio"
              accent="danger"
            />
            <Metric
              icon={<Gauge size={20} />}
              label="High Risk Materials"
              value={overview?.highRiskMaterials ?? 0}
              note={`${formatNumber(overview?.highRiskRatePercent ?? 0)}% high risk`}
              accent="warning"
              onClick={() => mergeFilters({ riskClass: "High", page: 1 })}
            />
            <Metric
              icon={<AlertTriangle size={20} />}
              label="Data Quality Issues"
              value={overview?.dataQualityIssues ?? 0}
              note="Detected validation findings"
            />
          </section>

          <section className="dashboard-grid">
            <Panel
              title="Defect Trend"
              subtitle="Backend filtered by time/site/source/material context."
              icon={<Activity size={18} />}
              className="wide-panel"
            >
              <div className="chart-box">
                <ResponsiveContainer width="100%" height={280}>
                  <LineChart data={trendData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="date" />
                    <YAxis />
                    <Tooltip />
                    <Line
                      type="monotone"
                      dataKey="defectRate"
                      name="Defect rate %"
                      strokeWidth={3}
                      dot={{ r: 4 }}
                    />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            </Panel>

            <Panel
              title="Defect Breakdown"
              subtitle="Click a defect to cross-filter the workspace."
              icon={<BarChart3 size={18} />}
            >
              <div className="chart-box">
                <ResponsiveContainer width="100%" height={280}>
                  <BarChart data={defectData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="name" />
                    <YAxis />
                    <Tooltip />
                    <Bar
                      dataKey="count"
                      name="Defects"
                      onClick={(entry: any) =>
                        mergeFilters({ defectType: entry.name, page: 1 })
                      }
                    >
                      {defectData.map((_: any, index: number) => (
                        <Cell key={index} className="clickable-bar" />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </Panel>

            <Panel
              title="Risk Classes"
              subtitle="Click risk class to filter the material table."
              icon={<Gauge size={18} />}
            >
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Risk Class</th>
                      <th>Count</th>
                      <th>Percent</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(risk?.riskClassBreakdown ?? []).map((item: any) => (
                      <tr
                        key={item.riskClass}
                        onClick={() =>
                          mergeFilters({ riskClass: item.riskClass, page: 1 })
                        }
                      >
                        <td>{item.riskClass ?? "Unknown"}</td>
                        <td>{item.count}</td>
                        <td>{formatNumber(item.percent ?? 0)}%</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </Panel>

            <Panel
              title="Data Quality"
              subtitle="Readiness signals for selected material population."
              icon={<Database size={18} />}
              className="wide-panel"
            >
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Issue / Severity</th>
                      <th>Count</th>
                      <th>Percent</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(dataQuality?.issueTypeBreakdown ?? []).map((item: any) => (
                      <tr key={item.code}>
                        <td>{item.code}</td>
                        <td>{item.count}</td>
                        <td>{formatNumber(item.percent ?? 0)}%</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </Panel>

            <Panel
              title="Material Search and Drilldown"
              subtitle="Backend paginated and sortable. Click material code to filter the whole workspace."
              icon={<Factory size={18} />}
              className="wide-panel"
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
                  onClick={() => setFilter("page", Math.max((materials?.page ?? 1) - 1, 1))}
                >
                  Previous
                </button>

                <span>
                  Page {materials?.page ?? 1} / {materials?.totalPages ?? 0} —
                  {materials?.totalCount ?? 0} materials
                </span>

                <button
                  className="secondary-button"
                  disabled={(materials?.page ?? 1) >= (materials?.totalPages ?? 0)}
                  onClick={() => setFilter("page", (materials?.page ?? 1) + 1)}
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
            </Panel>
          </section>
        </>
      ) : null}
    </main>
  );
}

function Metric({
  icon,
  label,
  value,
  note,
  accent,
  onClick,
}: {
  icon: React.ReactNode;
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

function Panel({
  title,
  subtitle,
  icon,
  children,
  className,
}: {
  title: string;
  subtitle: string;
  icon: React.ReactNode;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <section className={`dashboard-panel ${className ?? ""}`}>
      <div className="panel-header">
        <div>
          <h3>
            {icon}
            {title}
          </h3>
          <p>{subtitle}</p>
        </div>
      </div>
      {children}
    </section>
  );
}

function formatNumber(value: number) {
  return new Intl.NumberFormat(undefined, {
    maximumFractionDigits: 2,
  }).format(value);
}