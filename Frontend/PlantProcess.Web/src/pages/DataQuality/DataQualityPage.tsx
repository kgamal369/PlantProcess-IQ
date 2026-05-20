import { useEffect, useMemo, useState } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  Database,
  Filter,
  RefreshCw,
  ShieldAlert,
} from "lucide-react";
import { apiClient } from "@/api/http";
import type { SortDirection } from "@/api/plantProcessApi";
import { MetricCard } from "@/components/MetricCard";
import { StatusBadge } from "@/components/StatusBadge";
import { SortableDataTable } from "@/components/SortableDataTable";
import type { SortableColumn } from "@/components/SortableDataTable";
import { ErrorPanel, LoadingPanel } from "@/components/AsyncState";
import { EmptyInsightState } from "@/components/dashboard/EmptyInsightState";

type DataQualitySeverity = "Critical" | "Error" | "Warning" | "Info" | string;

interface DataQualityIssueRow {
  id?: string;
  issueId?: string;
  issueType?: string;
  severity?: DataQualitySeverity;
  sourceSystem?: string;
  entityType?: string;
  entityId?: string;
  fieldName?: string;
  message?: string;
  detectedAtUtc?: string;
  createdAtUtc?: string;
  status?: string;
}

interface DataQualityDashboardResponse {
  generatedAtUtc?: string;
  totalIssues?: number;
  criticalCount?: number;
  errorCount?: number;
  warningCount?: number;
  infoCount?: number;
  bySourceSystem?: Array<{ sourceSystem: string; count: number }>;
  bySeverity?: Array<{ severity: string; count: number }>;
  issues?: DataQualityIssueRow[];
}

interface ScanHistoryRow {
  id?: string;
  scanId?: string;
  status?: string;
  startedAtUtc?: string;
  completedAtUtc?: string;
  issueCount?: number;
  message?: string;
}

const defaultFilters = {
  severity: "",
  sourceSystem: "",
  fromUtc: "",
  toUtc: "",
};

