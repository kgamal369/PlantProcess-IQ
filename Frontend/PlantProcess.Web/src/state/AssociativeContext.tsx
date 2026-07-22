import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { apiClient } from "../api/http";
import { useDashboardFilters } from "./DashboardFilterContext";
import { ASSOC_FIELDS, type AssocField } from "./associativeFields";

/** M2-37 associative engine (Qlik spec S0), client-orchestrated:
 * possible-set per field = the existing, registry-validated widget query for
 * that field's dimension, with the current selections MINUS the field's own
 * (so alternatives inside a field stay selectable - the Qlik semantic).
 * all-set = the same query, unfiltered, cached at mount.
 * excluded = all minus possible. selected = the field's current filter value. */

export type ValueState = "selected" | "possible" | "excluded";
export type FieldAssoc = {
  field: AssocField;
  available: boolean;
  loading: boolean;
  all: string[];
  states: Map<string, ValueState>;
  possibleCount: number;
};

type Ctx = {
  enabled: boolean;
  setEnabled: (v: boolean) => void;
  fields: FieldAssoc[];
  toggleValue: (fieldKey: string, value: string) => void;
};

const AssociativeCtx = createContext<Ctx | null>(null);
export const useAssociative = () => {
  const c = useContext(AssociativeCtx);
  if (!c) throw new Error("useAssociative outside provider");
  return c;
};

type QueryRow = Record<string, unknown>;
async function dimensionValues(dimension: string, filters: Record<string, unknown>): Promise<string[] | null> {
  try {
    const res = await apiClient.post<{ rows?: QueryRow[]; data?: QueryRow[] }>(
      "/analytics/dashboard/widgets/query",
      {
        widgetType: "chart", chartType: "bar",
        dimensionCode: dimension, measureCode: "observationCount",
        parameterCode: null, filters,
        options: { maxRows: 500, rawRowLimit: 500, sortDirection: "desc", includeWarnings: false },
      }
    );
    const rows = (res.rows ?? res.data ?? []) as QueryRow[];
    const vals = rows
      .map((r) => String(r["dimension"] ?? r["label"] ?? r["key"] ?? r[dimension] ?? ""))
      .filter((v) => v !== "");
    return Array.from(new Set(vals));
  } catch {
    return null; // registry does not support this dimension -> honest degradation
  }
}

export function AssociativeProvider({ children }: { children: ReactNode }) {
  const { filters, setFilter } = useDashboardFilters();
  const [enabled, setEnabled] = useState(true);
  const [allSets, setAllSets] = useState<Record<string, string[] | null>>({});
  const [possibleSets, setPossibleSets] = useState<Record<string, string[] | null>>({});
  const [loading, setLoading] = useState<Record<string, boolean>>({});
  const timer = useRef<number | null>(null);
  const generation = useRef(0);

  // all-sets once at mount (unfiltered enumeration per field)
  useEffect(() => {
    let stop = false;
    (async () => {
      for (const f of ASSOC_FIELDS) {
        const vals = await dimensionValues(f.dimension, {});
        if (stop) return;
        setAllSets((s) => ({ ...s, [f.key]: vals }));
        if (vals === null) console.warn(`[associative] dimension '${f.dimension}' unavailable; field ${f.key} degraded`);
      }
    })();
    return () => { stop = true; };
  }, []);

  const refresh = useCallback(() => {
    const gen = ++generation.current;
    const g = (filters ?? {}) as Record<string, unknown>;
    ASSOC_FIELDS.forEach(async (f) => {
      if (allSets[f.key] === null) return; // unavailable
      setLoading((l) => ({ ...l, [f.key]: true }));
      const minusOwn: Record<string, unknown> = {};
      for (const k of ASSOC_FIELDS.map((x) => x.key)) {
        if (k === f.key) continue;
        const v = g[k];
        if (v !== undefined && v !== null && v !== "") minusOwn[k] = v;
      }
      const vals = await dimensionValues(f.dimension, minusOwn);
      if (generation.current !== gen) return; // stale
      setPossibleSets((s) => ({ ...s, [f.key]: vals }));
      setLoading((l) => ({ ...l, [f.key]: false }));
    });
  }, [filters, allSets]);

  useEffect(() => {
    if (!enabled) return;
    if (timer.current) window.clearTimeout(timer.current);
    timer.current = window.setTimeout(refresh, 250);
    return () => { if (timer.current) window.clearTimeout(timer.current); };
  }, [enabled, refresh]);

  const fields: FieldAssoc[] = useMemo(() => {
    const g = (filters ?? {}) as Record<string, unknown>;
    return ASSOC_FIELDS.map((f) => {
      const all = allSets[f.key];
      const possible = possibleSets[f.key];
      const selectedVal = g[f.key] !== undefined && g[f.key] !== null && g[f.key] !== "" ? String(g[f.key]) : null;
      const states = new Map<string, ValueState>();
      if (all) {
        const poss = new Set(possible ?? all);
        for (const v of all) {
          states.set(v, v === selectedVal ? "selected" : poss.has(v) ? "possible" : "excluded");
        }
        if (selectedVal && !states.has(selectedVal)) states.set(selectedVal, "selected");
      }
      return {
        field: f,
        available: all !== null && all !== undefined,
        loading: !!loading[f.key],
        all: all ?? [],
        states,
        possibleCount: (possible ?? all ?? []).length,
      };
    });
  }, [filters, allSets, possibleSets, loading]);

  const toggleValue = useCallback((fieldKey: string, value: string) => {
    const g = (filters ?? {}) as Record<string, unknown>;
    const current = g[fieldKey] !== undefined && g[fieldKey] !== null ? String(g[fieldKey]) : null;
    // Qlik semantic: clicking an excluded value is allowed - the state pivots.
    setFilter(fieldKey as never, (current === value ? undefined : value) as never);
  }, [filters, setFilter]);

  const value = useMemo(() => ({ enabled, setEnabled, fields, toggleValue }), [enabled, fields, toggleValue]);
  return <AssociativeCtx.Provider value={value}>{children}</AssociativeCtx.Provider>;
}