import { useEffect, useMemo, useState } from "react";
import {
  AlertTriangle,
  Gauge,
  RefreshCw,
  ShieldAlert,
  TrendingUp,
  Workflow,
} from "lucide-react";
import { apiClient } from "@/api/http";
import type { SortDirection } from "@/api/plantProcessApi";
import { MetricCard } from "@/components/MetricCard";
import { StatusBadge } from "@/components/StatusBadge";
import { SortableDataTable } from "@/components/SortableDataTable";
import type { SortableColumn } from "@/components/SortableDataTable";
import { ErrorPanel } from "@/components/AsyncState";
import { SkeletonKpi, SkeletonTable, SkeletonChart } from "@/components/skeletons/Skeleton";
import {
  InteractiveBarChart,
  InteractiveLineChart,
  InteractivePieChart,
} from "@/components/charts/InteractiveCharts";
import type { ChartRow } from "@/components/charts/InteractiveCharts";
import { EmptyInsightState } from "@/components/dashboard/EmptyInsightState";

interface RiskClassBreakdown {
  riskClass?: unknown;
  className?: unknown;
  count?: unknown;
  materialCount?: unknown;
  averageScore?: unknown;
}

interface RiskTrendPoint {
  bucketUtc?: unknown;
  timestampUtc?: unknown;
  date?: unknown;
  averageRiskScore?: unknown;
  avgRiskScore?: unknown;
  highRiskCount?: unknown;
  materialCount?: unknown;
}

interface HighRiskMaterial {
  materialUnitId?: unknown;
  materialCode?: unknown;
  materialUnitType?: unknown;
  productFamily?: unknown;
  latestRiskScore?: unknown;
  riskScore?: unknown;
  latestRiskClass?: unknown;
  riskClass?: unknown;
  topContributor?: unknown;
  topRiskContributor?: unknown;
  sourceSystem?: unknown;
  latestScoredAtUtc?: unknown;
  scoredAtUtc?: unknown;
}

interface RiskDashboardResponse {
  generatedAtUtc?: unknown;
  totalMaterials?: unknown;
  highRiskMaterials?: unknown;
  averageRiskScore?: unknown;
  riskClassBreakdown?: RiskClassBreakdown[];
  distribution?: RiskClassBreakdown[];
  trend?: RiskTrendPoint[];
  highRisk?: HighRiskMaterial[];
  topContributors?: Array<{ contributor?: unknown; count?: unknown; averageRiskScore?: unknown }>;
}

const defaultFilters = {
  riskClass: "",
  sourceSystem: "",
  fromUtc: "",
  toUtc: "",
};

