// PPIQ T-038 pack 01 acceptance.
//
// THE INVARIANT HE SET IS THE FIRST DESCRIBE BLOCK, and it is the one that
// matters: load a saved widget, change nothing, compile, and the payload is
// contractually the same widget. Identity, dashboard target, expression,
// display options, role binding, title and configuration all survive.
//
// No fixture here is a plant name. These are the shapes a widget definition
// has, not the vocabulary of anyone's plant.

import { describe, expect, it } from "vitest";
import {
  EMPTY_S2_STATE, chartCapabilities, loadS2State, parseFilterJson,
  requiresParameter, saveRefusal, saveTarget, toFilterJson, toWidgetPayload,
  widgetCodeFor, widgetTypeFor,
  type ChartTypeSpec, type FieldSpec, type WidgetDefinitionRecord,
} from "./widgetDefinitionModel";
import { readRoleBinding } from "@/api/product-core/widget-role-binding";

const CHART_TYPES: ChartTypeSpec[] = [
  { code: "bar", category: "chart", supportsDimension: true, supportsMeasure: true },
  { code: "kpi", category: "tile", supportsDimension: false, supportsMeasure: true },
];
const DIMENSIONS: FieldSpec[] = [
  { code: "dim_group", requiresParameterCode: false },
  { code: "dim_parametric", requiresParameterCode: true },
];
const MEASURES: FieldSpec[] = [{ code: "mea_total", requiresParameterCode: false }];
const CATALOGUE = { chartTypes: CHART_TYPES, dimensions: DIMENSIONS, measures: MEASURES };

// A widget saved through the surface being retired, with an unrelated key in
// its display options put there by another surface.
const SAVED: WidgetDefinitionRecord = {
  id: "widget-1",
  widgetCode: "throughput_by_group_a1b2c",
  widgetTitle: "Throughput by group",
  widgetType: "chart",
  chartType: "bar",
  dimensionCode: "dim_group",
  measureCode: "mea_total",
  parameterCode: null,
  filterJson: "{\"filter_window\":\"last_7_days\"}",
  layoutJson: "{\"x\":2,\"y\":3}",
  displayOptionsJson: "{\"legend\":\"right\",\"roleBinding\":{\"category\":\"group_code\",\"value\":\"measured_value\",\"secondary\":null}}",
  sortOrder: 4,
  queryExpression: "source canonical dimension group_code measure sum(measured_value)",
};

describe("T-038 the round-trip invariant: load, edit nothing, compile", () => {
  const compiled = toWidgetPayload(loadS2State(SAVED), SAVED, CATALOGUE, "unused");

  it("keeps the widget identity and the code it was saved under", () => {
    expect(compiled.widgetCode).toBe(SAVED.widgetCode);
    const target = saveTarget(SAVED, "dashboard-9");
    expect(target).toEqual({ mode: "update", dashboardDefinitionId: "dashboard-9", widgetId: "widget-1" });
  });

  it("keeps the title, chart type and configuration", () => {
    expect(compiled.widgetTitle).toBe(SAVED.widgetTitle);
    expect(compiled.widgetType).toBe(SAVED.widgetType);
    expect(compiled.chartType).toBe(SAVED.chartType);
    expect(compiled.dimensionCode).toBe(SAVED.dimensionCode);
    expect(compiled.measureCode).toBe(SAVED.measureCode);
    expect(compiled.sortOrder).toBe(SAVED.sortOrder);
    expect(compiled.layoutJson).toBe(SAVED.layoutJson);
  });

  it("keeps the query expression, and reopens the widget as a query", () => {
    expect(loadS2State(SAVED).bindMode).toBe("query");
    expect(compiled.queryExpression).toBe(SAVED.queryExpression);
  });

  it("keeps the filters as the same set, not as the same string", () => {
    expect(JSON.parse(compiled.filterJson)).toEqual(JSON.parse(String(SAVED.filterJson)));
  });

  it("keeps the role binding and every unrelated display option beside it", () => {
    const before = JSON.parse(String(SAVED.displayOptionsJson));
    const after = JSON.parse(compiled.displayOptionsJson);
    expect(after.legend).toBe(before.legend);
    expect(readRoleBinding(compiled.displayOptionsJson)).toEqual(readRoleBinding(SAVED.displayOptionsJson));
  });
});

