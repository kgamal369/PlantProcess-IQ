import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { apiClient } from "../api/http";
import { useDashboardFilters } from "./DashboardFilterContext";
import { productApi } from "../api/productApiClient";
import { buildAssociativeFields, type AssocField } from "./associativeFields";

/** M2-37 associative engine (Qlik spec S0), client-orchestrated:
 * possible-set per field = the existing, registry-validated widget query for
 * that field's dimension, with the current selections MINUS the field's own
 * (so alternatives inside a field stay selectable - the Qlik semantic).
 * all-set = the same query, unfiltered, cached at mount.
 * excluded = all minus possible. selected = the field's current filter value. */

/** T-048. The four states Chapter 4 5.1.3 requires.
 *
 * ALTERNATIVE is not a weaker "possible". It means: this field already has a
 * selection, and this value is the road not taken - still reachable in one
 * click, and excluded from nothing. Collapsing it into "possible" hides which
 * field the reader is currently steering by.
 */
export type ValueState = "selected" | "possible" | "excluded" | "alternative";
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

/** PPIQ-SCENE5678: never forward pagination or sort params into a dimension
 *  enumeration - they are not filters and they change nothing about which values
 *  are still possible. */
const PAGINATION_KEYS = ["page", "pageSize", "sortBy", "sortDirection"];