export function RiskDashboardPage() {
  const [dashboard, setDashboard] = useState<RiskDashboardResponse | null>(null);
  const [highRisk, setHighRisk] = useState<HighRiskMaterial[]>([]);
  const [filters, setFilters] = useState(defaultFilters);

  const [sortBy, setSortBy] = useState("latestRiskScore");
  const [sortDirection, setSortDirection] = useState<SortDirection>("desc");

  const [isLoading, setIsLoading] = useState(true);
  const [isCalculating, setIsCalculating] = useState(false);
  const [error, setError] = useState<unknown>(null);

  async function load() {
    setIsLoading(true);
    setError(null);

    try {
      const result = await apiClient.get<RiskDashboardResponse>("/analytics/dashboard/risk", {
        riskClass: filters.riskClass || undefined,
        sourceSystem: filters.sourceSystem || undefined,
        fromUtc: filters.fromUtc || undefined,
        toUtc: filters.toUtc || undefined,
      });

      setDashboard(result);
      setHighRisk(result.highRisk ?? []);
    } catch (loadError) {
      setError(loadError);
    } finally {
      setIsLoading(false);
    }
  }

  async function recalculateRisk() {
    setIsCalculating(true);
    setError(null);

    try {
      await apiClient.post("/risk-scores/calculate", {
        requestedBy: "PlantProcess IQ UI",
        requestedAtUtc: new Date().toISOString(),
        scope: "FilteredDashboard",
        filters,
      });

      await load();
    } catch (firstError) {
      try {
        await apiClient.post("/analytics/risk-scores/calculate", {
          requestedBy: "PlantProcess IQ UI",
          requestedAtUtc: new Date().toISOString(),
          scope: "FilteredDashboard",
          filters,
        });

        await load();
      } catch {
        setError(firstError);
      }
    } finally {
      setIsCalculating(false);
    }
  }

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters.riskClass, filters.sourceSystem, filters.fromUtc, filters.toUtc]);

  const distributionRows: ChartRow[] = useMemo(() => {
    const rows = dashboard?.riskClassBreakdown ?? dashboard?.distribution ?? [];

    return rows.map((x) => ({
      riskClass: toDisplayText(x.riskClass ?? x.className, "Unknown"),
      count: toFiniteNumber(x.count ?? x.materialCount),
      averageScore: toFiniteNumber(x.averageScore),
    }));
  }, [dashboard]);

  const trendRows: ChartRow[] = useMemo(() => {
    return (dashboard?.trend ?? []).map((x) => ({
      bucket: formatShortDate(x.bucketUtc ?? x.timestampUtc ?? x.date),
      averageRiskScore: toFiniteNumber(x.averageRiskScore ?? x.avgRiskScore),
      highRiskCount: toFiniteNumber(x.highRiskCount),
      materialCount: toFiniteNumber(x.materialCount),
    }));
  }, [dashboard]);

  const contributorRows: ChartRow[] = useMemo(() => {
    return (dashboard?.topContributors ?? []).map((x) => ({
      contributor: toDisplayText(x.contributor, "Unknown"),
      count: toFiniteNumber(x.count),
      averageRiskScore: toFiniteNumber(x.averageRiskScore),
    }));
  }, [dashboard]);

  const sortedHighRisk = useMemo(() => {
    const rows = [...highRisk];

    rows.sort((a, b) => {
      const left = (a as any)[sortBy];
      const right = (b as any)[sortBy];

      if (typeof left === "number" && typeof right === "number") {
        return sortDirection === "asc" ? left - right : right - left;
      }

      const compare = toDisplayText(left, "").localeCompare(toDisplayText(right, ""));
      return sortDirection === "asc" ? compare : -compare;
    });

    return rows;
  }, [highRisk, sortBy, sortDirection]);

  const sourceOptions = useMemo(() => {
    return Array.from(
      new Set(
        highRisk
          .map((x) => toDisplayText(x.sourceSystem, ""))
          .filter((source) => source.length > 0)
      )
    ).sort();
  }, [highRisk]);

  const columns: SortableColumn<HighRiskMaterial>[] = [
    {
      key: "materialCode",
      title: "Material",
      sortable: true,
      render: (row) => (
        <div>
          <strong>{toDisplayText(row.materialCode ?? row.materialUnitId)}</strong>
          <p>{toDisplayText(row.materialUnitType ?? row.productFamily, "")}</p>
        </div>
      ),
    },
    {
      key: "latestRiskScore",
      title: "Risk Score",
      sortable: true,
      align: "right",
      render: (row) => formatPercent(row.latestRiskScore ?? row.riskScore),
    },
    {
      key: "latestRiskClass",
      title: "Risk Class",
      sortable: true,
      render: (row) => (
        <StatusBadge status={toDisplayText(row.latestRiskClass ?? row.riskClass, "Unknown")} />
      ),
    },
    {
      key: "topContributor",
      title: "Top Contributor",
      sortable: true,
      render: (row) => toDisplayText(row.topContributor ?? row.topRiskContributor),
    },
    {
      key: "sourceSystem",
      title: "Source",
      sortable: true,
      render: (row) => toDisplayText(row.sourceSystem),
    },
    {
      key: "latestScoredAtUtc",
      title: "Last Scored",
      sortable: true,
      render: (row) => formatDate(row.latestScoredAtUtc ?? row.scoredAtUtc),
    },
  ];

  if (error && !dashboard) {
    return <ErrorPanel title="Could not load risk dashboard" error={error} />;
  }

  if (isLoading && !dashboard) {
    return (
      <section className="page">
        <div className="page-title">
          <div>
            <h1>Risk Dashboard</h1>
            <p>Monitor material risk classes, high-risk units, contributors and risk trend movement.</p>
          </div>
        </div>
        <div className="metric-grid">
          <SkeletonKpi />
          <SkeletonKpi />
          <SkeletonKpi />
          <SkeletonKpi />
        </div>
        <SkeletonChart height={320} />
        <SkeletonTable rows={12} columns={6} />
      </section>
    );
  }

  return (
    <section className="page">
      <div className="page-title">
        <div>
          <h1>Risk Dashboard</h1>
          <p>
            Monitor material risk classes, high-risk units, contributors and risk trend movement.
          </p>
        </div>

        <div className="toolbar">
          <button
            className="primary-button"
            type="button"
            disabled={isCalculating}
            onClick={recalculateRisk}
          >
            <RefreshCw size={16} className={isCalculating ? "spin" : undefined} />
            {isCalculating ? "Recalculating..." : "Recalculate Risk"}
          </button>
        </div>
      </div>

      {error ? <ErrorPanel title="Latest risk action failed" error={error} /> : null}

      <div className="metric-grid">
        <MetricCard
          title="Total Materials"
          value={toDisplayText(dashboard?.totalMaterials ?? highRisk.length)}
          subtitle="Materials in current risk scope"
          icon={<Workflow size={20} />}
        />
        <MetricCard
          title="High Risk"
          value={toDisplayText(dashboard?.highRiskMaterials ?? highRisk.length)}
          subtitle="Requires investigation"
          icon={<ShieldAlert size={20} />}
        />
        <MetricCard
          title="Average Risk"
          value={formatPercent(dashboard?.averageRiskScore)}
          subtitle="Current filtered population"
          icon={<Gauge size={20} />}
        />
        <MetricCard
          title="Risk Classes"
          value={distributionRows.length}
          subtitle="Class distribution buckets"
          icon={<TrendingUp size={20} />}
        />
      </div>

      <section className="panel">
        <div className="panel-header">
          <div>
            <h3>Risk Filters</h3>
            <p>Filter by risk class, source system and scoring window.</p>
          </div>

          <button
            className="secondary-button"
            type="button"
            onClick={() => setFilters(defaultFilters)}
          >
            Clear Filters
          </button>
        </div>

        <div className="filter-grid">
          <label className="field">
            <span>Risk Class</span>
            <select
              value={filters.riskClass}
              onChange={(event) =>
                setFilters((current) => ({ ...current, riskClass: event.target.value }))
              }
            >
              <option value="">All classes</option>
              <option value="High">High</option>
              <option value="Medium">Medium</option>
              <option value="Low">Low</option>
              <option value="Unknown">Unknown</option>
            </select>
          </label>

          <label className="field">
            <span>Source System</span>
            <select
              value={filters.sourceSystem}
              onChange={(event) =>
                setFilters((current) => ({ ...current, sourceSystem: event.target.value }))
              }
            >
              <option value="">All sources</option>
              {sourceOptions.map((source) => (
                <option key={source} value={source}>
                  {source}
                </option>
              ))}
            </select>
          </label>

          <label className="field">
            <span>From UTC</span>
            <input
              type="datetime-local"
              value={toLocalInput(filters.fromUtc)}
              onChange={(event) =>
                setFilters((current) => ({
                  ...current,
                  fromUtc: toUtcValue(event.target.value) ?? "",
                }))
              }
            />
          </label>

          <label className="field">
            <span>To UTC</span>
            <input
              type="datetime-local"
              value={toLocalInput(filters.toUtc)}
              onChange={(event) =>
                setFilters((current) => ({
                  ...current,
                  toUtc: toUtcValue(event.target.value) ?? "",
                }))
              }
            />
          </label>
        </div>
      </section>

      <div className="dashboard-grid-two">
        <section className="panel">
          <div className="panel-header">
            <div>
              <h3>
                <AlertTriangle size={18} />
                Risk Class Distribution
              </h3>
              <p>Material count by latest risk class.</p>
            </div>
          </div>

          {distributionRows.length === 0 ? (
            <EmptyInsightState />
          ) : (
            <InteractivePieChart
              data={distributionRows}
              categoryKey="riskClass"
              valueKey="count"
              height={320}
              selection={{
                type: "riskClass",
                field: "riskClass",
                sourceWidget: "risk-dashboard-distribution",
                valueKey: "riskClass",
                labelKey: "riskClass",
              }}
            />
          )}
        </section>

        <section className="panel">
          <div className="panel-header">
            <div>
              <h3>Risk Trend</h3>
              <p>Average risk-score movement over time.</p>
            </div>
          </div>

          {trendRows.length === 0 ? (
            <EmptyInsightState />
          ) : (
            <InteractiveLineChart
              data={trendRows}
              categoryKey="bucket"
              valueKey="averageRiskScore"
              height={320}
              selection={{
                type: "dateRange",
                field: "fromUtc",
                sourceWidget: "risk-dashboard-trend",
                valueKey: "bucket",
                labelKey: "bucket",
              }}
            />
          )}
        </section>
      </div>

      <section className="panel">
        <div className="panel-header">
          <div>
            <h3>Top Risk Contributors</h3>
            <p>Most frequent contributors across high-risk population.</p>
          </div>
        </div>

        {contributorRows.length === 0 ? (
          <EmptyInsightState />
        ) : (
          <InteractiveBarChart
            data={contributorRows}
            categoryKey="contributor"
            valueKey="count"
            height={300}
            selection={{
              type: "sourceSystem",
              field: "sourceSystem",
              sourceWidget: "risk-dashboard-contributors",
              valueKey: "contributor",
              labelKey: "contributor",
            }}
          />
        )}
      </section>

      <section className="panel">
        <div className="panel-header">
          <div>
            <h3>High-Risk Materials</h3>
            <p>{sortedHighRisk.length} material(s) require investigation.</p>
          </div>
        </div>

        {sortedHighRisk.length === 0 ? (
          <EmptyInsightState
            title="No high-risk materials"
            message="No high-risk materials found for the selected filters."
          />
        ) : (
          <SortableDataTable
            rows={sortedHighRisk}
            columns={columns}
            sortBy={sortBy}
            sortDirection={sortDirection}
            onSort={(nextSortBy, nextDirection) => {
              setSortBy(nextSortBy);
              setSortDirection(nextDirection);
            }}
            emptyText="No high-risk materials found."
          />
        )}
      </section>
    </section>
  );
}

