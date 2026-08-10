import { useCallback, useEffect, useRef, useState } from "react";
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

/**
 * T-043 S3. The layout document carries more than the grid.
 *
 * Sheets live inside layout_json under option A, so a caller may read the
 * whole document when it arrives and contribute keys when it is written.
 * Both are optional, so every existing caller is untouched.
 */
export interface DashboardLayoutDocumentHooks {
  onLayoutJsonLoaded?: (layoutJson: string) => void;
  buildExtraDocument?: () => Record<string, unknown>;
}

function withExtraDocument(
  layoutJson: string,
  extra: Record<string, unknown> | undefined
): string {
  if (!extra) return layoutJson;

  try {
    const base = JSON.parse(layoutJson) as Record<string, unknown>;
    return JSON.stringify({ ...base, ...extra });
  } catch {
    // Never lose the layout to a merge. If the grid cannot parse its own
    // serialisation the layout still goes and the extra keys are dropped for
    // this save, rather than the save going nowhere at all.
    return layoutJson;
  }
}

export function useDashboardLayoutPersistence(
  dashboardDefinitionId: string | null | undefined,
  documentHooks?: DashboardLayoutDocumentHooks
): DashboardLayoutPersistenceState {
  const { serializeLayouts, replaceLayoutsFromJson } = useDashboardGridLayout();

  // Held in a ref and deliberately NOT in any dependency array. The caller
  // rebuilds these closures every render, and reloadLayout is the dependency
  // of an effect that calls reloadLayout, so depending on them directly would
  // refetch the layout on every render forever.
  const documentRef = useRef(documentHooks);
  documentRef.current = documentHooks;

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

      const layoutJson = dashboard.layoutJson ?? "{}";
      replaceLayoutsFromJson(layoutJson);
      documentRef.current?.onLayoutJsonLoaded?.(layoutJson);
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
      const layoutJson = withExtraDocument(
        serializeLayouts(),
        documentRef.current?.buildExtraDocument?.()
      );

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