type QueryRow = Record<string, unknown>;
async function dimensionValues(dimension: string, measureCode: string, filters: Record<string, unknown>): Promise<string[] | null> {
  try {
    const res = await apiClient.post<{ rows?: QueryRow[]; data?: QueryRow[] }>(
      "/analytics/dashboard/widgets/query",
      {
        widgetType: "chart", chartType: "bar",
        dimensionCode: dimension, measureCode,
        parameterCode: null, filters,
        options: { maxRows: 500, rawRowLimit: 500, sortDirection: "desc", includeWarnings: false },
      }
    ,
      // DEMO-BI-R1. THIS ENUMERATION MUST NOT SHOUT. The strip asks every
      // dimension for its values, and a source legitimately does not carry
      // every dimension. The backend refuses those pairings by name and the
      // catch below already degrades the field to no values, which is why
      // they render as N/A. The central toast fires BEFORE that catch, so a
      // governed refusal read as "Something went wrong on our side" on every
      // opening state. The error is still thrown and still caught - it just
      // stops being announced as a fault.
      { suppressToast: true });
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
  const [enabled, setEnabled] = useState(false);
  // T-048. Derived once from the published dimension catalogue. Until it
  // arrives the set is empty and the panel shows nothing, which is honest: a
  // placeholder list would be a guess about the plant.
  const [assocFields, setAssocFields] = useState<AssocField[]>([]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const metadata = await productApi.getDashboardMetadata();
        if (cancelled) { return; }
        setAssocFields(buildAssociativeFields(metadata?.dimensions));
      } catch {
        if (!cancelled) { setAssocFields([]); }
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const [allSets, setAllSets] = useState<Record<string, string[] | null>>({});
  const [possibleSets, setPossibleSets] = useState<Record<string, string[] | null>>({});
  const [loading, setLoading] = useState<Record<string, boolean>>({});
  const timer = useRef<number | null>(null);
  const generation = useRef(0);

  // PPIQ-SCENE5678: enumerate all fields in PARALLEL. The previous version
  // awaited each dimension in a for loop, so the eight columns appeared one at a
  // time over a second or more - on the first thing the customer sees.
  useEffect(() => {
    let stop = false;
    Promise.all(
      assocFields.map(async (f) => {
        const vals = await dimensionValues(f.dimension, f.measureCode, {});
        return { key: f.key, dimension: f.dimension, vals };
      })
    ).then((results) => {
      if (stop) return;
      const next: Record<string, string[] | null> = {};
      for (const r of results) {
        next[r.key] = r.vals;
        if (r.vals === null) console.warn(`[associative] dimension '${r.dimension}' unavailable; field ${r.key} degraded`);
      }
      setAllSets((s) => ({ ...s, ...next }));
    });
    return () => { stop = true; };
    // T-048. Depends on the registry-derived set: enumerating once at mount
    // would run against an empty list and never recover.
  }, [assocFields]);

  const refresh = useCallback(() => {
    const gen = ++generation.current;
    const g = (filters ?? {}) as Record<string, unknown>;
    assocFields.forEach(async (f) => {
      if (allSets[f.key] === null) return; // unavailable
      setLoading((l) => ({ ...l, [f.key]: true }));
      // PPIQ-SCENE5678: carry EVERY active workspace filter, minus this field's
      // own selection. The previous version copied only the eight associative
      // keys, so a time-range selection (fromUtc/toUtc) narrowed the widgets but
      // not the panel - the chips and the charts then disagreed on screen.
      const minusOwn: Record<string, unknown> = {};
      for (const k of Object.keys(g)) {
        if (k === f.key) continue;
        if (PAGINATION_KEYS.indexOf(k) >= 0) continue;
        const v = g[k];
        if (v !== undefined && v !== null && v !== "") minusOwn[k] = v;
      }
      const vals = await dimensionValues(f.dimension, f.measureCode, minusOwn);
      if (generation.current !== gen) return; // stale
      setPossibleSets((s) => ({ ...s, [f.key]: vals }));
      setLoading((l) => ({ ...l, [f.key]: false }));
    });
  }, [filters, allSets, assocFields]);

  useEffect(() => {
    if (!enabled) return;
    if (timer.current) window.clearTimeout(timer.current);
    timer.current = window.setTimeout(refresh, 250);
    return () => { if (timer.current) window.clearTimeout(timer.current); };
  }, [enabled, refresh]);

  const fields: FieldAssoc[] = useMemo(() => {
    const g = (filters ?? {}) as Record<string, unknown>;
    return assocFields.map((f) => {
      const all = allSets[f.key];
      const possible = possibleSets[f.key];
      const selectedVal = g[f.key] !== undefined && g[f.key] !== null && g[f.key] !== "" ? String(g[f.key]) : null;
      const states = new Map<string, ValueState>();
      if (all) {
        const poss = new Set(possible ?? all);
        for (const v of all) {
          // Order matters. Excluded is decided before alternative, because a
          // value the current selections have ruled out is excluded whether or
          // not this field happens to carry a selection.
          states.set(
            v,
            v === selectedVal ? "selected"
              : !poss.has(v) ? "excluded"
                : selectedVal ? "alternative"
                  : "possible"
          );
        }
        if (selectedVal && !states.has(selectedVal)) states.set(selectedVal, "selected");
      }
      return {
        field: f,
        // PPIQ-SCENE5678: an empty enumeration is not an available field. It used
        // to render as a titled column with a 0/0 count and no chips, which reads
        // as broken rather than honest. Zero values now degrades to n/a.
        available: all !== null && all !== undefined && all.length > 0,
        loading: !!loading[f.key],
        all: all ?? [],
        states,
        possibleCount: (possible ?? all ?? []).length,
      };
    });
  }, [filters, allSets, possibleSets, loading, assocFields]);

  const toggleValue = useCallback((fieldKey: string, value: string) => {
    const g = (filters ?? {}) as Record<string, unknown>;
    const current = g[fieldKey] !== undefined && g[fieldKey] !== null ? String(g[fieldKey]) : null;
    // Qlik semantic: clicking an excluded value is allowed - the state pivots.
    setFilter(fieldKey as never, (current === value ? undefined : value) as never);
  }, [filters, setFilter]);

  const value = useMemo(() => ({ enabled, setEnabled, fields, toggleValue }), [enabled, fields, toggleValue]);
  return <AssociativeCtx.Provider value={value}>{children}</AssociativeCtx.Provider>;
}