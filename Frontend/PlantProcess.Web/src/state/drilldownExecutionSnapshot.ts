// T-050 STEP 2B. THE EXECUTION SNAPSHOT THAT TRAVELS WITH THE POINT.
//
// A drill-down asks "where did THIS number come from". The only truthful answer
// is the execution that produced it - the filters, options and bindings as they
// were when the chart was drawn. By the time somebody clicks, the page filters
// may have moved on. Rebuilding the request from them would fetch evidence for
// a DIFFERENT execution and present it as this point's provenance.
//
// So the snapshot is captured at render time and stamped onto the row, exactly
// as the backend row index already is. It travels with the datum through
// sorting, slicing and projection, and the click reads it back off. The stale
// context race is then impossible by construction rather than by remembering.
//
// It also carries the render's rowPopulations, so the drawer can name the
// clicked point's population from the SAME result that produced it, without
// re-querying and without the descriptor list drifting underneath it.

import type {
  DashboardWidgetFilters, DashboardWidgetQueryOptions, DashboardWidgetQueryResult,
  DashboardWidgetRowPopulation,
} from "@/api/product-core/dashboard-widget-types";

export const PPIQ_EXECUTION_SNAPSHOT = "__ppiqExecutionSnapshot" as const;

export interface WidgetExecutionIdentity {
  pageCode?: string | null;
  widgetCode?: string | null;
  widgetDefinitionId?: string | null;
}

/** Everything needed to run the SAME query again, plus who ran it. Plain data:
 *  no closures, so it can be stamped onto a row and survive a state update. */
export interface WidgetExecutionSnapshot {
  kind: "catalogue" | "expression";
  expression?: string | null;
  widgetType?: string | null;
  chartType?: string | null;
  dimensionCode?: string | null;
  measureCode?: string | null;
  parameterCode?: string | null;
  filters: DashboardWidgetFilters | null;
  options: DashboardWidgetQueryOptions;
  identity: WidgetExecutionIdentity;
  rowPopulations?: DashboardWidgetRowPopulation[] | null;
}

type Stamped = Record<string, unknown> & { [PPIQ_EXECUTION_SNAPSHOT]?: WidgetExecutionSnapshot };

/** Non-mutating: result rows are shared. */
export function stampExecutionSnapshot<T extends Record<string, unknown>>(
  rows: readonly T[], snapshot: WidgetExecutionSnapshot,
): (T & Stamped)[] {
  return rows.map((row) => ({ ...row, [PPIQ_EXECUTION_SNAPSHOT]: snapshot }));
}

export function executionSnapshot(row: unknown): WidgetExecutionSnapshot | null {
  if (row === null || typeof row !== "object") return null;
  const value = (row as Stamped)[PPIQ_EXECUTION_SNAPSHOT];
  return value && typeof value === "object" ? value : null;
}

/** The evidence-requesting re-execution. The ONLY place includeExecutionEvidence
 *  is set to true, so an ordinary render cannot acquire an evidence side effect
 *  by accident. */
export async function executeWithEvidence(
  snapshot: WidgetExecutionSnapshot,
  runCatalogue: (query: Record<string, unknown>) => Promise<DashboardWidgetQueryResult>,
  runExpression: (query: Record<string, unknown>) => Promise<DashboardWidgetQueryResult>,
): Promise<DashboardWidgetQueryResult> {
  const options = { ...snapshot.options, includeExecutionEvidence: true };

  if (snapshot.kind === "expression") {
    return runExpression({
      expression: snapshot.expression,
      filters: snapshot.filters,
      options,
    });
  }

  return runCatalogue({
    widgetType: snapshot.widgetType,
    chartType: snapshot.chartType,
    dimensionCode: snapshot.dimensionCode,
    measureCode: snapshot.measureCode,
    parameterCode: snapshot.parameterCode,
    filters: snapshot.filters,
    options,
    executionIdentity: snapshot.identity,
  });
}
