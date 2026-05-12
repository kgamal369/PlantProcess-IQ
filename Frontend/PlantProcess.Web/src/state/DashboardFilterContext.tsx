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
  activeFilterCount: number;
}

const DashboardFilterContext =
  createContext<DashboardFilterContextValue | null>(null);

const filterKeys: (keyof DashboardFilters)[] = [
  "siteId",
  "areaId",
  "equipmentId",
  "materialCode",
  "sourceSystem",
  "defectType",
  "parameterCode",
  "riskClass",
  "fromUtc",
  "toUtc",
  "shiftCode",
  "linkMode",
  "genealogyDepth",
  "bins",
  "minimumObservationsPerBin",
];

function parseFilters(searchParams: URLSearchParams): DashboardFilters {
  const filters: DashboardFilters = {};

  for (const key of filterKeys) {
    const value = searchParams.get(key);
    if (!value) continue;

    if (
      key === "genealogyDepth" ||
      key === "bins" ||
      key === "minimumObservationsPerBin"
    ) {
      const parsed = Number(value);
      if (!Number.isNaN(parsed)) {
        (filters as any)[key] = parsed;
      }
    } else {
      (filters as any)[key] = value;
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

      Object.keys(nextFilters).forEach((key) => {
        const typedKey = key as keyof DashboardFilters;
        const value = nextFilters[typedKey];
        if (value === undefined || value === null || value === "") {
          delete (nextFilters as any)[typedKey];
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
      update({ [key]: value } as Partial<DashboardFilters>);
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
      delete (next as any)[key];
      update(next, true);
    },
    [filters, update]
  );

  const clearAllFilters = useCallback(() => {
    setSearchParams(new URLSearchParams(), { replace: false });
  }, [setSearchParams]);

  const activeFilterCount = useMemo(
    () =>
      Object.entries(filters).filter(
        ([, value]) => value !== undefined && value !== null && value !== ""
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
    [
      filters,
      setFilter,
      mergeFilters,
      clearFilter,
      clearAllFilters,
      activeFilterCount,
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