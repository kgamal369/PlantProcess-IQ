// ============================================================
// FILE: Frontend/PlantProcess.Web/src/state/DashboardFilterContext.tsx
//
// T-094. TWO CONTRACTS, ONE URL.
//
//  * Structural filters and transport controls are typed keys the product owns
//    and are written as themselves: ?siteId=...&page=2
//  * A customer-DECLARED dimension is not a key of this contract. It travels as
//    a repeatable ?dimensionFilter=<published-code>:<value>, which is exactly
//    the shape the backend accepts on both the widget and workspace surfaces.
//
// The old defectType / riskClass / shiftCode keys are gone. They are not
// translated here or anywhere: a request that still carries them is refused by
// the backend with DB10 rather than silently ignored, because an ignored filter
// widens the population and reports the wider number as the answer.
// ============================================================

import {
  createContext,
  useCallback,
  useContext,
  useMemo,
} from "react";
import type { ReactNode } from "react";
import { useSearchParams } from "react-router-dom";
import type { DashboardFilters, DeclaredDimensionFilter } from "../api/productApiClient";
import {
  DECLARED_FILTER_PARAM,
  formatDeclaredFilterParam,
  parseDeclaredFilterParam,
} from "../api/product-core/declared-dimension-types";

interface DashboardFilterContextValue {
  filters: DashboardFilters;
  /** Keyed filters on published declarations, in stable code order. */
  declaredFilters: DeclaredDimensionFilter[];
  setFilter: <K extends keyof DashboardFilters>(
    key: K,
    value: DashboardFilters[K] | undefined
  ) => void;
  setDeclaredFilter: (code: string, value: string | undefined) => void;
  mergeFilters: (patch: Partial<DashboardFilters>) => void;
  clearFilter: (key: keyof DashboardFilters) => void;
  clearDeclaredFilter: (code: string) => void;
  clearAllFilters: () => void;
  /** Count of user-facing filters, structural and declared (excludes pagination/sort). */
  activeFilterCount: number;
}

const DashboardFilterContext =
  createContext<DashboardFilterContextValue | null>(null);

// Structural filters and transport controls that map to URL search params.
const filterKeys: (keyof DashboardFilters)[] = [
  "siteId", "areaId", "equipmentId", "materialCode", "materialUnitType",
  "sourceSystem", "parameterCode",
  "fromUtc", "toUtc", "linkMode",
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
    const value = searchParams.get(String(key));
    if (!value) continue;

    if (numericKeys.has(key)) {
      const parsed = Number(value);
      if (!Number.isNaN(parsed)) {
        (filters as Record<string, unknown>)[String(key)] = parsed;
      }
    } else {
      (filters as Record<string, unknown>)[String(key)] = value;
    }
  }

  return filters;
}

/** FAIL CLOSED. A malformed URL selection must never disappear and widen the
 *  population. The shared parser throws DB09 vocabulary before a request runs. */
function parseDeclared(searchParams: URLSearchParams): DeclaredDimensionFilter[] {
  return searchParams
    .getAll(DECLARED_FILTER_PARAM)
    .map(parseDeclaredFilterParam)
    .sort((left, right) => left.code.localeCompare(right.code));
}

function writeSearchParams(
  filters: DashboardFilters,
  declared: DeclaredDimensionFilter[]
): URLSearchParams {
  const next = new URLSearchParams();

  for (const key of filterKeys) {
    const value = (filters as Record<string, unknown>)[String(key)];
    if (value === undefined || value === null || value === "") continue;
    next.set(String(key), String(value));
  }

  for (const filter of declared) {
    if (!filter.code || !filter.value) continue;
    next.append(DECLARED_FILTER_PARAM, formatDeclaredFilterParam(filter));
  }

  return next;
}

export function DashboardFilterProvider({ children }: { children: ReactNode }) {
  const [searchParams, setSearchParams] = useSearchParams();

  const filters = useMemo(() => parseFilters(searchParams), [searchParams]);
  const declaredFilters = useMemo(() => parseDeclared(searchParams), [searchParams]);

  const update = useCallback(
    (
      patch: Partial<DashboardFilters>,
      declared: DeclaredDimensionFilter[],
      replace = false
    ) => {
      const nextFilters = replace ? patch : { ...filters, ...patch };

      // Purge undefined / null / empty values.
      (Object.keys(nextFilters) as string[]).forEach((key) => {
        const k = key as keyof DashboardFilters;
        if (
          nextFilters[k] === undefined ||
          nextFilters[k] === null ||
          nextFilters[k] === ""
        ) {
          delete (nextFilters as Record<string, unknown>)[String(key)];
        }
      });

      setSearchParams(writeSearchParams(nextFilters, declared), {
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
      update({ [String(key)]: value, page: 1 } as Partial<DashboardFilters>, declaredFilters);
    },
    [update, declaredFilters]
  );

  const setDeclaredFilter = useCallback(
    (code: string, value: string | undefined) => {
      const trimmedCode = code.trim();
      if (!trimmedCode) return;

      const remaining = declaredFilters.filter((filter) => filter.code !== trimmedCode);
      const trimmedValue = (value ?? "").trim();
      const next = trimmedValue
        ? [...remaining, { code: trimmedCode, value: trimmedValue }]
        : remaining;

      update({ page: 1 } as Partial<DashboardFilters>, next);
    },
    [declaredFilters, update]
  );

  const mergeFilters = useCallback(
    (patch: Partial<DashboardFilters>) => {
      update(patch, declaredFilters);
    },
    [update, declaredFilters]
  );

  const clearFilter = useCallback(
    (key: keyof DashboardFilters) => {
      const next = { ...filters };
      delete (next as Record<string, unknown>)[String(key)];
      update(next, declaredFilters, true);
    },
    [filters, declaredFilters, update]
  );

  const clearDeclaredFilter = useCallback(
    (code: string) => {
      update(
        { page: 1 } as Partial<DashboardFilters>,
        declaredFilters.filter((filter) => filter.code !== code)
      );
    },
    [declaredFilters, update]
  );

  const clearAllFilters = useCallback(() => {
    // Preserve pagination state when clearing filters so the user stays
    // on page 1 (reset page too) with their sort preference intact. Declared
    // filters are cleared with everything else.
    const preserved: Partial<DashboardFilters> = {
      page: 1,
      ...(filters.pageSize ? { pageSize: filters.pageSize } : {}),
      ...(filters.sortBy ? { sortBy: filters.sortBy } : {}),
      ...(filters.sortDirection ? { sortDirection: filters.sortDirection } : {}),
    };
    setSearchParams(writeSearchParams(preserved, []), { replace: false });
  }, [filters, setSearchParams]);

  // Only count real user-facing filters, not pagination/sort. A declared filter
  // counts exactly as much as a structural one: it is a selection the user made.
  const activeFilterCount = useMemo(
    () =>
      Object.entries(filters).filter(
        ([key, value]) =>
          !paginationKeys.has(key as keyof DashboardFilters) &&
          value !== undefined &&
          value !== null &&
          value !== ""
      ).length + declaredFilters.length,
    [filters, declaredFilters]
  );

  const value = useMemo<DashboardFilterContextValue>(
    () => ({
      filters,
      declaredFilters,
      setFilter,
      setDeclaredFilter,
      mergeFilters,
      clearFilter,
      clearDeclaredFilter,
      clearAllFilters,
      activeFilterCount,
    }),
    [
      filters, declaredFilters,
      setFilter, setDeclaredFilter, mergeFilters,
      clearFilter, clearDeclaredFilter, clearAllFilters, activeFilterCount,
    ]
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
