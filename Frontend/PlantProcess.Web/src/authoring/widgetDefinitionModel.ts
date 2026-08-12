// PPIQ T-038 pack 01. THE WIDGET DEFINITION MODEL.
//
// His invariant for this pack, and it is the whole acceptance:
//
//   an existing saved widget -> load into S2 authoring state -> compile with
//   NO edits -> the save payload is contractually the same widget.
//
// So this module is pure. No React, no network, no randomness. Everything the
// panel decided inline while rendering is decided here where it can be proved,
// and the decisions are the panel's OWN decisions carried across unchanged -
// this is a move, not a redesign. Where a rule looked wrong it was still
// carried, and named in the report rather than quietly improved, because a
// widget that saves differently after T-038 than before it is a regression the
// customer sees and no test would have asked for.
//
// The one thing deliberately NOT carried is the randomness the panel called
// inside slug(). A pure function cannot own randomness, so the caller supplies
// the suffix. That also makes the create path testable, which it was not
// before. This comment does not spell that call out, because the purity guard
// below scans this very file for it.

import {
  EMPTY_ROLE_BINDING, readRoleBinding, writeRoleBinding,
  type WidgetRoleBinding,
} from "@/api/product-core/widget-role-binding";

// Chapter 4 section 5.1.11: catalogue binding is the simple path, the authored
// query is the general one. Both produce one artifact class.
export type WidgetBindMode = "catalogue" | "query";

/** The saved shape, mirroring AuthoredWidget on the retiring panel. */
export interface WidgetDefinitionRecord {
  id?: string;
  widgetCode?: string;
  widgetTitle?: string;
  widgetType?: string;
  chartType?: string;
  dimensionCode?: string;
  measureCode?: string;
  parameterCode?: string | null;
  filterJson?: string;
  layoutJson?: string;
  displayOptionsJson?: string;
  sortOrder?: number;
  queryExpression?: string | null;
  expressionEnabled?: boolean;
}

export interface WidgetFilterRow { code: string; value: string }

/** Everything the S2 face holds while the author works. */
export interface S2AuthoringState {
  title: string;
  chartType: string;
  dimensionCode: string;
  measureCode: string;
  parameterCode: string;
  filters: WidgetFilterRow[];
  bindMode: WidgetBindMode;
  expression: string;
  roleBinding: WidgetRoleBinding;
}

/** The three server-declared shapes this model reads. Rule 1: no literals. */
export interface ChartTypeSpec {
  code: string; category: string;
  supportsDimension: boolean; supportsMeasure: boolean;
}
export interface FieldSpec { code: string; requiresParameterCode: boolean }

export interface WidgetSavePayload {
  widgetCode: string;
  widgetTitle: string;
  widgetType: string;
  chartType: string;
  dimensionCode: string;
  measureCode: string;
  parameterCode: string | null;
  filterJson: string;
  layoutJson: string;
  displayOptionsJson: string;
  sortOrder: number;
  queryExpression: string | null;
  isSynthetic: boolean;
}

/** Where the save goes. Identity is part of the contract, not an afterthought. */
export interface WidgetSaveTarget {
  mode: "create" | "update";
  dashboardDefinitionId: string;
  widgetId: string | null;
}

export const EMPTY_S2_STATE: S2AuthoringState = {
  title: "", chartType: "", dimensionCode: "", measureCode: "",
  parameterCode: "", filters: [], bindMode: "catalogue", expression: "",
  roleBinding: EMPTY_ROLE_BINDING,
};

// A malformed filter blob is not a reason to lose the widget, so it is read as
// no filters. Empty and null values are dropped on the way in, exactly as the
// panel dropped them, so a filter with no value never becomes a filter.
export function parseFilterJson(json?: string | null): WidgetFilterRow[] {
  if (!json) { return []; }
  try {
    const obj = JSON.parse(json) as Record<string, unknown>;
    if (!obj || typeof obj !== "object" || Array.isArray(obj)) { return []; }
    return Object.keys(obj)
      .filter((k) => obj[k] !== null && obj[k] !== undefined && String(obj[k]) !== "")
      .map((k) => ({ code: k, value: String(obj[k]) }));
  } catch { return []; }
}

export function toFilterJson(rows: readonly WidgetFilterRow[]): string {
  const out: Record<string, string> = {};
  for (const r of rows) { if (r.code && r.value) { out[r.code] = r.value; } }
  return JSON.stringify(out);
}

/**
 * Load. A widget authored as a query REOPENS as a query: showing an empty
 * catalogue form for a widget that has an expression would read as the
 * expression having been lost.
 */
export function loadS2State(existing?: WidgetDefinitionRecord | null): S2AuthoringState {
  return {
    title: existing?.widgetTitle ?? "",
    chartType: existing?.chartType ?? "",
    dimensionCode: existing?.dimensionCode ?? "",
    measureCode: existing?.measureCode ?? "",
    parameterCode: existing?.parameterCode ?? "",
    filters: parseFilterJson(existing?.filterJson),
    bindMode: existing?.queryExpression ? "query" : "catalogue",
    expression: existing?.queryExpression ?? "",
    roleBinding: readRoleBinding(existing?.displayOptionsJson) ?? EMPTY_ROLE_BINDING,
  };
}

/**
 * The server declares per chart type whether it uses a dimension and a
 * measure, so the face follows the catalogue instead of assuming both. Before
 * a chart type is chosen, both are open.
 */
/**
 * What a chart type does with its bindings, as the chart type itself declares
 * them. T-046 Pack 4B2 gives the shape a name so `saveRefusal` can ask both
 * questions instead of receiving one boolean and guessing the other.
 */
export interface ChartBindingCapabilities {
  usesDimension: boolean;
  usesMeasure: boolean;
}

