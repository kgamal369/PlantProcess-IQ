import { DrilldownDrawer } from "@/components/dashboard/DrilldownDrawer";
import { Component, lazy, Suspense, useCallback, useEffect, useState, type ComponentProps, type ErrorInfo, type ReactNode } from "react";
import { useParams } from "react-router-dom";
import { DashboardFilterBar } from "@/components/DashboardFilterBar";
import { DashboardGridLayout } from "@/components/dashboard/DashboardGridLayout";
import { SavedDashboardWidget } from "@/components/dashboard/SavedDashboardWidget";
import { AssociativePanel } from "@/components/dashboard/AssociativePanel";
import { SelectionBreadcrumb } from "@/components/dashboard/SelectionBreadcrumb";
import { useDashboardLayoutPersistence } from "@/hooks/useDashboardLayoutPersistence";
import { dashboardingApi } from "@/api/dashboarding/dashboarding.api";
import { StandardButton } from "@/components/standard";

type WidgetRecord = ComponentProps<typeof SavedDashboardWidget>["widget"];

// Deferred on purpose: the wizard stays out of this page's chunk until it is
// opened. This was never the cause of the earlier failures, but it is still the
// right way to reference a large optional subtree.
const WidgetAuthoringPanel = lazy(
  () => import("@/components/dashboard/widget-authoring/WidgetAuthoringPanel"),
);

// Imports nothing, so it cannot itself be the thing that fails.
class WizardBoundary extends Component<
  { children: ReactNode; onClose: () => void },
  { message: string | null }
> {
  constructor(props: { children: ReactNode; onClose: () => void }) {
    super(props);
    this.state = { message: null };
  }
  static getDerivedStateFromError(error: unknown) {
    return { message: error instanceof Error ? error.message : String(error) };
  }
  componentDidCatch(error: unknown, info: ErrorInfo) {
    console.error("Widget builder failed", error, info);
  }
  render() {
    if (this.state.message === null) { return this.props.children; }
    return (
      <div role="alert" className="ppiq-std-card">
        <h3>The widget builder did not open</h3>
        <p>The fault is inside the builder itself. This workspace is unaffected.</p>
        <code>{this.state.message}</code>
        <StandardButton variant="ghost" onClick={this.props.onClose}>Close</StandardButton>
      </div>
    );
  }
}

type LoadedDashboard = {
  id: string;
  name: string;
  description: string;
  widgets: WidgetRecord[];
};

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" ? (value as Record<string, unknown>) : {};
}

function pickArray(source: Record<string, unknown>, keys: string[]): unknown[] {
  for (const key of keys) {
    const v = source[key];
    if (Array.isArray(v)) return v;
  }
  return [];
}

async function loadDashboardByCode(code: string): Promise<LoadedDashboard | null> {
  const listRaw = await dashboardingApi.getDashboardDefinitions();
  const list = Array.isArray(listRaw)
    ? listRaw
    : pickArray(asRecord(listRaw), ["items", "definitions", "rows", "data"]);
  const match = list
    .map(asRecord)
    .find(
      (d) =>
        String(d["dashboardCode"] ?? d["dashboard_code"] ?? "").toUpperCase() ===
        code.toUpperCase()
    );
  if (!match) return null;
  const id = String(match["id"] ?? "");
  if (!id) return null;
  const fullRaw = await dashboardingApi.getDashboardDefinition(id);
  const full = asRecord(fullRaw);
  const container = asRecord(full["definition"]) ?? full;
  const widgetsRaw = [
    ...pickArray(full, ["widgets", "widgetDefinitions", "dashboardWidgetDefinitions"]),
    ...pickArray(container, ["widgets", "widgetDefinitions", "dashboardWidgetDefinitions"]),
  ];
  return {
    id,
    name: String(full["name"] ?? container["name"] ?? match["name"] ?? code),
    description: String(full["description"] ?? container["description"] ?? match["description"] ?? ""),
    widgets: widgetsRaw as WidgetRecord[],
  };
}

