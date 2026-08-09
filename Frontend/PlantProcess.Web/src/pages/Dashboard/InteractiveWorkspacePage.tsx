import { DrilldownDrawer } from "@/components/dashboard/DrilldownDrawer";
import { Component, lazy, Suspense, useCallback, useEffect, useState, type ComponentProps, type ErrorInfo, type ReactNode } from "react";
import { useParams, useSearchParams } from "react-router-dom";
import { applyEvidenceFocus, findWidgetElement } from "./evidenceFocus";
import "./evidenceFocus.css";
import { DashboardFilterBar } from "@/components/DashboardFilterBar";
import { DashboardGridLayout } from "@/components/dashboard/DashboardGridLayout";
import { SavedDashboardWidget } from "@/components/dashboard/SavedDashboardWidget";
import { AssociativePanel } from "@/components/dashboard/AssociativePanel";
import { SelectionBreadcrumb } from "@/components/dashboard/SelectionBreadcrumb";
import { useDashboardLayoutPersistence } from "@/hooks/useDashboardLayoutPersistence";
import { dashboardingApi } from "@/api/dashboarding/dashboarding.api";
import { StandardButton } from "@/components/standard";
import type { WidgetDefinitionRecord } from "@/authoring/widgetDefinitionModel";

type WidgetRecord = ComponentProps<typeof SavedDashboardWidget>["widget"];

// Deferred on purpose: the authoring surface stays out of this page's chunk
// until it is opened. This was never the cause of the earlier failures, but it
// is still the right way to reference a large optional subtree.
//
// T-038. Chapter 4 section 5.1.10 rules that Add Widget reaches THE SHARED
// SHELL IN S2 MODE, and 5.1.12 that Edit opens the same shell with the
// definition already loaded. So this page references one component, and the
// surface it used to open is retired rather than kept beside it.
const SharedAuthoringShell = lazy(() => import("@/authoring/SharedAuthoringShell"));

// Imports nothing, so it cannot itself be the thing that fails.
class AuthoringBoundary extends Component<
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
    console.error("Authoring surface failed", error, info);
  }
  render() {
    if (this.state.message === null) { return this.props.children; }
    return (
      <div role="alert" className="ppiq-std-card">
        <h3>The authoring surface did not open</h3>
        <p>The fault is inside the authoring surface itself. This workspace is unaffected.</p>
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
  const [authoringOpen, setAuthoringOpen] = useState(false);
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
            onClick={() => { setEditing(null); setAuthoringOpen(true); }}
          >
            Add widget
          </StandardButton>
        </div>
      </header>

      {/* Constitution v3 II.6.7: widget authoring opens from the page the
          widget lives on. */}
      {authoringOpen && (
        <AuthoringBoundary onClose={() => setAuthoringOpen(false)}>
          <Suspense fallback={<div className="ppiq-std-card">Opening the authoring surface...</div>}>
            {/* ONE COMPONENT, TWO ENTRY POINTS. Add passes no widget and Edit
                passes the record the grid already holds; nothing else differs,
                because a second door is how two surfaces start. The cast goes
                through unknown deliberately: the grid's record type and the
                definition type describe the same row from two directions. */}
            <SharedAuthoringShell
              purpose="S2"
              dashboardDefinitionId={dashboard.id}
              existingWidget={editing as unknown as WidgetDefinitionRecord | null}
              onClose={() => { setAuthoringOpen(false); setEditing(null); }}
              onSaved={async () => { await refresh(); }}
            />
          </Suspense>
        </AuthoringBoundary>
      )}
      <DashboardFilterBar />
      <AssociativePanel />
        <DrilldownDrawer />
        <SelectionBreadcrumb />
      <DashboardGridLayout>
        {dashboard.widgets.map((widget) => (
          <div
            key={String((widget as { id?: unknown }).id ?? Math.random())}
            /* T-075: the DOM hook an evidence citation uses to find this exact
               widget after Open in page. It is the persisted widget code and
               nothing else. */
            data-widget-code={String((widget as { widgetCode?: unknown }).widgetCode ?? "")}
          >
            <SavedDashboardWidget
              dashboardDefinitionId={dashboard.id}
              widget={widget}
              onEdit={() => { setEditing(widget); setAuthoringOpen(true); }}
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

/**
 * T-075. Focuses the widget an evidence citation came from.
 *
 * The parameter rides the CANONICAL workspace route rather than a new one, the
 * widget is found by its real persisted code, and the effect is limited to
 * bringing it into view and marking it briefly.
 *
 * The widget renders asynchronously, so this polls briefly rather than assuming
 * the element is already there. If it never appears, the page says so: a
 * historical citation pointing at a widget that no longer exists is a real
 * situation, and landing silently on a page with nothing highlighted would
 * leave someone hunting for something that is not there.
 */
function useEvidenceWidgetFocus(): string | null {
  const [searchParams] = useSearchParams();
  const focusWidget = searchParams.get("focusWidget");
  const [missing, setMissing] = useState<string | null>(null);

  useEffect(() => {
    setMissing(null);
    if (!focusWidget) return;

    const started = Date.now();

    const timer = window.setInterval(() => {
      const target = findWidgetElement(document, focusWidget);

      if (target) {
        window.clearInterval(timer);
        applyEvidenceFocus(target, window);
        return;
      }

      if (Date.now() - started > 6000) {
        window.clearInterval(timer);
        setMissing(focusWidget);
      }
    }, 200);

    return () => window.clearInterval(timer);
  }, [focusWidget]);

  return missing;
}

export function RoutedInteractiveWorkspacePage() {
  const params = useParams<{ dashboardCode: string }>();
  const missingWidget = useEvidenceWidgetFocus();

  return (
    <>
      {missingWidget ? (
        <p data-testid="evidence-widget-missing" className="ppiq-evidence-missing">
          The widget this evidence refers to ({missingWidget}) is no longer on this page.
        </p>
      ) : null}
      <InteractiveWorkspacePage dashboardCode={params.dashboardCode ?? "PRODUCTION_OVERVIEW"} />
    </>
  );
}