export function chartCapabilities(
  chartTypes: readonly ChartTypeSpec[], chartType: string,
): ChartBindingCapabilities {
  if (!chartType) { return { usesDimension: true, usesMeasure: true }; }
  const spec = chartTypes.find((c) => c.code === chartType) ?? null;
  return {
    usesDimension: spec?.supportsDimension ?? true,
    usesMeasure: spec?.supportsMeasure ?? true,
  };
}

export function requiresParameter(
  dimensions: readonly FieldSpec[], measures: readonly FieldSpec[],
  dimensionCode: string, measureCode: string,
): boolean {
  const d = dimensions.find((x) => x.code === dimensionCode);
  const m = measures.find((x) => x.code === measureCode);
  return Boolean(d?.requiresParameterCode || m?.requiresParameterCode);
}

/** The chart category the widget is filed under when it is new. */
export function widgetTypeFor(
  _chartTypes: readonly ChartTypeSpec[], chartType: string,
): string {
  // PPIQ T-042. WidgetType is the PERSISTENCE PROTOCOL KIND, not the chart's
  // analytical category. Reading `category` off the metadata sent "Comparison"
  // to a server whose contract is kpi | chart | table, and every Bar widget was
  // refused with 400 Unsupported widget type. The chart type decides the kind.
  const normalized = chartType.trim().toLowerCase();

  if (normalized === "kpi") {
    return "kpi";
  }

  if (normalized === "table") {
    return "table";
  }

  return "chart";
}

/**
 * The code a new widget gets. Deterministic: the caller supplies the suffix,
 * because a pure function may not own randomness. An existing widget keeps the
 * code it was saved under - a widget that changed identity on edit would lose
 * every reference to it.
 */
export function widgetCodeFor(
  existing: WidgetDefinitionRecord | null | undefined,
  title: string, suffix: string,
): string {
  if (existing?.widgetCode) { return existing.widgetCode; }
  const base = title.trim().toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/^_+|_+$/g, "");
  return (base || "widget") + "_" + suffix;
}

/**
 * The refusal, in the words the author already reads today. Null means the
 * definition may be saved. The sentences are byte-identical to the panel's so
 * that nothing the author sees changes when the surface does.
 */
export function saveRefusal(
  state: S2AuthoringState, capabilities: ChartBindingCapabilities,
): string | null {
  if (!state.title.trim()) { return "Give the widget a title."; }
  if (!state.chartType) { return "Choose a chart type."; }

  // T-046 Pack 4B2. THE CLIENT CONVERGES ON THE SERVER RULE.
  //
  // This read "usesMeasure && !measureCode && !dimensionCode": it refused only
  // when BOTH were missing, so a widget with a dimension and no measure passed
  // here and was then refused by the server, which requires a measure. A client
  // that knowingly permits what the server deterministically refuses teaches an
  // author that Save is a guess.
  //
  // The two questions are now asked separately, because they have different
  // answers and an author fixing one should not be told about the other.
  if (capabilities.usesMeasure && !state.measureCode) {
    return "Choose a measure so the widget has something to show.";
  }

  // AND THE CHART DECIDES WHETHER A DIMENSION IS NEEDED, not this function.
  // capabilities comes from the chart type's own declaration, so a KPI - which
  // declares supportsDimension = false - saves with a measure and no dimension,
  // exactly as the seventeen-type grammar says it should.
  if (capabilities.usesDimension && !state.dimensionCode) {
    return "Choose a dimension for this chart type.";
  }

  return null;
}

export function saveTarget(
  existing: WidgetDefinitionRecord | null | undefined,
  dashboardDefinitionId: string,
): WidgetSaveTarget {
  const id = existing?.id ?? null;
  return {
    mode: id ? "update" : "create",
    dashboardDefinitionId,
    widgetId: id,
  };
}

/**
 * Compile. Every field either comes from the author's state or is carried
 * forward from the existing record; nothing is invented and nothing the model
 * does not understand is dropped.
 *
 * displayOptionsJson MERGES: the role mapping is written beside whatever else
 * the blob already held, so an unrelated key put there by another surface
 * survives an edit here.
 *
 * queryExpression is null in catalogue mode, which is how an author moves a
 * widget back from an authored query to catalogue binding.
 */
export function toWidgetPayload(
  state: S2AuthoringState,
  existing: WidgetDefinitionRecord | null | undefined,
  catalogue: {
    chartTypes: readonly ChartTypeSpec[];
    dimensions: readonly FieldSpec[];
    measures: readonly FieldSpec[];
  },
  newCodeSuffix: string,
): WidgetSavePayload {
  const needsParameter = requiresParameter(
    catalogue.dimensions, catalogue.measures, state.dimensionCode, state.measureCode);
  const isQuery = state.bindMode === "query";
  return {
    widgetCode: widgetCodeFor(existing, state.title, newCodeSuffix),
    widgetTitle: state.title.trim(),
    widgetType: existing?.widgetType ?? widgetTypeFor(catalogue.chartTypes, state.chartType),
    chartType: state.chartType,
    dimensionCode: state.dimensionCode,
    measureCode: state.measureCode,
    parameterCode: needsParameter && state.parameterCode ? state.parameterCode : null,
    filterJson: toFilterJson(state.filters),
    layoutJson: existing?.layoutJson ?? "{}",
    displayOptionsJson: writeRoleBinding(
      existing?.displayOptionsJson ?? "{}",
      isQuery ? state.roleBinding : EMPTY_ROLE_BINDING),
    sortOrder: existing?.sortOrder ?? 0,
    queryExpression: isQuery && state.expression.trim() ? state.expression : null,
    isSynthetic: false,
  };
}