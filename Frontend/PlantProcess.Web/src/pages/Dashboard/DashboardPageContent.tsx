import { useEffect, useMemo, useState } from "react";
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

import { ActiveFilterChips } from "@/components/ActiveFilterChips";
import { DashboardFilterBar } from "@/components/DashboardFilterBar";
import { ErrorPanel, LoadingPanel } from "@/components/AsyncState";
import { SortableDataTable } from "@/components/SortableDataTable";
import type { SortableColumn } from "@/components/SortableDataTable";
import { DashboardWidgetCard } from "@/components/dashboard/DashboardWidgetCard";
import { DashboardGridLayout } from "@/components/dashboard/DashboardGridLayout";
import { DrilldownDrawer } from "@/components/dashboard/DrilldownDrawer";
import { SelectionBreadcrumb } from "@/components/dashboard/SelectionBreadcrumb";
import { WidgetBuilderWizard } from "@/components/dashboard/WidgetBuilderWizard";
import { useDashboardGridLayout } from "@/state/DashboardGridLayoutContext";

import {
  InteractiveBarChart,
  InteractiveHeatmap,
  InteractiveLineChart,
  InteractivePieChart,
  InteractiveScatterChart,
} from "@/components/charts/InteractiveCharts";
import type { ChartRow } from "@/components/charts/InteractiveCharts";
import { useDashboardFilters } from "@/state/DashboardFilterContext";
import { useDashboardSelections } from "@/state/DashboardSelectionContext";
import { dashboardingApi } from "@/api/dashboarding";

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
  const [error, setError] = useState<unknown>(null);
  const [isWidgetBuilderOpen, setIsWidgetBuilderOpen] = useState(false);
  const [editingWidget, setEditingWidget] =
    useState<DashboardWidgetDefinitionRecord | null>(null);
  const [isReloadingLayout, setIsReloadingLayout] = useState(false);
  const [lastSavedLayoutAtUtc, setLastSavedLayoutAtUtc] = useState<string | null>(null);
  const [layoutMessage, setLayoutMessage] = useState<string | null>(null);

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

  async function loadDashboardDefinitions(preferredDashboardId?: string | null) {
    setIsLoadingDashboards(true);

    try {
      // FIX: EnsureSystemTemplatesAsync is not concurrency-safe on the backend.
      // Two simultaneous calls both try to INSERT the same widget_code and the
      // second hits Postgres unique constraint 23505. Since the first call
      // already created the template, swallow the duplicate-key error and continue.
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
        // else: template already exists — safe to continue
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
          addWidget(`saved-${widget.id}`, { w: 6, h: 8, minW: 4, minH: 5 });
        }
      }
    } catch (loadError) {
      setError(loadError);
    } finally {
      setIsLoadingDashboards(false);
    }
  }

  async function selectDashboard(dashboardId: string) {
    const selected = dashboards.find((x) => x.id === dashboardId) ?? null;
    setActiveDashboard(selected);
    if (selected?.layoutJson) replaceLayoutsFromJson(selected.layoutJson);
    for (const widget of selected?.widgets ?? []) {
      if (widget.isActive) addWidget(`saved-${widget.id}`, { w: 6, h: 8, minW: 4, minH: 5 });
    }
  }

  function openCreateWidgetWizard() { setEditingWidget(null); setIsWidgetBuilderOpen(true); }
  function openEditWidgetWizard(widget: DashboardWidgetDefinitionRecord) { setEditingWidget(widget); setIsWidgetBuilderOpen(true); }

  async function handleWidgetSaved(widgetId: string) {
    if (!activeDashboard) return;
    addWidget(`saved-${widgetId}`, { w: 6, h: 8, minW: 4, minH: 5 });
    await loadDashboardDefinitions(activeDashboard.id);
  }

  async function removeSavedWidget(widget: DashboardWidgetDefinitionRecord) {
    if (!activeDashboard) return;
    removeWidget(`saved-${widget.id}`);
    await plantProcessApi.deactivateDashboardWidgetDefinition(activeDashboard.id, widget.id);
    await loadDashboardDefinitions(activeDashboard.id);
  }

  async function cloneSavedWidget(widget: DashboardWidgetDefinitionRecord) {
    if (!activeDashboard) return;
    const cloned = await plantProcessApi.cloneDashboardWidgetDefinition(
      activeDashboard.id, widget.id,
      { widgetTitle: `${widget.widgetTitle} Copy`, sortOrder: widget.sortOrder + 1 }
    );
    addWidget(`saved-${cloned.id}`, { w: 6, h: 8, minW: 4, minH: 5 });
    await loadDashboardDefinitions(activeDashboard.id);
  }

  async function hideSavedWidget(widget: DashboardWidgetDefinitionRecord) {
    removeWidget(`saved-${widget.id}`);
  }

  async function saveDashboardLayout() {
    if (!activeDashboard) { setError(new Error("No active dashboard is selected.")); return; }
    setIsSavingLayout(true); setError(null); setLayoutMessage(null);
    try {
      await dashboardingApi.updateDashboardLayout(activeDashboard.id, serializeLayouts());
      setLastSavedLayoutAtUtc(new Date().toISOString());
      setLayoutMessage("Dashboard layout saved to backend.");
      await loadDashboardDefinitions(activeDashboard.id);
    } catch (saveError) { setError(saveError); }
    finally { setIsSavingLayout(false); }
  }

  async function reloadDashboardLayout() {
    if (!activeDashboard) { setError(new Error("No active dashboard is selected.")); return; }
    setIsReloadingLayout(true); setError(null); setLayoutMessage(null);
    try {
      const latest = (await dashboardingApi.getDashboardDefinition(activeDashboard.id)) as DashboardDefinitionRecord;
      if (latest.layoutJson) { replaceLayoutsFromJson(latest.layoutJson); setLayoutMessage("Dashboard layout reloaded from backend."); }
      else setLayoutMessage("No backend layout stored for this dashboard yet.");
      await loadDashboardDefinitions(activeDashboard.id);
    } catch (reloadError) { setError(reloadError); }
    finally { setIsReloadingLayout(false); }
  }

  async function refreshReadModels() {
    setIsRefreshingReadModels(true); setError(null);
    try { await plantProcessApi.refreshDashboardReadModels(); await load(); }
    catch (refreshError) { setError(refreshError); }
    finally { setIsRefreshingReadModels(false); }
  }

  useEffect(() => { loadDashboardDefinitions(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, []);

  useEffect(() => {
    let ignore = false;
    async function loadSafe() {
      setIsLoading(true); setError(null);
      try {
        const result = await plantProcessApi.getDashboardWorkspace({ ...filters, page: filters.page ?? 1, pageSize: filters.pageSize ?? 25 });
        if (!ignore) setWorkspace(result);
      } catch (loadError) { if (!ignore) setError(loadError); }
      finally { if (!ignore) setIsLoading(false); }
    }
    loadSafe();
    return () => { ignore = true; };
  }, [filters]);

  const overview = workspace?.overview;
  const quality = workspace?.quality;
  const risk = workspace?.risk;
  const dataQuality = workspace?.dataQuality;
  const materials = workspace?.materials;

  const trendData = useMemo<ChartRow[]>(() =>
    (overview?.defectTrend ?? []).map((x: any) => ({
      date: new Date(x.dateUtc).toLocaleDateString(), isoDate: x.dateUtc,
      defectRate: Number(x.defectRatePercent ?? 0), defects: Number(x.defectEventCount ?? 0), materials: Number(x.materialCount ?? 0),
    })), [overview]);

  const defectData = useMemo<ChartRow[]>(() =>
    (quality?.defectBreakdown ?? []).map((x: any) => ({
      name: x.defectCode ?? x.defectName ?? "Unknown", label: x.defectName ?? x.defectCode ?? "Unknown",
      count: Number(x.count ?? 0), percent: Number(x.percentOfDefects ?? 0),
    })), [quality]);

  const riskClassData = useMemo<ChartRow[]>(() =>
    (risk?.riskClassBreakdown ?? []).map((x: any) => ({
      riskClass: x.riskClass ?? "Unknown", count: Number(x.count ?? 0), percent: Number(x.percent ?? 0),
    })), [risk]);

  const sourceContributionData = useMemo<ChartRow[]>(() => {
    const grouped = new Map<string, number>();
    for (const row of materials?.items ?? []) {
      const source = row.sourceSystem || "Unknown";
      grouped.set(source, (grouped.get(source) ?? 0) + 1);
    }
    return Array.from(grouped.entries()).map(([sourceSystem, count]) => ({ sourceSystem, count }));
  }, [materials]);

  const riskScatterData = useMemo<ChartRow[]>(() =>
    (materials?.items ?? [])
      .filter((row) => row.latestRiskScore !== undefined && row.latestRiskScore !== null)
      .map((row) => ({
        materialCode: row.materialCode, riskScore: Number(row.latestRiskScore ?? 0),
        defects: row.defectEventCount, processSteps: row.processStepCount, parameterObservations: row.parameterObservationCount,
      })), [materials]);

  const heatmapData = useMemo<ChartRow[]>(() => {
    const grouped = new Map<string, number>();
    for (const row of materials?.items ?? []) {
      const key = `${row.latestRiskClass || "Unknown"}||${row.materialUnitType || "Unknown"}`;
      grouped.set(key, (grouped.get(key) ?? 0) + 1);
    }
    return Array.from(grouped.entries()).map(([key, count]) => {
      const [riskClass, materialType] = key.split("||");
      return { riskClass, materialType, count };
    });
  }, [materials]);

  const dataQualityRows = useMemo<Record<string, unknown>[]>(() =>
    (dataQuality?.issueTypeBreakdown ?? []).map((x: any) => ({ issueType: x.code, count: x.count, percent: x.percent })), [dataQuality]);

  const topContributors = useMemo<ChartRow[]>(() =>
    (overview?.topRiskContributors ?? []).map((x: any) => ({
      contributorCode: x.contributorCode ?? x.contributorType ?? "Unknown",
      count: Number(x.count ?? 0), averageRiskScore: Number(x.averageRiskScore ?? 0),
    })), [overview]);

  const materialColumns: SortableColumn<DashboardMaterialRow>[] = [
    { key: "materialCode", title: "Material", sortable: true, render: (row) => (
      <button className="link-button" type="button" onClick={() => {
        applySelection({ type: "material", field: "materialCode", value: row.materialCode, label: row.materialCode, sourceWidget: "Material Explorer" });
        openDrilldown({ title: row.materialCode, subtitle: "Material drilldown", type: "material", payload: row });
      }}>{row.materialCode}</button>) },
    { key: "materialUnitType", title: "Type", sortable: true, render: (row) => row.materialUnitType },
    { key: "productFamily", title: "Family", sortable: true, render: (row) => row.productFamily ?? "-" },
    { key: "gradeOrRecipe", title: "Grade / Recipe", render: (row) => row.gradeOrRecipe ?? "-" },
    { key: "sourceSystem", title: "Source", render: (row) => (
      <button className="link-button" type="button" onClick={() =>
        applySelection({ type: "sourceSystem", field: "sourceSystem", value: row.sourceSystem ?? "Unknown", label: row.sourceSystem ?? "Unknown", sourceWidget: "Material Explorer" })
      }>{row.sourceSystem ?? "-"}</button>) },
    { key: "processStepCount", title: "Steps", align: "right", render: (row) => row.processStepCount },
    { key: "parameterObservationCount", title: "Parameters", align: "right", render: (row) => row.parameterObservationCount },
    { key: "defectEventCount", title: "Defects", align: "right", render: (row) => row.defectEventCount },
    { key: "latestRiskScore", title: "Risk", align: "right", render: (row) => row.latestRiskScore == null ? "-" : formatNumber(row.latestRiskScore) },
    { key: "latestRiskClass", title: "Risk Class", render: (row) => row.latestRiskClass ? (
      <button className={`risk-pill risk-${row.latestRiskClass.toLowerCase()}`} type="button"
        onClick={() => applySelection({ type: "riskClass", field: "riskClass", value: row.latestRiskClass!, label: row.latestRiskClass!, sourceWidget: "Material Explorer" })}
      >{row.latestRiskClass}</button>) : "-" },
    { key: "productionStartUtc", title: "Start", sortable: true, render: (row) => row.productionStartUtc ? new Date(row.productionStartUtc).toLocaleString() : "-" },
  ];

  function updateSort(sortBy: string, sortDirection: SortDirection) {
    setFilter("sortBy", sortBy); setFilter("sortDirection", sortDirection); setFilter("page", 1);
  }

  const defectChartType = getWidgetState("defectBreakdown").chartType ?? "bar";
  const trendChartType = getWidgetState("defectTrend").chartType ?? "line";
  const riskChartType = getWidgetState("riskDistribution").chartType ?? "donut";
  const sourceChartType = getWidgetState("sourceContribution").chartType ?? "bar";

  return (
    <main className="page-shell advanced-dashboard-shell">

      {/* Professional dashboard toolbar — replaces old dashboard-hero */}
      <section className="piq-dash-hero">
        <div className="piq-dash-hero__left">
          <div className="piq-dash-hero__dashboard-row">
            <select
              className="piq-dash-select"
              value={activeDashboard?.id ?? ""}
              onChange={(event) => selectDashboard(event.target.value)}
              disabled={isLoadingDashboards || dashboards.length === 0}
              aria-label="Select active dashboard"
            >
              {dashboards.length === 0 ? <option value="">No dashboards yet</option> : null}
              {dashboards.map((dashboard) => (
                <option key={dashboard.id} value={dashboard.id}>
                  {dashboard.name}{dashboard.isDefault ? " — Default" : ""}
                </option>
              ))}
            </select>
            {activeDashboard?.isSystemTemplate ? (
              <span className="piq-dash-badge piq-dash-badge--system">System template</span>
            ) : null}
            {isLoadingDashboards ? (
              <span className="piq-dash-badge" style={{ opacity: 0.5 }}>Loading…</span>
            ) : null}
          </div>
        </div>

        <div className="piq-dash-hero__actions">
          <button className="piq-dash-btn piq-dash-btn--primary" onClick={openCreateWidgetWizard}
            disabled={!activeDashboard} type="button" title="Add a new custom widget">
            <PlusCircle size={14} />Add widget
          </button>
          <button className="piq-dash-btn" onClick={saveDashboardLayout}
            disabled={!activeDashboard || isSavingLayout || isReloadingLayout} type="button" title="Save widget positions to backend">
            {isSavingLayout ? "Saving…" : "Save layout"}
          </button>
          <button className="piq-dash-btn" onClick={reloadDashboardLayout}
            disabled={!activeDashboard || isSavingLayout || isReloadingLayout} type="button" title="Reload positions from last saved state">
            {isReloadingLayout ? "Reloading…" : "Reload layout"}
          </button>
          <button className="piq-dash-btn" onClick={refreshReadModels}
            disabled={isRefreshingReadModels} type="button" title="Rebuild all read-models from source data">
            <RefreshCw size={14} />{isRefreshingReadModels ? "Refreshing…" : "Refresh data"}
          </button>
        </div>
      </section>

      {layoutMessage ? (
        <div className="success-banner">
              {layoutMessage}
              {lastSavedLayoutAtUtc ? (
                <span className="muted-text"> Last saved: {new Date(lastSavedLayoutAtUtc).toLocaleTimeString()}</span>
              ) : null}
            </div>
          ) : null}

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

      {/* Process Steps — GitBranch: steps in a process flow/tree */}
      <InteractiveMetric
        icon={<GitBranch size={20} />}
        label="Process Steps"
        value={overview?.processSteps ?? 0}
        note="Filtered process executions"
      />

      {/* Parameter Observations — FlaskConical: laboratory/measurement readings */}
      <InteractiveMetric
        icon={<FlaskConical size={20} />}
        label="Parameter Observations"
        value={overview?.parameterObservations ?? 0}
        note="Aggregated sensor & measurement data"
      />

      {/* Defect Rate — Bug: defects are bugs in the production process */}
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

      {/* High Risk Materials — AlertOctagon: octagon = STOP/danger (stronger than triangle) */}
      <InteractiveMetric
        icon={<AlertOctagon size={20} />}
        label="High Risk Materials"
        value={overview?.highRiskMaterials ?? 0}
        note={`${formatNumber(overview?.highRiskRatePercent ?? 0)}% high risk`}
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

      {/* Data Quality Issues — ShieldOff: broken shield = quality protection failed */}
      <InteractiveMetric
        icon={<ShieldOff size={20} />}
        label="Data Quality Issues"
        value={overview?.dataQualityIssues ?? 0}
        note="Detected validation findings"
      />
      </section>

          <DashboardGridLayout>
            <div key="defectTrend">
              <DashboardWidgetCard widgetId="defectTrend" title="Defect Trend" subtitle="Click a date point to filter from that date."
                icon={<Activity size={18} />} chartTypes={["line", "area", "bar", "table"]} exportRows={trendData as Record<string, unknown>[]}>
                {trendChartType === "line" ? (
                  <InteractiveLineChart data={trendData} categoryKey="date" valueKey="defectRate"
                    selection={{ type: "dateRange", field: "fromUtc", sourceWidget: "Defect Trend", valueKey: "isoDate", labelKey: "date" }} />
                ) : trendChartType === "area" ? (
                  <InteractiveLineChart data={trendData} categoryKey="date" valueKey="defectRate" area
                    selection={{ type: "dateRange", field: "fromUtc", sourceWidget: "Defect Trend", valueKey: "isoDate", labelKey: "date" }} />
                ) : trendChartType === "bar" ? (
                  <InteractiveBarChart data={trendData} categoryKey="date" valueKey="defects"
                    selection={{ type: "dateRange", field: "fromUtc", sourceWidget: "Defect Trend", valueKey: "isoDate", labelKey: "date" }} />
                ) : <MiniTable rows={trendData} />}
              </DashboardWidgetCard>
            </div>

            <div key="defectBreakdown">
              <DashboardWidgetCard widgetId="defectBreakdown" title="Defect Breakdown" subtitle="Click bar/pie/donut slice to filter defect type."
                icon={<BarChart3 size={18} />} chartTypes={["bar", "pie", "donut", "table"]} exportRows={defectData as Record<string, unknown>[]}>
                {defectChartType === "bar" ? (
                  <InteractiveBarChart data={defectData} categoryKey="name" valueKey="count"
                    selection={{ type: "defect", field: "defectType", sourceWidget: "Defect Breakdown", valueKey: "name", labelKey: "label" }} />
                ) : defectChartType === "pie" ? (
                  <InteractivePieChart data={defectData} categoryKey="name" valueKey="count"
                    selection={{ type: "defect", field: "defectType", sourceWidget: "Defect Breakdown", valueKey: "name", labelKey: "label" }} />
                ) : defectChartType === "donut" ? (
                  <InteractivePieChart data={defectData} categoryKey="name" valueKey="count" donut
                    selection={{ type: "defect", field: "defectType", sourceWidget: "Defect Breakdown", valueKey: "name", labelKey: "label" }} />
                ) : <MiniTable rows={defectData} />}
              </DashboardWidgetCard>
            </div>

            <div key="riskDistribution">
              <DashboardWidgetCard widgetId="riskDistribution" title="Risk Class Distribution" subtitle="Click class to filter materials by risk."
                icon={<Gauge size={18} />} chartTypes={["donut", "pie", "bar", "table"]} exportRows={riskClassData as Record<string, unknown>[]}>
                {riskChartType === "donut" ? (
                  <InteractivePieChart data={riskClassData} categoryKey="riskClass" valueKey="count" donut
                    selection={{ type: "riskClass", field: "riskClass", sourceWidget: "Risk Distribution", valueKey: "riskClass", labelKey: "riskClass" }} />
                ) : riskChartType === "pie" ? (
                  <InteractivePieChart data={riskClassData} categoryKey="riskClass" valueKey="count"
                    selection={{ type: "riskClass", field: "riskClass", sourceWidget: "Risk Distribution", valueKey: "riskClass", labelKey: "riskClass" }} />
                ) : riskChartType === "bar" ? (
                  <InteractiveBarChart data={riskClassData} categoryKey="riskClass" valueKey="count"
                    selection={{ type: "riskClass", field: "riskClass", sourceWidget: "Risk Distribution", valueKey: "riskClass", labelKey: "riskClass" }} />
                ) : <MiniTable rows={riskClassData} />}
              </DashboardWidgetCard>
            </div>

            <div key="sourceContribution">
              <DashboardWidgetCard widgetId="sourceContribution" title="Source System Contribution" subtitle="Click source to filter by source system."
                icon={<Database size={18} />} chartTypes={["bar", "pie", "donut", "table"]} exportRows={sourceContributionData as Record<string, unknown>[]}>
                {sourceChartType === "bar" ? (
                  <InteractiveBarChart data={sourceContributionData} categoryKey="sourceSystem" valueKey="count"
                    selection={{ type: "sourceSystem", field: "sourceSystem", sourceWidget: "Source Contribution", valueKey: "sourceSystem", labelKey: "sourceSystem" }} />
                ) : sourceChartType === "pie" ? (
                  <InteractivePieChart data={sourceContributionData} categoryKey="sourceSystem" valueKey="count"
                    selection={{ type: "sourceSystem", field: "sourceSystem", sourceWidget: "Source Contribution", valueKey: "sourceSystem", labelKey: "sourceSystem" }} />
                ) : sourceChartType === "donut" ? (
                  <InteractivePieChart data={sourceContributionData} categoryKey="sourceSystem" valueKey="count" donut
                    selection={{ type: "sourceSystem", field: "sourceSystem", sourceWidget: "Source Contribution", valueKey: "sourceSystem", labelKey: "sourceSystem" }} />
                ) : <MiniTable rows={sourceContributionData} />}
              </DashboardWidgetCard>
            </div>

            <div key="riskScatter">
              <DashboardWidgetCard widgetId="riskScatter" title="Risk vs Defect Scatter" subtitle="Click a material point to filter and open drilldown."
                icon={<ScatterIcon size={18} />} chartTypes={["scatter", "table"]} exportRows={riskScatterData as Record<string, unknown>[]}>
                {(getWidgetState("riskScatter").chartType ?? "scatter") === "scatter" ? (
                  <InteractiveScatterChart data={riskScatterData} xKey="defects" yKey="riskScore" zKey="parameterObservations" labelKey="materialCode"
                    selection={{ type: "material", field: "materialCode", sourceWidget: "Risk vs Defect Scatter", valueKey: "materialCode", labelKey: "materialCode" }} />
                ) : <MiniTable rows={riskScatterData} />}
              </DashboardWidgetCard>
            </div>

            <div key="qualityHeatmap">
              <DashboardWidgetCard widgetId="qualityHeatmap" title="Quality Heatmap" subtitle="Material type vs risk class density from current result set."
                icon={<Grid3X3 size={18} />} chartTypes={["heatmap", "table"]} exportRows={heatmapData as Record<string, unknown>[]}>
                {(getWidgetState("qualityHeatmap").chartType ?? "heatmap") === "heatmap" ? (
                  <InteractiveHeatmap data={heatmapData} xKey="materialType" yKey="riskClass" valueKey="count"
                    selection={{ type: "riskClass", field: "riskClass", sourceWidget: "Quality Heatmap", valueKey: "riskClass", labelKey: "riskClass" }} />
                ) : <MiniTable rows={heatmapData} />}
              </DashboardWidgetCard>
            </div>

            <div key="topContributors">
              <DashboardWidgetCard widgetId="topContributors" title="Top Risk Contributors" subtitle="Click contributor to use it as selected parameter."
                icon={<Activity size={18} />} chartTypes={["bar", "table"]} exportRows={topContributors as Record<string, unknown>[]}>
                {(getWidgetState("topContributors").chartType ?? "bar") === "bar" ? (
                  <InteractiveBarChart data={topContributors} categoryKey="contributorCode" valueKey="count"
                    selection={{ type: "parameter", field: "parameterCode", sourceWidget: "Top Risk Contributors", valueKey: "contributorCode", labelKey: "contributorCode" }} />
                ) : <MiniTable rows={topContributors} />}
              </DashboardWidgetCard>
            </div>

            <div key="dataQuality">
              <DashboardWidgetCard widgetId="dataQuality" title="Data Quality" subtitle="Readiness signals for selected material population."
                icon={<AlertTriangle size={18} />} chartTypes={["table", "bar"]} exportRows={dataQualityRows}>
                {(getWidgetState("dataQuality").chartType ?? "table") === "bar" ? (
                  <InteractiveBarChart data={dataQualityRows as ChartRow[]} categoryKey="issueType" valueKey="count"
                    selection={{ type: "generic", field: "materialCode", sourceWidget: "Data Quality", valueKey: "issueType", labelKey: "issueType" }} />
                ) : <MiniTable rows={dataQualityRows as ChartRow[]} />}
              </DashboardWidgetCard>
            </div>

            {(activeDashboard?.widgets ?? []).filter((w) => w.isActive).map((widget) => (
              <div key={`saved-${widget.id}`}>
                <SavedDashboardWidget
                  dashboardDefinitionId={activeDashboard!.id} widget={widget}
                  onEdit={() => openEditWidgetWizard(widget)}
                  onRemoved={() => removeSavedWidget(widget)}
                  onCloned={() => cloneSavedWidget(widget)}
                  onHidden={() => hideSavedWidget(widget)}
                />
              </div>
            ))}

            <div key="materialExplorer">
              <DashboardWidgetCard widgetId="materialExplorer" title="Material Explorer"
                subtitle="Backend paginated and sortable. Click material/source/risk values to filter."
                icon={<Factory size={18} />} chartTypes={["table"]}
                exportRows={(materials?.items ?? []) as unknown as Record<string, unknown>[]}>
                <SortableDataTable rows={materials?.items ?? []} columns={materialColumns}
                  sortBy={filters.sortBy} sortDirection={filters.sortDirection ?? "desc"}
                  onSort={updateSort} emptyText="No materials match the selected filters." />
                <div className="pagination-row">
                  <button className="secondary-button" type="button"
                    disabled={(materials?.page ?? 1) <= 1}
                    onClick={() => setFilter("page", Math.max((materials?.page ?? 1) - 1, 1))}>
                    Previous
                  </button>
                  <span>Page {materials?.page ?? 1} / {materials?.totalPages ?? 0} — {materials?.totalCount ?? 0} materials</span>
                  <button className="secondary-button" type="button"
                    disabled={(materials?.page ?? 1) >= (materials?.totalPages ?? 0)}
                    onClick={() => setFilter("page", (materials?.page ?? 1) + 1)}>
                    Next
                  </button>
                  <select value={filters.pageSize ?? 25}
                    onChange={(e) => { setFilter("pageSize", Number(e.target.value)); setFilter("page", 1); }}>
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

      <WidgetBuilderWizard isOpen={isWidgetBuilderOpen} dashboardDefinitionId={activeDashboard?.id}
        existingWidget={editingWidget}
        onClose={() => { setIsWidgetBuilderOpen(false); setEditingWidget(null); }}
        onWidgetSaved={handleWidgetSaved} />
    </main>
  );
}

// ── SavedDashboardWidget ──────────────────────────────────────
function SavedDashboardWidget({ widget, onEdit, onRemoved, onCloned, onHidden }:
  { dashboardDefinitionId: string; widget: DashboardWidgetDefinitionRecord;
    onEdit: () => void | Promise<void>; onRemoved: () => Promise<void>;
    onCloned: () => Promise<void>; onHidden?: () => void | Promise<void>; }) {
  const [result, setResult] = useState<DashboardWidgetQueryResult | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isRemoving, setIsRemoving] = useState(false);
  const [isCloning, setIsCloning] = useState(false);

  useEffect(() => {
    let ignore = false;
    async function loadWidget() {
      setIsLoading(true); setError(null);
      try {
        const response = await plantProcessApi.queryDashboardWidget(buildWidgetQueryFromDefinition(widget));
        if (!ignore) setResult(response);
      } catch (e) { if (!ignore) setError(e); }
      finally { if (!ignore) setIsLoading(false); }
    }
    loadWidget();
    return () => { ignore = true; };
  }, [widget]);

  async function handleRemove() {
    setIsRemoving(true); setError(null);
    try { await onRemoved(); } catch (e) { setError(e); } finally { setIsRemoving(false); }
  }
  async function handleClone() {
    setIsCloning(true); setError(null);
    try { await onCloned(); } catch (e) { setError(e); } finally { setIsCloning(false); }
  }

  const rows = (result?.rows ?? []) as ChartRow[];
  const categoryKey = inferCategoryKey(result);
  const valueKey = inferValueKey(result);
  const chartType = normalizeChartType(widget.chartType);
  const savedSelection = buildSavedWidgetSelection(widget, categoryKey);

  return (
    <DashboardWidgetCard widgetId={`saved-${widget.id}`} title={widget.widgetTitle}
      subtitle={`${widget.dimensionCode || "No dimension"} / ${widget.measureCode || "No measure"}${widget.parameterCode ? ` / ${widget.parameterCode}` : ""}`}
      icon={<BarChart3 size={18} />} chartTypes={[chartType, "table"]} exportRows={result?.rows ?? []}
      onEdit={onEdit} onRename={onEdit} onRemove={handleRemove} onClone={handleClone} onHide={onHidden}>
      <div className="saved-widget-actions">
        <button className="secondary-button" onClick={handleClone} disabled={isCloning} type="button">
          <Copy size={14} />{isCloning ? "Cloning..." : "Clone"}
        </button>
        <button className="danger-button" onClick={handleRemove} disabled={isRemoving} type="button">
          <Trash2 size={14} />{isRemoving ? "Removing..." : "Remove"}
        </button>
      </div>
      {isLoading ? <LoadingPanel /> : null}
      {error ? <ErrorPanel error={error} /> : null}
      {!isLoading && !error ? (
        rows.length === 0 ? (
          <div className="empty-insight"><strong>No data</strong><p>No records for this widget.</p></div>
        ) : chartType === "line" || chartType === "area" ? (
          <InteractiveLineChart data={rows} categoryKey={categoryKey} valueKey={valueKey} area={chartType === "area"} selection={savedSelection} />
        ) : chartType === "pie" || chartType === "donut" ? (
          <InteractivePieChart data={rows} categoryKey={categoryKey} valueKey={valueKey} donut={chartType === "donut"} selection={savedSelection} />
        ) : chartType === "scatter" ? (
          <InteractiveScatterChart data={rows} xKey={categoryKey} yKey={valueKey} labelKey={categoryKey} selection={savedSelection} />
        ) : chartType === "heatmap" ? (
          <InteractiveHeatmap data={rows} xKey={categoryKey} yKey={result?.columns?.[1]?.code ?? valueKey} valueKey={valueKey} selection={savedSelection} />
        ) : chartType === "table" ? <MiniTable rows={rows} />
        : <InteractiveBarChart data={rows} categoryKey={categoryKey} valueKey={valueKey} selection={savedSelection} />
      ) : null}
      {result?.warnings?.length ? (
        <div className="widget-warning-list">{result.warnings.map((w) => <span key={w}>{w}</span>)}</div>
      ) : null}
    </DashboardWidgetCard>
  );
}