describe("T-038 loading a saved definition", () => {
  it("reads a catalogue-bound widget back as catalogue-bound", () => {
    const state = loadS2State({ ...SAVED, queryExpression: null });
    expect(state.bindMode).toBe("catalogue");
    expect(state.expression).toBe("");
  });

  it("starts empty when there is nothing to load", () => {
    expect(loadS2State(null)).toEqual(EMPTY_S2_STATE);
    expect(loadS2State(undefined).title).toBe("");
  });

  it("treats a malformed filter blob as no filters rather than losing the widget", () => {
    expect(parseFilterJson("not json at all")).toEqual([]);
    expect(parseFilterJson(undefined)).toEqual([]);
  });

  it("drops a filter that has no value, in and out", () => {
    expect(parseFilterJson("{\"a\":\"1\",\"b\":\"\"}")).toEqual([{ code: "a", value: "1" }]);
    expect(toFilterJson([{ code: "a", value: "1" }, { code: "b", value: "" }])).toBe("{\"a\":\"1\"}");
  });
});

describe("T-038 the catalogue decides, not the surface", () => {
  it("follows the chart type's declared support rather than assuming both", () => {
    expect(chartCapabilities(CHART_TYPES, "kpi")).toEqual({ usesDimension: false, usesMeasure: true });
    expect(chartCapabilities(CHART_TYPES, "bar")).toEqual({ usesDimension: true, usesMeasure: true });
  });

  it("leaves both open before a chart type is chosen, and for one it does not know", () => {
    expect(chartCapabilities(CHART_TYPES, "")).toEqual({ usesDimension: true, usesMeasure: true });
    expect(chartCapabilities(CHART_TYPES, "not_in_the_catalogue")).toEqual({ usesDimension: true, usesMeasure: true });
  });

  it("asks for a parameter only when a chosen field declares it needs one", () => {
    expect(requiresParameter(DIMENSIONS, MEASURES, "dim_group", "mea_total")).toBe(false);
    expect(requiresParameter(DIMENSIONS, MEASURES, "dim_parametric", "")).toBe(true);
  });

  it("keeps a parameter code the definition needs, and carries none it does not", () => {
    const parametric: WidgetDefinitionRecord = {
      ...SAVED, dimensionCode: "dim_parametric", parameterCode: "param_1",
    };
    const kept = toWidgetPayload(loadS2State(parametric), parametric, CATALOGUE, "unused");
    expect(kept.parameterCode).toBe("param_1");
    expect(compiledParameterOf(SAVED)).toBeNull();
  });

  it("files a new widget under the chart type's own category", () => {
    expect(widgetTypeFor(CHART_TYPES, "kpi")).toBe("kpi");
    expect(widgetTypeFor(CHART_TYPES, "table")).toBe("table");
    expect(widgetTypeFor(CHART_TYPES, "bar")).toBe("chart");
    expect(widgetTypeFor(CHART_TYPES, "unknown_type")).toBe("chart");

    // The exact production failure: a metadata category must never become the
    // persisted widget type.
    expect(
      widgetTypeFor(
        [{ code: "bar", category: "Comparison", supportsDimension: true, supportsMeasure: true }],
        "bar",
      ),
    ).toBe("chart");
  });
});

function compiledParameterOf(record: WidgetDefinitionRecord): string | null {
  return toWidgetPayload(loadS2State(record), record, CATALOGUE, "unused").parameterCode;
}