export function InteractiveWorkspacePage({ dashboardCode }: { dashboardCode: string }) {
  const [dashboard, setDashboard] = useState<LoadedDashboard | null>(null);
  const [failed, setFailed] = useState<string | null>(null);
  const [reloadNonce, setReloadNonce] = useState(0);
  // THIS LINE'S POSITION IS THE WHOLE FIX. Two earlier packs and the probe
  // declared it far lower in the component, below the guard clauses, which made
  // it a conditional hook call. React threw at runtime and the top-level
  // boundary said the application could not start. Hooks belong together, at
  // the top, before any guard.
  const [wizardOpen, setWizardOpen] = useState(false);
  // Null means add; a record means edit. Declared here with the other hooks,
  // above every guard clause, for the reason recorded in the add-widget pack.
  const [editing, setEditing] = useState<WidgetRecord | null>(null);

  const persistence = useDashboardLayoutPersistence(dashboard?.id);

  const refresh = useCallback(() => {
    setReloadNonce((n) => n + 1);
  }, []);

  useEffect(() => {
    let alive = true;
    setFailed(null);
    loadDashboardByCode(dashboardCode)
      .then((loaded) => {
        if (!alive) return;
        if (!loaded) {
          setFailed("No dashboard found with code " + dashboardCode + ".");
          return;
        }
        setDashboard(loaded);
      })
      .catch(() => {
        if (!alive) return;
        setFailed("The dashboard service is not reachable.");
      });
    return () => {
      alive = false;
    };
  }, [dashboardCode, reloadNonce]);

  useEffect(() => {
    if (dashboard?.id) {
      void persistence.reloadLayout();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dashboard?.id]);

  if (failed) {
    return (
      <section className="ppiq-std-card">
        <header className="ppiq-std-card__header">
          <h2>Interactive workspace</h2>
        </header>
        <p>{failed}</p>
      </section>
    );
  }
  if (!dashboard) {
    return (
      <section className="ppiq-std-card">
        <header className="ppiq-std-card__header">
          <h2>Interactive workspace</h2>
        </header>
        <p>Loading dashboard...</p>
      </section>
    );
  }

  return (
    <section aria-label={dashboard.name}>
      <header className="ppiq-std-card__header">
        <div>
          <h2>{dashboard.name}</h2>
          <p>{dashboard.description}</p>
        </div>
        <div className="ppiq-journey-actions">
          <StandardButton
            variant="ghost"
            onClick={() => void persistence.saveLayout()}
            disabled={persistence.isSavingLayout}
          >
            {persistence.isSavingLayout ? "Saving layout..." : "Save layout"}
          </StandardButton>
          <StandardButton variant="ghost" onClick={refresh}>
            Refresh widgets
          </StandardButton>
          <StandardButton
            variant="primary"
            data-testid="workspace-add-widget"
            onClick={() => { setEditing(null); setWizardOpen(true); }}
          >
            Add widget
          </StandardButton>
        </div>
      </header>

      {/* Constitution v3 II.6.7: widget authoring opens from the page the
          widget lives on. */}
      {wizardOpen && (
        <WizardBoundary onClose={() => setWizardOpen(false)}>
          <Suspense fallback={<div className="ppiq-std-card">Opening the authoring panel...</div>}>
            <WidgetAuthoringPanel
              isOpen={wizardOpen}
              dashboardDefinitionId={dashboard.id}
              existing={editing as never}
              onClose={() => { setWizardOpen(false); setEditing(null); }}
              onSaved={async () => { await refresh(); }}
            />
          </Suspense>
        </WizardBoundary>
      )}
      <DashboardFilterBar />
      <AssociativePanel />
        <DrilldownDrawer />
        <SelectionBreadcrumb />
      <DashboardGridLayout>
        {dashboard.widgets.map((widget) => (
          <div key={String((widget as { id?: unknown }).id ?? Math.random())}>
            <SavedDashboardWidget
              dashboardDefinitionId={dashboard.id}
              widget={widget}
              onEdit={() => { setEditing(widget); setWizardOpen(true); }}
              onRemoved={refresh}
              onCloned={refresh}
              onHidden={refresh}
            />
          </div>
        ))}
      </DashboardGridLayout>
    </section>
  );
}

export function RoutedInteractiveWorkspacePage() {
  const params = useParams<{ dashboardCode: string }>();
  return (
    <InteractiveWorkspacePage dashboardCode={params.dashboardCode ?? "PRODUCTION_OVERVIEW"} />
  );
}