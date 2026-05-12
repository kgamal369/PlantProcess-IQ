// ============================================================
// TASK 7 — Validate filter interaction
// FILE: Frontend/PlantProcess.Web/src/state/DashboardFilterContext.tsx
//
// CHANGES vs current version:
//  1. activeFilterCount excludes pagination and sort params (page,
//     pageSize, sortBy, sortDirection) — these are not user filters.
//  2. clearAllFilters preserves sort/pagination params when clearing.
//  3. setFilter now resets page to 1 automatically so users always
//     see the first page after applying a new filter.
//  4. Numeric filter keys list separated clearly for clarity.
// ============================================================

import {
  createContext,
  useCallback,
  useContext,
  useMemo,
} from "react";
import type { ReactNode } from "react";
import { useSearchParams } from "react-router-dom";
import type { DashboardFilters } from "../api/plantProcessApi";

interface DashboardFilterContextValue {
  filters: DashboardFilters;
  setFilter: <K extends keyof DashboardFilters>(
    key: K,
    value: DashboardFilters[K] | undefined
  ) => void;
  mergeFilters: (patch: Partial<DashboardFilters>) => void;
  clearFilter: (key: keyof DashboardFilters) => void;
  clearAllFilters: () => void;
  /** Count of user-facing filters (excludes pagination/sort). */
  activeFilterCount: number;
}

const DashboardFilterContext =
  createContext<DashboardFilterContextValue | null>(null);

// All filter keys that map to URL search params.
const filterKeys: (keyof DashboardFilters)[] = [
  "siteId", "areaId", "equipmentId", "materialCode",
  "sourceSystem", "defectType", "parameterCode", "riskClass",
  "fromUtc", "toUtc", "shiftCode", "linkMode",
  "genealogyDepth", "bins", "minimumObservationsPerBin",
  "page", "pageSize", "sortBy", "sortDirection",
];

// These are not user-facing filters — excluded from activeFilterCount.
const paginationKeys = new Set<keyof DashboardFilters>([
  "page", "pageSize", "sortBy", "sortDirection",
]);

const numericKeys = new Set<keyof DashboardFilters>([
  "genealogyDepth", "bins", "minimumObservationsPerBin", "page", "pageSize",
]);

function parseFilters(searchParams: URLSearchParams): DashboardFilters {
  const filters: DashboardFilters = {};

  for (const key of filterKeys) {
    const value = searchParams.get(key);
    if (!value) continue;

    if (numericKeys.has(key)) {
      const parsed = Number(value);
      if (!Number.isNaN(parsed)) {
        (filters as Record<string, unknown>)[key] = parsed;
      }
    } else {
      (filters as Record<string, unknown>)[key] = value;
    }
  }

  return filters;
}

function writeFiltersToSearchParams(filters: DashboardFilters): URLSearchParams {
  const next = new URLSearchParams();

  for (const key of filterKeys) {
    const value = filters[key];
    if (value === undefined || value === null || value === "") continue;
    next.set(key, String(value));
  }

  return next;
}

export function DashboardFilterProvider({ children }: { children: ReactNode }) {
  const [searchParams, setSearchParams] = useSearchParams();

  const filters = useMemo(() => parseFilters(searchParams), [searchParams]);

  const update = useCallback(
    (patch: Partial<DashboardFilters>, replace = false) => {
      const nextFilters = replace ? patch : { ...filters, ...patch };

      // Purge undefined / null / empty values.
      Object.keys(nextFilters).forEach((key) => {
        const k = key as keyof DashboardFilters;
        if (
          nextFilters[k] === undefined ||
          nextFilters[k] === null ||
          nextFilters[k] === ""
        ) {
          delete (nextFilters as Record<string, unknown>)[key];
        }
      });

      setSearchParams(writeFiltersToSearchParams(nextFilters), {
        replace: false,
      });
    },
    [filters, setSearchParams]
  );

  const setFilter = useCallback(
    <K extends keyof DashboardFilters>(
      key: K,
      value: DashboardFilters[K] | undefined
    ) => {
      // Reset to page 1 whenever a filter changes.
      update({ [key]: value, page: 1 } as Partial<DashboardFilters>);
    },
    [update]
  );

  const mergeFilters = useCallback(
    (patch: Partial<DashboardFilters>) => {
      update(patch);
    },
    [update]
  );

  const clearFilter = useCallback(
    (key: keyof DashboardFilters) => {
      const next = { ...filters };
      delete (next as Record<string, unknown>)[key];
      update(next, true);
    },
    [filters, update]
  );

  const clearAllFilters = useCallback(() => {
    // Preserve pagination state when clearing filters so the user stays
    // on page 1 (reset page too) with their sort preference intact.
    const preserved: Partial<DashboardFilters> = {
      page: 1,
      ...(filters.pageSize ? { pageSize: filters.pageSize } : {}),
      ...(filters.sortBy ? { sortBy: filters.sortBy } : {}),
      ...(filters.sortDirection ? { sortDirection: filters.sortDirection } : {}),
    };
    setSearchParams(writeFiltersToSearchParams(preserved), { replace: false });
  }, [filters, setSearchParams]);

  // Only count real user-facing filters, not pagination/sort.
  const activeFilterCount = useMemo(
    () =>
      Object.entries(filters).filter(
        ([key, value]) =>
          !paginationKeys.has(key as keyof DashboardFilters) &&
          value !== undefined &&
          value !== null &&
          value !== ""
      ).length,
    [filters]
  );

  const value = useMemo<DashboardFilterContextValue>(
    () => ({
      filters,
      setFilter,
      mergeFilters,
      clearFilter,
      clearAllFilters,
      activeFilterCount,
    }),
    [filters, setFilter, mergeFilters, clearFilter, clearAllFilters, activeFilterCount]
  );

  return (
    <DashboardFilterContext.Provider value={value}>
      {children}
    </DashboardFilterContext.Provider>
  );
}

export function useDashboardFilters() {
  const context = useContext(DashboardFilterContext);

  if (!context) {
    throw new Error(
      "useDashboardFilters must be used inside DashboardFilterProvider."
    );
  }

  return context;
}