describe("T-038 creating a widget that does not exist yet", () => {
  it("derives a code from the title with the suffix the caller supplied", () => {
    expect(widgetCodeFor(null, "Throughput by group", "a1b2c")).toBe("throughput_by_group_a1b2c");
    expect(widgetCodeFor(null, "   ", "x9")).toBe("widget_x9");
  });

  it("never changes the code of a widget that already has one", () => {
    expect(widgetCodeFor(SAVED, "A completely different title", "zzzzz")).toBe(SAVED.widgetCode);
  });

  it("writes no role binding for a catalogue-bound widget", () => {
    const state = { ...EMPTY_S2_STATE, title: "New", chartType: "bar", dimensionCode: "dim_group" };
    const payload = toWidgetPayload(state, null, CATALOGUE, "n1");
    expect(readRoleBinding(payload.displayOptionsJson)).toBeNull();
    expect(payload.queryExpression).toBeNull();
    expect(payload.layoutJson).toBe("{}");
    expect(payload.sortOrder).toBe(0);
  });
});

describe("T-046 the client refuses exactly what the server refuses", () => {
  const bar = { usesDimension: true, usesMeasure: true };
  const kpi = { usesDimension: false, usesMeasure: true };

  it("names the missing title first", () => {
    expect(saveRefusal(EMPTY_S2_STATE, bar)).toBe("Give the widget a title.");
  });

  it("names the missing chart type next", () => {
    expect(saveRefusal({ ...EMPTY_S2_STATE, title: "A widget" }, bar))
      .toBe("Choose a chart type.");
  });

  // THE DEFECT THIS PACK REMOVES. The old rule refused only when BOTH bindings
  // were missing, so this state passed the client and was refused by the
  // server, which requires a measure.
  it("refuses a dimension with no measure, which the server would refuse", () => {
    const state = { ...EMPTY_S2_STATE, title: "A widget", chartType: "bar", dimensionCode: "dim_group" };
    expect(saveRefusal(state, bar)).toBe("Choose a measure so the widget has something to show.");
  });

  it("asks for a dimension only when the chart type uses one", () => {
    const state = { ...EMPTY_S2_STATE, title: "A widget", chartType: "bar", measureCode: "m_count" };
    expect(saveRefusal(state, bar)).toBe("Choose a dimension for this chart type.");
  });

  // THE REGRESSION THAT MATTERS MOST. A KPI declares supportsDimension = false,
  // the domain entity no longer demands one, and the shell must save it.
  it("saves a KPI with a measure and no dimension", () => {
    const state = { ...EMPTY_S2_STATE, title: "A single number", chartType: "kpi", measureCode: "m_count" };
    expect(saveRefusal(state, kpi)).toBeNull();
  });

  it("saves a bar once both bindings the chart uses are chosen", () => {
    const state = {
      ...EMPTY_S2_STATE, title: "A widget", chartType: "bar",
      dimensionCode: "dim_group", measureCode: "m_count",
    };
    expect(saveRefusal(state, bar)).toBeNull();
  });

  // Each reason stands alone. Collapsing them would put an author back where
  // Pack 4A found them: told that something is wrong, not what.
  it("gives each refusal its own sentence", () => {
    const sentences = new Set([
      saveRefusal(EMPTY_S2_STATE, bar),
      saveRefusal({ ...EMPTY_S2_STATE, title: "A" }, bar),
      saveRefusal({ ...EMPTY_S2_STATE, title: "A", chartType: "bar", dimensionCode: "d" }, bar),
      saveRefusal({ ...EMPTY_S2_STATE, title: "A", chartType: "bar", measureCode: "m" }, bar),
    ]);
    expect(sentences.size).toBe(4);
  });
});

describe("T-046 a definition that was already valid stays valid", () => {
  it("does not refuse a saved definition that was already valid", () => {
    expect(saveRefusal(loadS2State(SAVED), { usesDimension: true, usesMeasure: true })).toBeNull();
  });
});