// ── Helpers ───────────────────────────────────────────────────
function buildWidgetQueryFromDefinition(widget: DashboardWidgetDefinitionRecord) {
  return { widgetType: widget.widgetType, chartType: widget.chartType, dimensionCode: widget.dimensionCode || null,
    measureCode: widget.measureCode || null, parameterCode: widget.parameterCode || null,
    filters: parseJsonOrNull(widget.filterJson),
    options: { maxRows: 100, rawRowLimit: 500, sortDirection: "desc" as SortDirection, includeWarnings: true } };
}
function parseJsonOrNull(value: string | null | undefined) {
  if (!value || value.trim() === "" || value.trim() === "{}") return null;
  try { return JSON.parse(value); } catch { return null; }
}
function inferCategoryKey(result: DashboardWidgetQueryResult | null) {
  const cols = result?.columns ?? [];
  return (cols.find((c) => ["dimension","label","category","name","code"].some((k) => c.code.toLowerCase().includes(k))) ?? cols[0])?.code ?? "dimension";
}
function inferValueKey(result: DashboardWidgetQueryResult | null) {
  const cols = result?.columns ?? [];
  return (cols.find((c) => ["number","decimal","double","integer","int"].some((t) => c.dataType.toLowerCase().includes(t))) ?? cols[1])?.code ?? "value";
}
type DashboardChartType = "line" | "area" | "bar" | "pie" | "donut" | "scatter" | "heatmap" | "table";

