import { useCallback, useEffect, useState } from "react";
import { dashboardingApi } from "../api/dashboarding";
import { useDashboardGridLayout } from "../state/DashboardGridLayoutContext";

export interface DashboardLayoutPersistenceState {
  isLoadingLayout: boolean;
  isSavingLayout: boolean;
  layoutError: string | null;
  lastSavedAtUtc: string | null;
  saveLayout: () => Promise<void>;
  reloadLayout: () => Promise<void>;
}

export function useDashboardLayoutPersistence(
  dashboardDefinitionId: string | null | undefined
): DashboardLayoutPersistenceState {
  const { serializeLayouts, replaceLayoutsFromJson } = useDashboardGridLayout();

  const [isLoadingLayout, setIsLoadingLayout] = useState(false);
  const [isSavingLayout, setIsSavingLayout] = useState(false);
  const [layoutError, setLayoutError] = useState<string | null>(null);
  const [lastSavedAtUtc, setLastSavedAtUtc] = useState<string | null>(null);

  const reloadLayout = useCallback(async () => {
    if (!dashboardDefinitionId) return;

    setIsLoadingLayout(true);
    setLayoutError(null);

    try {
      const dashboard = await dashboardingApi.getDashboardDefinition(
        dashboardDefinitionId
      ) as { layoutJson?: string | null };

      replaceLayoutsFromJson(dashboard.layoutJson ?? "{}");
    } catch (error) {
      const message =
        error instanceof Error
          ? error.message
          : "Data refresh did not complete dashboard layout.";

      setLayoutError(message);
      throw error;
    } finally {
      setIsLoadingLayout(false);
    }
  }, [dashboardDefinitionId, replaceLayoutsFromJson]);

  const saveLayout = useCallback(async () => {
    if (!dashboardDefinitionId) {
      throw new Error("Cannot save dashboard layout because no dashboard is selected.");
    }

    setIsSavingLayout(true);
    setLayoutError(null);

    try {
      const layoutJson = serializeLayouts();

      await dashboardingApi.updateDashboardLayout(
        dashboardDefinitionId,
        layoutJson
      );

      setLastSavedAtUtc(new Date().toISOString());
    } catch (error) {
      const message =
        error instanceof Error
          ? error.message
          : "Failed to save dashboard layout.";

      setLayoutError(message);
      throw error;
    } finally {
      setIsSavingLayout(false);
    }
  }, [dashboardDefinitionId, serializeLayouts]);

  useEffect(() => {
    void reloadLayout();
  }, [reloadLayout]);

  return {
    isLoadingLayout,
    isSavingLayout,
    layoutError,
    lastSavedAtUtc,
    saveLayout,
    reloadLayout,
  };
}