export function DataQualityPage() {
  const [dashboard, setDashboard] = useState<DataQualityDashboardResponse | null>(null);
  const [issues, setIssues] = useState<DataQualityIssueRow[]>([]);
  const [scanHistory, setScanHistory] = useState<ScanHistoryRow[]>([]);
  const [validation, setValidation] = useState<any>(null);

  const [filters, setFilters] = useState(defaultFilters);
  const [sortBy, setSortBy] = useState("detectedAtUtc");
  const [sortDirection, setSortDirection] = useState<SortDirection>("desc");

  const [isLoading, setIsLoading] = useState(true);
  const [isScanning, setIsScanning] = useState(false);
  const [error, setError] = useState<unknown>(null);

  async function load() {
    setIsLoading(true);
    setError(null);

    try {
      const [dashboardResult, issuesResult, scanPreviewResult] =
        await Promise.allSettled([
          apiClient.get<DataQualityDashboardResponse>("/analytics/dashboard/data-quality", {
            severity: filters.severity || undefined,
            sourceSystem: filters.sourceSystem || undefined,
            fromUtc: filters.fromUtc || undefined,
            toUtc: filters.toUtc || undefined,
          }),
          apiClient.get<DataQualityIssueRow[]>("/data-quality/issues", {
            severity: filters.severity || undefined,
            sourceSystem: filters.sourceSystem || undefined,
            fromUtc: filters.fromUtc || undefined,
            toUtc: filters.toUtc || undefined,
          }),
          apiClient.get<{
            persistIssues: boolean;
            generatedCandidates: number;
            candidates: Array<{
              issueType?: string;
              severity?: string;
              description?: string;
              affectedEntityName?: string;
              affectedEntityId?: string;
              materialUnitId?: string;
              sourceSystem?: string;
              sourceRecordId?: string;
            }>;
          }>("/data-quality/scan-preview", {
            take: 20,
          }),
        ]);

      if (dashboardResult.status === "fulfilled") {
        setDashboard(dashboardResult.value);
        setIssues(dashboardResult.value.issues ?? []);
      }

      if (issuesResult.status === "fulfilled") {
        setIssues(Array.isArray(issuesResult.value) ? issuesResult.value : []);
      }

      if (scanPreviewResult.status === "fulfilled") {
        const preview = scanPreviewResult.value;

        setScanHistory([
          {
            id: "preview",
            scanId: "preview",
            status: "Preview",
            startedAtUtc: new Date().toISOString(),
            completedAtUtc: new Date().toISOString(),
            issueCount: preview.generatedCandidates ?? preview.candidates?.length ?? 0,
            message: "Preview generated from current canonical data.",
          },
        ]);
      } else {
        setScanHistory([]);
      }

      const failed = [dashboardResult, issuesResult].find(
        (x) => x.status === "rejected"
      ) as PromiseRejectedResult | undefined;

      if (failed) {
        throw failed.reason;
      }
    } catch (loadError) {
      setError(loadError);
    } finally {
      setIsLoading(false);
    }
  }

  async function scanNow() {
  setIsScanning(true);
  setError(null);

  try {
    await apiClient.post("/data-quality/scan/run", {
      requestedBy: "PlantProcess IQ UI",
      requestedAtUtc: new Date().toISOString(),
    });

    await load();
  } catch (scanError) {
    setError(scanError);
  } finally {
    setIsScanning(false);
  }
}

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters.severity, filters.sourceSystem, filters.fromUtc, filters.toUtc]);

  const filteredIssues = useMemo(() => {
    const result = [...issues];

    result.sort((a, b) => {
      const left = String((a as any)[sortBy] ?? "");
      const right = String((b as any)[sortBy] ?? "");
      const compare = left.localeCompare(right);
      return sortDirection === "asc" ? compare : -compare;
    });

    return result;
  }, [issues, sortBy, sortDirection]);

  const sourceOptions = useMemo(() => {
    const values = new Set<string>();

    for (const issue of issues) {
      if (issue.sourceSystem) values.add(issue.sourceSystem);
    }

    for (const item of dashboard?.bySourceSystem ?? []) {
      if (item.sourceSystem) values.add(item.sourceSystem);
    }

    return Array.from(values).sort();
  }, [issues, dashboard]);

  const columns: SortableColumn<DataQualityIssueRow>[] = [
    {
      key: "severity",
      title: "Severity",
      sortable: true,
      render: (row) => <StatusBadge status={row.severity ?? "Unknown"} />,
    },
    {
      key: "issueType",
      title: "Issue Type",
      sortable: true,
      render: (row) => row.issueType ?? "-",
    },
    {
      key: "sourceSystem",
      title: "Source",
      sortable: true,
      render: (row) => row.sourceSystem ?? "-",
    },
    {
      key: "entityType",
      title: "Entity",
      sortable: true,
      render: (row) => (
        <span>
          {row.entityType ?? "-"}
          {row.fieldName ? ` / ${row.fieldName}` : ""}
        </span>
      ),
    },
    {
      key: "message",
      title: "Message",
      render: (row) => row.message ?? "-",
    },
    {
      key: "detectedAtUtc",
      title: "Detected",
      sortable: true,
      render: (row) => formatDate(row.detectedAtUtc ?? row.createdAtUtc),
    },
    {
      key: "status",
      title: "Status",
      sortable: true,
      render: (row) => <StatusBadge status={row.status ?? "Open"} />,
    },
  ];

  if (isLoading && !dashboard) {
    return <LoadingPanel text="Loading data-quality intelligence." />;
  }

  if (error && !dashboard) {
    return <ErrorPanel title="Could not load data quality page" error={error} />;
  }

  return (
    <section className="page">
      <div className="page-title">
        <div>
          <h1>Data Quality</h1>
          <p>
            Monitor source completeness, validation issues, scan history and data-readiness blockers.
          </p>
        </div>

        <div className="toolbar">
          <StatusBadge status={validation?.status ?? "Validation"} />
          <button
            className="primary-button"
            type="button"
            onClick={scanNow}
            disabled={isScanning}
          >
            <RefreshCw size={16} className={isScanning ? "spin" : undefined} />
            {isScanning ? "Scanning..." : "Scan Now"}
          </button>
        </div>
      </div>

      {error ? <ErrorPanel title="Latest data-quality action failed" error={error} /> : null}

      <div className="metric-grid">
        <MetricCard
          title="Total Issues"
          value={dashboard?.totalIssues ?? filteredIssues.length}
          subtitle="Open data-quality findings"
          icon={<Database size={20} />}
        />
        <MetricCard
          title="Critical"
          value={dashboard?.criticalCount ?? countBySeverity(filteredIssues, "Critical")}
          subtitle="Must fix before customer demo"
          icon={<ShieldAlert size={20} />}
        />
        <MetricCard
          title="Errors"
          value={dashboard?.errorCount ?? countBySeverity(filteredIssues, "Error")}
          subtitle="Canonical pipeline blockers"
          icon={<AlertTriangle size={20} />}
        />
        <MetricCard
          title="Warnings"
          value={dashboard?.warningCount ?? countBySeverity(filteredIssues, "Warning")}
          subtitle="Quality degradation signals"
          icon={<CheckCircle2 size={20} />}
        />
      </div>

      <section className="panel">
        <div className="panel-header">
          <div>
            <h3>
              <Filter size={18} />
              Issue Filters
            </h3>
            <p>Filter by severity, source system and detection window.</p>
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
            <span>Severity</span>
            <select
              value={filters.severity}
              onChange={(event) =>
                setFilters((current) => ({ ...current, severity: event.target.value }))
              }
            >
              <option value="">All severities</option>
              <option value="Critical">Critical</option>
              <option value="Error">Error</option>
              <option value="Warning">Warning</option>
              <option value="Info">Info</option>
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

      <section className="panel">
        <div className="panel-header">
          <div>
            <h3>Detected Issues</h3>
            <p>{filteredIssues.length} issue(s) loaded from backend.</p>
          </div>
        </div>

        {filteredIssues.length === 0 ? (
          <EmptyInsightState
            title="No data-quality issues"
            message="No issues found for the selected filters."
          />
        ) : (
          <SortableDataTable
            rows={filteredIssues}
            columns={columns}
            sortBy={sortBy}
            sortDirection={sortDirection}
            onSort={(nextSortBy, nextDirection) => {
              setSortBy(nextSortBy);
              setSortDirection(nextDirection);
            }}
            emptyText="No data-quality issues found."
          />
        )}
      </section>

      <section className="panel">
        <div className="panel-header">
          <div>
            <h3>Scan History</h3>
            <p>Latest data-quality scans triggered from backend jobs or UI.</p>
          </div>
        </div>

        {scanHistory.length === 0 ? (
          <EmptyInsightState
            title="No scan history"
            message="Trigger a scan to populate scan execution history."
          />
        ) : (
          <div className="detail-list">
            {scanHistory.map((scan, index) => (
              <div className="detail-row" key={scan.id ?? scan.scanId ?? index}>
                <span>
                  {formatDate(scan.startedAtUtc)} → {formatDate(scan.completedAtUtc)}
                </span>
                <strong>
                  {scan.status ?? "Unknown"} · {scan.issueCount ?? 0} issue(s)
                </strong>
              </div>
            ))}
          </div>
        )}
      </section>
    </section>
  );
}

function countBySeverity(rows: DataQualityIssueRow[], severity: string) {
  return rows.filter((x) =>
    String(x.severity ?? "").toLowerCase().includes(severity.toLowerCase())
  ).length;
}

function formatDate(value?: string) {
  if (!value) return "-";

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;

  return date.toLocaleString();
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