function normalizeChartType(value: string | null | undefined): DashboardChartType {
  const n = (value ?? "bar").toLowerCase();
  const valid: DashboardChartType[] = ["line","area","pie","donut","scatter","heatmap","table","bar"];
  return (valid.includes(n as DashboardChartType) ? n : "bar") as DashboardChartType;
}

function buildSavedWidgetSelection(widget: DashboardWidgetDefinitionRecord, categoryKey: string):
  { type: "site"|"area"|"equipment"|"sourceSystem"|"material"|"defect"|"riskClass"|"shift"|"parameter"|"dateRange"|"generic";
    field: keyof DashboardFilters; sourceWidget: string; valueKey?: string; labelKey?: string; } {
  const d = `${widget.dimensionCode ?? ""} ${categoryKey}`.toLowerCase();
  if (d.includes("source")) return { type:"sourceSystem", field:"sourceSystem", sourceWidget:widget.widgetTitle, valueKey:categoryKey, labelKey:categoryKey };
  if (d.includes("defect")||d.includes("quality")) return { type:"defect", field:"defectType", sourceWidget:widget.widgetTitle, valueKey:categoryKey, labelKey:categoryKey };
  if (d.includes("risk")) return { type:"riskClass", field:"riskClass", sourceWidget:widget.widgetTitle, valueKey:categoryKey, labelKey:categoryKey };
  if (d.includes("parameter")) return { type:"parameter", field:"parameterCode", sourceWidget:widget.widgetTitle, valueKey:categoryKey, labelKey:categoryKey };
  if (d.includes("material")) return { type:"material", field:"materialCode", sourceWidget:widget.widgetTitle, valueKey:categoryKey, labelKey:categoryKey };
  if (d.includes("shift")||d.includes("crew")) return { type:"shift", field:"shiftCode", sourceWidget:widget.widgetTitle, valueKey:categoryKey, labelKey:categoryKey };
  return { type:"generic", field:"materialCode", sourceWidget:widget.widgetTitle, valueKey:categoryKey, labelKey:categoryKey };
}

function InteractiveMetric({ icon, label, value, note, accent, onClick }:
  { icon: ReactNode; label: string; value: string|number; note: string; accent?: "danger"|"warning"; onClick?: () => void; }) {
  return (
    <button className={`metric-tile ${accent ? `metric-tile--${accent}` : ""}`} onClick={onClick} type="button">
      <div className="metric-icon">{icon}</div>
      <div><span>{label}</span><strong>{value}</strong><small>{note}</small></div>
    </button>
  );
}

function MiniTable({ rows }: { rows: ChartRow[] }) {
  if (!rows.length) return (
    <div className="empty-insight"><strong>No data</strong><p>No records available for this widget and filter context.</p></div>
  );
  const columns = Object.keys(rows[0]);
  return (
    <div className="table-wrap">
      <table>
        <thead><tr>{columns.map((c) => <th key={c}>{c}</th>)}</tr></thead>
        <tbody>{rows.map((row, i) => <tr key={i}>{columns.map((c) => <td key={c}>{formatCell(row[c])}</td>)}</tr>)}</tbody>
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
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value);
}
