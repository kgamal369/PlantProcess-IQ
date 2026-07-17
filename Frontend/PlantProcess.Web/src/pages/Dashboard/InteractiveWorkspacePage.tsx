import { useCallback, useEffect, useState, type ComponentProps } from "react";
import { useParams } from "react-router-dom";
import { DashboardFilterBar } from "@/components/DashboardFilterBar";
import { DashboardGridLayout } from "@/components/dashboard/DashboardGridLayout";
import { SavedDashboardWidget } from "@/components/dashboard/SavedDashboardWidget";
import { SelectionBreadcrumb } from "@/components/dashboard/SelectionBreadcrumb";
import { useDashboardLayoutPersistence } from "@/hooks/useDashboardLayoutPersistence";
import { dashboardingApi } from "@/api/dashboarding/dashboarding.api";
import { StandardButton } from "@/components/standard";

type WidgetRecord = ComponentProps<typeof SavedDashboardWidget>["widget"];

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
        </div>
      </header>
      <DashboardFilterBar />
      <SelectionBreadcrumb />
      <DashboardGridLayout>
        {dashboard.widgets.map((widget) => (
          <div key={String((widget as { id?: unknown }).id ?? Math.random())}>
            <SavedDashboardWidget
              dashboardDefinitionId={dashboard.id}
              widget={widget}
              onEdit={() => undefined}
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