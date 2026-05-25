import { memo, useCallback, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import {
  Activity,
  AlertOctagon,
  AlertTriangle,
  BarChart3,
  Bug,
  Copy,
  Database,
  Factory,
  FlaskConical,
  GitBranch,
  Gauge,
  Grid3X3,
  PlusCircle,
  RefreshCw,
  ScatterChart as ScatterIcon,
  ShieldOff,
  Trash2,
} from "lucide-react";

import { plantProcessApi } from "@/api/plantProcessApi";

import type {
  DashboardDefinitionRecord,
  DashboardFilters,
  DashboardMaterialRow,
  DashboardWidgetDefinitionRecord,
  DashboardWidgetQueryResult,
  DashboardWorkspace,
  SortDirection,
} from "@/api/plantProcessApi";

import { dashboardingApi } from "@/api/dashboarding";

import { ActiveFilterChips } from "@/components/ActiveFilterChips";
import { DashboardFilterBar } from "@/components/DashboardFilterBar";
import { ErrorPanel } from "@/components/AsyncState";
import {
  SkeletonChart,
  SkeletonKpi,
  SkeletonWidgetGrid,
} from "@/components/skeletons/Skeleton";
import { SortableDataTable } from "@/components/SortableDataTable";
import type { SortableColumn } from "@/components/SortableDataTable";

import { DashboardWidgetCard } from "@/components/dashboard/DashboardWidgetCard";
import { DashboardGridLayout } from "@/components/dashboard/DashboardGridLayout";
import { DrilldownDrawer } from "@/components/dashboard/DrilldownDrawer";
import { SelectionBreadcrumb } from "@/components/dashboard/SelectionBreadcrumb";
import { WidgetBuilderWizard } from "@/components/dashboard/widget-builder/WidgetBuilderWizard";

import {
  InteractiveBarChart,
  InteractiveHeatmap,
  InteractiveLineChart,
  InteractivePieChart,
  InteractiveScatterChart,
} from "@/components/charts/InteractiveCharts";
import type { ChartRow } from "@/components/charts/InteractiveCharts";

import { useDashboardFilters } from "@/state/DashboardFilterContext";
import { useDashboardGridLayout } from "@/state/DashboardGridLayoutContext";
import { useDashboardSelections } from "@/state/DashboardSelectionContext";

export function DashboardPageContent() {
  const { filters, setFilter } = useDashboardFilters();

  const { applySelection, openDrilldown, getWidgetState } =
    useDashboardSelections();

  const { serializeLayouts, replaceLayoutsFromJson, addWidget, removeWidget } =
    useDashboardGridLayout();

  const [workspace, setWorkspace] = useState<DashboardWorkspace | null>(null);
  const [dashboards, setDashboards] = useState<DashboardDefinitionRecord[]>([]);
  const [activeDashboard, setActiveDashboard] =
    useState<DashboardDefinitionRecord | null>(null);

  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingDashboards, setIsLoadingDashboards] = useState(true);
  const [isRefreshingReadModels, setIsRefreshingReadModels] = useState(false);
  const [isSavingLayout, setIsSavingLayout] = useState(false);
  const [isReloadingLayout, setIsReloadingLayout] = useState(false);

  const [error, setError] = useState<unknown>(null);

  const [isWidgetBuilderOpen, setIsWidgetBuilderOpen] = useState(false);
  const [editingWidget, setEditingWidget] =
    useState<DashboardWidgetDefinitionRecord | null>(null);

  const [lastSavedLayoutAtUtc, setLastSavedLayoutAtUtc] =
    useState<string | null>(null);
  const [layoutMessage, setLayoutMessage] = useState<string | null>(null);

  const load = useCallback(async () => {
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
  }, [filters]);

  const loadDashboardDefinitions = useCallback(
    async (preferredDashboardId?: string | null) => {
      setIsLoadingDashboards(true);

      try {
        try {
          await plantProcessApi.ensureSystemDashboardTemplates();
        } catch (ensureError: unknown) {
          const isDuplicateKey =
            ensureError instanceof Error &&
            (ensureError.message.includes("409") ||
              ensureError.message.includes("23505") ||
              ensureError.message.toLowerCase().includes("duplicate") ||
              ensureError.message.toLowerCase().includes("unique constraint"));

          if (!isDuplicateKey) throw ensureError;
        }

        const result = await plantProcessApi.getDashboardDefinitions(false, true);
        setDashboards(result);

        const selected =
          (preferredDashboardId
            ? result.find((x) => x.id === preferredDashboardId)
            : null) ??
          result.find((x) => x.isDefault && x.isActive) ??
          result.find((x) => x.isActive) ??
          null;

        setActiveDashboard(selected);

        if (selected?.layoutJson) {
          replaceLayoutsFromJson(selected.layoutJson);
        }

        for (const widget of selected?.widgets ?? []) {
          if (widget.isActive) {
            addWidget(`saved-${widget.id}`, {
              w: 6,
              h: 8,
              minW: 4,
              minH: 5,
            });
          }
        }
      } catch (loadError) {
        setError(loadError);
      } finally {
        setIsLoadingDashboards(false);
      }
    },
    [addWidget, replaceLayoutsFromJson]
  );

  const selectDashboard = useCallback(
    async (dashboardId: string) => {
      const selected = dashboards.find((x) => x.id === dashboardId) ?? null;

      setActiveDashboard(selected);

      if (selected?.layoutJson) {
        replaceLayoutsFromJson(selected.layoutJson);
      }

      for (const widget of selected?.widgets ?? []) {
        if (widget.isActive) {
          addWidget(`saved-${widget.id}`, {
            w: 6,
            h: 8,
            minW: 4,
            minH: 5,
          });
        }
      }
    },
    [addWidget, dashboards, replaceLayoutsFromJson]
  );

  const openCreateWidgetWizard = useCallback(() => {
    setEditingWidget(null);
    setIsWidgetBuilderOpen(true);
  }, []);

  const openEditWidgetWizard = useCallback(
    (widget: DashboardWidgetDefinitionRecord) => {
      setEditingWidget(widget);
      setIsWidgetBuilderOpen(true);
    },
    []
  );

  const handleWidgetSaved = useCallback(
    async (widgetId: string) => {
      if (!activeDashboard) return;

      addWidget(`saved-${widgetId}`, {
        w: 6,
        h: 8,
        minW: 4,
        minH: 5,
      });

      await loadDashboardDefinitions(activeDashboard.id);
    },
    [activeDashboard, addWidget, loadDashboardDefinitions]
  );

  const removeSavedWidget = useCallback(
    async (widget: DashboardWidgetDefinitionRecord) => {
      if (!activeDashboard) return;

      removeWidget(`saved-${widget.id}`);

      await plantProcessApi.deactivateDashboardWidgetDefinition(
        activeDashboard.id,
        widget.id
      );

      await loadDashboardDefinitions(activeDashboard.id);
    },
    [activeDashboard, loadDashboardDefinitions, removeWidget]
  );

  const cloneSavedWidget = useCallback(
    async (widget: DashboardWidgetDefinitionRecord) => {
      if (!activeDashboard) return;

      const cloned = await plantProcessApi.cloneDashboardWidgetDefinition(
        activeDashboard.id,
        widget.id,
        {
          widgetTitle: `${widget.widgetTitle} Copy`,
          sortOrder: widget.sortOrder + 1,
        }
      );

      addWidget(`saved-${cloned.id}`, {
        w: 6,
        h: 8,
        minW: 4,
        minH: 5,
      });

      await loadDashboardDefinitions(activeDashboard.id);
    },
    [activeDashboard, addWidget, loadDashboardDefinitions]
  );

  const hideSavedWidget = useCallback(
    async (widget: DashboardWidgetDefinitionRecord) => {
      removeWidget(`saved-${widget.id}`);
    },
    [removeWidget]
  );

  const saveDashboardLayout = useCallback(async () => {
    if (!activeDashboard) {
      setError(new Error("No active dashboard is selected."));
      return;
    }

    setIsSavingLayout(true);
    setError(null);
    setLayoutMessage(null);

    try {
      await dashboardingApi.updateDashboardLayout(
        activeDashboard.id,
        serializeLayouts()
      );

      setLastSavedLayoutAtUtc(new Date().toISOString());
      setLayoutMessage("Dashboard layout saved to backend.");

      await loadDashboardDefinitions(activeDashboard.id);
    } catch (saveError) {
      setError(saveError);
    } finally {
      setIsSavingLayout(false);
    }
  }, [activeDashboard, loadDashboardDefinitions, serializeLayouts]);

  const reloadDashboardLayout = useCallback(async () => {
    if (!activeDashboard) {
      setError(new Error("No active dashboard is selected."));
      return;
    }

    setIsReloadingLayout(true);
    setError(null);
    setLayoutMessage(null);

    try {
      const latest = (await dashboardingApi.getDashboardDefinition(
        activeDashboard.id
      )) as DashboardDefinitionRecord;

      if (latest.layoutJson) {
        replaceLayoutsFromJson(latest.layoutJson);
        setLayoutMessage("Dashboard layout reloaded from backend.");
      } else {
        setLayoutMessage("No backend layout stored for this dashboard yet.");
      }

      await loadDashboardDefinitions(activeDashboard.id);
    } catch (reloadError) {
      setError(reloadError);
    } finally {
      setIsReloadingLayout(false);
    }
  }, [activeDashboard, loadDashboardDefinitions, replaceLayoutsFromJson]);

  const refreshReadModels = useCallback(async () => {
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
  }, [load]);

  useEffect(() => {
    void loadDashboardDefinitions();
  }, [loadDashboardDefinitions]);

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

        if (!ignore) {
          setWorkspace(result);
        }
      } catch (loadError) {
        if (!ignore) {
          setError(loadError);
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    void loadSafe();

    return () => {
      ignore = true;
    };
  }, [filters]);

  const overview = workspace?.overview;
  const quality = workspace?.quality;
  const risk = workspace?.risk;
  const dataQuality = workspace?.dataQuality;
  const materials = workspace?.materials;

  const trendData = useMemo<ChartRow[]>(
    () =>
      (overview?.defectTrend ?? []).map((x: any) => ({
        date: new Date(x.dateUtc).toLocaleDateString(),
        isoDate: x.dateUtc,
        defectRate: Number(x.defectRatePercent ?? 0),
        defects: Number(x.defectEventCount ?? 0),
        materials: Number(x.materialCount ?? 0),
      })),
    [overview]
  );

  const defectData = useMemo<ChartRow[]>(
    () =>
      (quality?.defectBreakdown ?? []).map((x: any) => ({
        name: x.defectCode ?? x.defectName ?? "Unknown",
        label: x.defectName ?? x.defectCode ?? "Unknown",
        count: Number(x.count ?? 0),
        percent: Number(x.percentOfDefects ?? 0),
      })),
    [quality]
  );

  const riskClassData = useMemo<ChartRow[]>(
    () =>
      (risk?.riskClassBreakdown ?? []).map((x: any) => ({
        riskClass: x.riskClass ?? "Unknown",
        count: Number(x.count ?? 0),
        percent: Number(x.percent ?? 0),
      })),
    [risk]
  );

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

  const riskScatterData = useMemo<ChartRow[]>(
    () =>
      (materials?.items ?? [])
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
        })),
    [materials]
  );

  const heatmapData = useMemo<ChartRow[]>(() => {
    const grouped = new Map<string, number>();

    for (const row of materials?.items ?? []) {
      const key = `${row.latestRiskClass || "Unknown"}||${
        row.materialUnitType || "Unknown"
      }`;

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

  const dataQualityRows = useMemo<Record<string, unknown>[]>(
    () =>
      (dataQuality?.issueTypeBreakdown ?? []).map((x: any) => ({
        issueType: x.code,
        count: x.count,
        percent: x.percent,
      })),
    [dataQuality]
  );

  const topContributors = useMemo<ChartRow[]>(
    () =>
      (overview?.topRiskContributors ?? []).map((x: any) => ({
        contributorCode: x.contributorCode ?? x.contributorType ?? "Unknown",
        count: Number(x.count ?? 0),
        averageRiskScore: Number(x.averageRiskScore ?? 0),
      })),
    [overview]
  );

  const updateSort = useCallback(
    (sortBy: string, sortDirection: SortDirection) => {
      setFilter("sortBy", sortBy);
      setFilter("sortDirection", sortDirection);
      setFilter("page", 1);
    },
    [setFilter]
  );

  const materialColumns = useMemo<SortableColumn<DashboardMaterialRow>[]>(
    () => [
      {
        key: "materialCode",
        title: "Material",
        sortable: true,
        render: (row) => (
          <button
            className="link-button"
            type="button"
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
            type="button"
            onClick={() =>
              applySelection({
                type: "sourceSystem",
                field: "sourceSystem",
                value: row.sourceSystem ?? "Unknown",
                label: row.sourceSystem ?? "Unknown",
                sourceWidget: "Material Explorer",
              })
            }
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
              type="button"
              onClick={() =>
                applySelection({
                  type: "riskClass",
                  field: "riskClass",
                  value: row.latestRiskClass!,
                  label: row.latestRiskClass!,
                  sourceWidget: "Material Explorer",
                })
              }
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
    ],
    [applySelection, openDrilldown]
  );

  const defectChartType = getWidgetState("defectBreakdown").chartType ?? "bar";
  const trendChartType = getWidgetState("defectTrend").chartType ?? "line";
  const riskChartType = getWidgetState("riskDistribution").chartType ?? "donut";
  const sourceChartType = getWidgetState("sourceContribution").chartType ?? "bar";

  const savedWidgets = useMemo(
    () => (activeDashboard?.widgets ?? []).filter((widget) => widget.isActive),
    [activeDashboard]
  );

  return (
    <main className="page-shell advanced-dashboard-shell">
      <section className="piq-dash-hero">
        <div className="piq-dash-hero__left">
          <div className="piq-dash-hero__dashboard-row">
            <select
              className="piq-dash-select"
              value={activeDashboard?.id ?? ""}
              onChange={(event) => void selectDashboard(event.target.value)}
              disabled={isLoadingDashboards || dashboards.length === 0}
              aria-label="Select active dashboard"
            >
              {dashboards.length === 0 ? (
                <option value="">No dashboards yet</option>
              ) : null}

              {dashboards.map((dashboard) => (
                <option key={dashboard.id} value={dashboard.id}>
                  {dashboard.name}
                  {dashboard.isDefault ? " — Default" : ""}
                </option>
              ))}
            </select>

            {activeDashboard?.isSystemTemplate ? (
              <span className="piq-dash-badge piq-dash-badge--system">
                System template
              </span>
            ) : null}

            {isLoadingDashboards ? (
              <span className="piq-dash-badge" style={{ opacity: 0.5 }}>
                Loading…
              </span>
            ) : null}
          </div>
        </div>

        <div className="piq-dash-hero__actions">
          <button
            className="piq-dash-btn piq-dash-btn--primary"
            onClick={openCreateWidgetWizard}
            disabled={!activeDashboard}
            type="button"
            title="Add a new custom widget"
          >
            <PlusCircle size={14} />
            Add widget
          </button>

          <button
            className="piq-dash-btn"
            onClick={() => void saveDashboardLayout()}
            disabled={!activeDashboard || isSavingLayout || isReloadingLayout}
            type="button"
            title="Save widget positions to backend"
          >
            {isSavingLayout ? "Saving…" : "Save layout"}
          </button>

          <button
            className="piq-dash-btn"
            onClick={() => void reloadDashboardLayout()}
            disabled={!activeDashboard || isSavingLayout || isReloadingLayout}
            type="button"
            title="Reload positions from last saved state"
          >
            {isReloadingLayout ? "Reloading…" : "Reload layout"}
          </button>

          <button
            className="piq-dash-btn"
            onClick={() => void refreshReadModels()}
            disabled={isRefreshingReadModels}
            type="button"
            title="Rebuild all read-models from source data"
          >
            <RefreshCw size={14} />
            {isRefreshingReadModels ? "Refreshing…" : "Refresh data"}
          </button>
        </div>
      </section>

      {layoutMessage ? (
        <div className="success-banner">
          {layoutMessage}
          {lastSavedLayoutAtUtc ? (
            <span className="muted-text">
              {" "}
              Last saved:{" "}
              {new Date(lastSavedLayoutAtUtc).toLocaleTimeString()}
            </span>
          ) : null}
        </div>
      ) : null}

      <DashboardFilterBar />
      <ActiveFilterChips />
      <SelectionBreadcrumb />

      {isLoading && !workspace ? (
        <>
          <div className="metric-grid">
            <SkeletonKpi />
            <SkeletonKpi />
            <SkeletonKpi />
            <SkeletonKpi />
          </div>
          <SkeletonWidgetGrid widgetCount={6} />
        </>
      ) : null}

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
              icon={<GitBranch size={20} />}
              label="Process Steps"
              value={overview?.processSteps ?? 0}
              note="Filtered process executions"
            />

            <InteractiveMetric
              icon={<FlaskConical size={20} />}
              label="Parameter Observations"
              value={overview?.parameterObservations ?? 0}
              note="Aggregated sensor & measurement data"
            />

            <InteractiveMetric
              icon={<Bug size={20} />}
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
              icon={<AlertOctagon size={20} />}
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
              icon={<ShieldOff size={20} />}
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

            {savedWidgets.map((widget) => (
              <div key={`saved-${widget.id}`}>
                <SavedDashboardWidget
                  dashboardDefinitionId={activeDashboard!.id}
                  widget={widget}
                  onEdit={openEditWidgetWizard}
                  onRemoved={removeSavedWidget}
                  onCloned={cloneSavedWidget}
                  onHidden={hideSavedWidget}
                />
              </div>
            ))}

            <div key="materialExplorer">
              <DashboardWidgetCard
                widgetId="materialExplorer"
                title="Material Explorer"
                subtitle="Backend paginated and sortable. Click material/source/risk values to filter."
                icon={<Factory size={18} />}
                chartTypes={["table"]}
                exportRows={
                  (materials?.items ?? []) as unknown as Record<string, unknown>[]
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
                    type="button"
                    disabled={(materials?.page ?? 1) <= 1}
                    onClick={() =>
                      setFilter("page", Math.max((materials?.page ?? 1) - 1, 1))
                    }
                  >
                    Previous
                  </button>

                  <span>
                    Page {materials?.page ?? 1} / {materials?.totalPages ?? 0} —{" "}
                    {materials?.totalCount ?? 0} materials
                  </span>

                  <button
                    className="secondary-button"
                    type="button"
                    disabled={
                      (materials?.page ?? 1) >= (materials?.totalPages ?? 0)
                    }
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
              </DashboardWidgetCard>
            </div>
          </DashboardGridLayout>

          <DrilldownDrawer />
        </>
      ) : null}

      <WidgetBuilderWizard
        isOpen={isWidgetBuilderOpen}
        dashboardDefinitionId={activeDashboard?.id}
        existingWidget={editingWidget}
        onClose={() => {
          setIsWidgetBuilderOpen(false);
          setEditingWidget(null);
        }}
        onWidgetSaved={handleWidgetSaved}
      />
    </main>
  );
}

// ─────────────────────────────────────────────────────────────
// Saved dashboard widget
// FE-HARD-006:
// - Memoized component.
// - Stable parent callbacks.
// - Stable query object.
// - Stable derived chart rows / keys / selection.
// - No refetch just because parent rerendered.
// ─────────────────────────────────────────────────────────────

type SavedDashboardWidgetProps = {
  dashboardDefinitionId: string;
  widget: DashboardWidgetDefinitionRecord;
  onEdit: (widget: DashboardWidgetDefinitionRecord) => void | Promise<void>;
  onRemoved: (widget: DashboardWidgetDefinitionRecord) => Promise<void>;
  onCloned: (widget: DashboardWidgetDefinitionRecord) => Promise<void>;
  onHidden?: (widget: DashboardWidgetDefinitionRecord) => void | Promise<void>;
};

const SavedDashboardWidget = memo(function SavedDashboardWidget({
  dashboardDefinitionId,
  widget,
  onEdit,
  onRemoved,
  onCloned,
  onHidden,
}: SavedDashboardWidgetProps) {
  const [result, setResult] = useState<DashboardWidgetQueryResult | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isRemoving, setIsRemoving] = useState(false);
  const [isCloning, setIsCloning] = useState(false);

  const widgetQueryKey = useMemo(
    () =>
      [
        dashboardDefinitionId,
        widget.id,
        widget.widgetCode,
        widget.widgetTitle,
        widget.widgetType,
        widget.chartType,
        widget.dimensionCode,
        widget.measureCode,
        widget.parameterCode ?? "",
        widget.filterJson,
        widget.displayOptionsJson,
        widget.layoutJson,
        widget.sortOrder,
        widget.isActive,
        widget.isSynthetic,
        widget.sourceSystem ?? "",
        widget.sourceRecordId ?? "",
      ].join("|"),
    [
      dashboardDefinitionId,
      widget.id,
      widget.widgetCode,
      widget.widgetTitle,
      widget.widgetType,
      widget.chartType,
      widget.dimensionCode,
      widget.measureCode,
      widget.parameterCode,
      widget.filterJson,
      widget.displayOptionsJson,
      widget.layoutJson,
      widget.sortOrder,
      widget.isActive,
      widget.isSynthetic,
      widget.sourceSystem,
      widget.sourceRecordId,
    ]
  );

  const widgetQuery = useMemo(
    () => buildWidgetQueryFromDefinition(widget),
    [widgetQueryKey, widget]
  );

  useEffect(() => {
    let ignore = false;

    async function loadWidget() {
      setIsLoading(true);
      setError(null);

      try {
        const response = await plantProcessApi.queryDashboardWidget(widgetQuery);

        if (!ignore) {
          setResult(response);
        }
      } catch (e) {
        if (!ignore) {
          setError(e);
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    void loadWidget();

    return () => {
      ignore = true;
    };
  }, [widgetQueryKey, widgetQuery]);

  const handleEdit = useCallback(() => {
    void onEdit(widget);
  }, [onEdit, widget]);

  const handleRemove = useCallback(async () => {
    setIsRemoving(true);
    setError(null);

    try {
      await onRemoved(widget);
    } catch (e) {
      setError(e);
    } finally {
      setIsRemoving(false);
    }
  }, [onRemoved, widget]);

  const handleClone = useCallback(async () => {
    setIsCloning(true);
    setError(null);

    try {
      await onCloned(widget);
    } catch (e) {
      setError(e);
    } finally {
      setIsCloning(false);
    }
  }, [onCloned, widget]);

  const handleHide = useCallback(() => {
    if (!onHidden) return;
    void onHidden(widget);
  }, [onHidden, widget]);

  const rows = useMemo<ChartRow[]>(
    () => (result?.rows ?? []) as ChartRow[],
    [result?.rows]
  );

  const categoryKey = useMemo(() => inferCategoryKey(result), [result]);

  const valueKey = useMemo(() => inferValueKey(result), [result]);

  const chartType = useMemo(
    () => normalizeChartType(widget.chartType),
    [widget.chartType]
  );

  const savedSelection = useMemo(
    () => buildSavedWidgetSelection(widget, categoryKey),
    [widget, categoryKey]
  );

  const subtitle = useMemo(
    () =>
      `${widget.dimensionCode || "No dimension"} / ${
        widget.measureCode || "No measure"
      }${widget.parameterCode ? ` / ${widget.parameterCode}` : ""}`,
    [widget.dimensionCode, widget.measureCode, widget.parameterCode]
  );

  return (
    <DashboardWidgetCard
      widgetId={`saved-${widget.id}`}
      title={widget.widgetTitle}
      subtitle={subtitle}
      icon={<BarChart3 size={18} />}
      chartTypes={[chartType, "table"]}
      exportRows={result?.rows ?? []}
      onEdit={handleEdit}
      onRename={handleEdit}
      onRemove={handleRemove}
      onClone={handleClone}
      onHide={handleHide}
    >
      <div className="saved-widget-actions">
        <button
          className="secondary-button"
          onClick={handleClone}
          disabled={isCloning}
          type="button"
        >
          <Copy size={14} />
          {isCloning ? "Cloning..." : "Clone"}
        </button>

        <button
          className="danger-button"
          onClick={handleRemove}
          disabled={isRemoving}
          type="button"
        >
          <Trash2 size={14} />
          {isRemoving ? "Removing..." : "Remove"}
        </button>
      </div>

      {isLoading ? <SkeletonChart height={240} /> : null}

      {error ? <ErrorPanel error={error} /> : null}

      {!isLoading && !error ? (
        rows.length === 0 ? (
          <div className="empty-insight">
            <strong>No data</strong>
            <p>No records for this widget.</p>
          </div>
        ) : chartType === "line" || chartType === "area" ? (
          <InteractiveLineChart
            data={rows}
            categoryKey={categoryKey}
            valueKey={valueKey}
            area={chartType === "area"}
            selection={savedSelection}
          />
        ) : chartType === "pie" || chartType === "donut" ? (
          <InteractivePieChart
            data={rows}
            categoryKey={categoryKey}
            valueKey={valueKey}
            donut={chartType === "donut"}
            selection={savedSelection}
          />
        ) : chartType === "scatter" ? (
          <InteractiveScatterChart
            data={rows}
            xKey={categoryKey}
            yKey={valueKey}
            labelKey={categoryKey}
            selection={savedSelection}
          />
        ) : chartType === "heatmap" ? (
          <InteractiveHeatmap
            data={rows}
            xKey={categoryKey}
            yKey={result?.columns?.[1]?.code ?? valueKey}
            valueKey={valueKey}
            selection={savedSelection}
          />
        ) : chartType === "table" ? (
          <MiniTable rows={rows} />
        ) : (
          <InteractiveBarChart
            data={rows}
            categoryKey={categoryKey}
            valueKey={valueKey}
            selection={savedSelection}
          />
        )
      ) : null}

      {result?.warnings?.length ? (
        <div className="widget-warning-list">
          {result.warnings.map((warning) => (
            <span key={warning}>{warning}</span>
          ))}
        </div>
      ) : null}
    </DashboardWidgetCard>
  );
}, areSavedDashboardWidgetPropsEqual);

function areSavedDashboardWidgetPropsEqual(
  previous: SavedDashboardWidgetProps,
  next: SavedDashboardWidgetProps
) {
  return (
    previous.dashboardDefinitionId === next.dashboardDefinitionId &&
    previous.onEdit === next.onEdit &&
    previous.onRemoved === next.onRemoved &&
    previous.onCloned === next.onCloned &&
    previous.onHidden === next.onHidden &&
    previous.widget.id === next.widget.id &&
    previous.widget.dashboardDefinitionId === next.widget.dashboardDefinitionId &&
    previous.widget.widgetCode === next.widget.widgetCode &&
    previous.widget.widgetTitle === next.widget.widgetTitle &&
    previous.widget.widgetType === next.widget.widgetType &&
    previous.widget.chartType === next.widget.chartType &&
    previous.widget.dimensionCode === next.widget.dimensionCode &&
    previous.widget.measureCode === next.widget.measureCode &&
    previous.widget.parameterCode === next.widget.parameterCode &&
    previous.widget.filterJson === next.widget.filterJson &&
    previous.widget.layoutJson === next.widget.layoutJson &&
    previous.widget.displayOptionsJson === next.widget.displayOptionsJson &&
    previous.widget.sortOrder === next.widget.sortOrder &&
    previous.widget.isActive === next.widget.isActive &&
    previous.widget.isSynthetic === next.widget.isSynthetic &&
    previous.widget.sourceSystem === next.widget.sourceSystem &&
    previous.widget.sourceRecordId === next.widget.sourceRecordId
  );
}

// ─────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────

function buildWidgetQueryFromDefinition(widget: DashboardWidgetDefinitionRecord) {
  const displayOptions = parseJsonObject(widget.displayOptionsJson);

  return {
    widgetType: widget.widgetType,
    chartType: widget.chartType,
    dimensionCode: widget.dimensionCode || null,
    measureCode: widget.measureCode || null,
    parameterCode: widget.parameterCode || null,
    filters: parseJsonOrNull(widget.filterJson),
    options: {
      maxRows: readNumberOption(displayOptions, "maxRows", 100),
      rawRowLimit: readNumberOption(displayOptions, "rawRowLimit", 500),
      sortDirection: readSortDirectionOption(displayOptions, "sortDirection", "desc"),
      includeWarnings: true,
    },
  };
}

function parseJsonOrNull(value: string | null | undefined) {
  if (!value || value.trim() === "" || value.trim() === "{}") {
    return null;
  }

  try {
    return JSON.parse(value);
  } catch {
    return null;
  }
}

function parseJsonObject(value: string | null | undefined) {
  const parsed = parseJsonOrNull(value);

  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
    return {};
  }

  return parsed as Record<string, unknown>;
}

function readNumberOption(
  source: Record<string, unknown>,
  key: string,
  fallback: number
) {
  const value = source[key];

  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === "string") {
    const parsed = Number(value);

    if (Number.isFinite(parsed)) {
      return parsed;
    }
  }

  return fallback;
}

function readSortDirectionOption(
  source: Record<string, unknown>,
  key: string,
  fallback: SortDirection
): SortDirection {
  const value = source[key];

  if (value === "asc" || value === "desc") {
    return value;
  }

  return fallback;
}

function inferCategoryKey(result: DashboardWidgetQueryResult | null) {
  const columns = result?.columns ?? [];

  return (
    columns.find((column) =>
      ["dimension", "label", "category", "name", "code"].some((keyword) =>
        column.code.toLowerCase().includes(keyword)
      )
    ) ?? columns[0]
  )?.code ?? "dimension";
}

function inferValueKey(result: DashboardWidgetQueryResult | null) {
  const columns = result?.columns ?? [];

  return (
    columns.find((column) =>
      ["number", "decimal", "double", "integer", "int"].some((typeKeyword) =>
        column.dataType.toLowerCase().includes(typeKeyword)
      )
    ) ?? columns[1]
  )?.code ?? "value";
}

type DashboardChartType =
  | "line"
  | "area"
  | "bar"
  | "pie"
  | "donut"
  | "scatter"
  | "heatmap"
  | "table";

function normalizeChartType(value: string | null | undefined): DashboardChartType {
  const normalized = (value ?? "bar").toLowerCase();

  const valid: DashboardChartType[] = [
    "line",
    "area",
    "pie",
    "donut",
    "scatter",
    "heatmap",
    "table",
    "bar",
  ];

  return valid.includes(normalized as DashboardChartType)
    ? (normalized as DashboardChartType)
    : "bar";
}

function buildSavedWidgetSelection(
  widget: DashboardWidgetDefinitionRecord,
  categoryKey: string
): {
  type:
    | "site"
    | "area"
    | "equipment"
    | "sourceSystem"
    | "material"
    | "defect"
    | "riskClass"
    | "shift"
    | "parameter"
    | "dateRange"
    | "generic";
  field: keyof DashboardFilters;
  sourceWidget: string;
  valueKey?: string;
  labelKey?: string;
} {
  const dimensionText = `${widget.dimensionCode ?? ""} ${categoryKey}`.toLowerCase();

  if (dimensionText.includes("source")) {
    return {
      type: "sourceSystem",
      field: "sourceSystem",
      sourceWidget: widget.widgetTitle,
      valueKey: categoryKey,
      labelKey: categoryKey,
    };
  }

  if (dimensionText.includes("defect") || dimensionText.includes("quality")) {
    return {
      type: "defect",
      field: "defectType",
      sourceWidget: widget.widgetTitle,
      valueKey: categoryKey,
      labelKey: categoryKey,
    };
  }

  if (dimensionText.includes("risk")) {
    return {
      type: "riskClass",
      field: "riskClass",
      sourceWidget: widget.widgetTitle,
      valueKey: categoryKey,
      labelKey: categoryKey,
    };
  }

  if (dimensionText.includes("parameter")) {
    return {
      type: "parameter",
      field: "parameterCode",
      sourceWidget: widget.widgetTitle,
      valueKey: categoryKey,
      labelKey: categoryKey,
    };
  }

  if (dimensionText.includes("material")) {
    return {
      type: "material",
      field: "materialCode",
      sourceWidget: widget.widgetTitle,
      valueKey: categoryKey,
      labelKey: categoryKey,
    };
  }

  if (dimensionText.includes("shift") || dimensionText.includes("crew")) {
    return {
      type: "shift",
      field: "shiftCode",
      sourceWidget: widget.widgetTitle,
      valueKey: categoryKey,
      labelKey: categoryKey,
    };
  }

  return {
    type: "generic",
    field: "materialCode",
    sourceWidget: widget.widgetTitle,
    valueKey: categoryKey,
    labelKey: categoryKey,
  };
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
        <p>No records available for this widget and filter context.</p>
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
          {rows.map((row, rowIndex) => (
            <tr key={rowIndex}>
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