function toFiniteNumber(value: unknown, fallback = 0): number {
  if (typeof value === "number") {
    return Number.isFinite(value) ? value : fallback;
  }

  if (typeof value === "string") {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
  }

  if (typeof value === "boolean") {
    return value ? 1 : 0;
  }

  if (value instanceof Date) {
    return Number.isFinite(value.getTime()) ? value.getTime() : fallback;
  }

  return fallback;
}

function toDisplayText(value: unknown, fallback = "-"): string {
  if (value === null || value === undefined || value === "") {
    return fallback;
  }

  if (
    typeof value === "string" ||
    typeof value === "number" ||
    typeof value === "boolean"
  ) {
    return String(value);
  }

  if (value instanceof Date) {
    return Number.isNaN(value.getTime()) ? fallback : value.toLocaleString();
  }

  if (Array.isArray(value)) {
    if (value.length === 0) return fallback;

    return (
      value
        .map((item) => toDisplayText(item, ""))
        .filter(Boolean)
        .join(", ") || fallback
    );
  }

  if (typeof value === "object") {
    const record = value as Record<string, unknown>;

    const preferred =
      record.materialCode ??
      record.materialUnitCode ??
      record.code ??
      record.name ??
      record.label ??
      record.displayName ??
      record.id ??
      record.materialUnitId ??
      record.riskClass ??
      record.riskType ??
      record.sourceSystem ??
      record.contributor ??
      record.score;

    if (preferred !== undefined && preferred !== value) {
      return toDisplayText(preferred, fallback);
    }

    return fallback;
  }

  return fallback;
}

function formatPercent(value?: unknown) {
  if (value === null || value === undefined || value === "") return "-";

  const numeric = toFiniteNumber(value, Number.NaN);
  if (Number.isNaN(numeric)) return "-";

  if (numeric <= 1) return `${(numeric * 100).toFixed(1)}%`;

  return `${numeric.toFixed(1)}%`;
}

function formatDate(value?: unknown) {
  if (!value) return "-";

  if (value instanceof Date) {
    return Number.isNaN(value.getTime()) ? "-" : value.toLocaleString();
  }

  if (typeof value !== "string" && typeof value !== "number") {
    return toDisplayText(value);
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return toDisplayText(value);

  return date.toLocaleString();
}

function formatShortDate(value?: unknown) {
  if (!value) return "-";

  if (value instanceof Date) {
    return Number.isNaN(value.getTime()) ? "-" : value.toLocaleDateString();
  }

  if (typeof value !== "string" && typeof value !== "number") {
    return toDisplayText(value);
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return toDisplayText(value);

  return date.toLocaleDateString();
}

function toLocalInput(value?: string): string {
  if (!value) return "";

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";

  const offsetMs = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 16);
}

function toUtcValue(value: string): string | undefined {
  if (!value) return undefined;
  return new Date(value).toISOString();
}