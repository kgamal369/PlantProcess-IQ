import { describe, expect, it } from "vitest";
import {
  createInitialPageBuilderState,
  createPageBuilderPayload,
  normalizePageVisibility,
  pageBuilderReducer,
} from "../pageBuilderReducer";

describe("pageBuilderReducer", () => {
  it("adds a widget with deterministic layout and canonical binding source", () => {
    const initial = createInitialPageBuilderState();

    const next = pageBuilderReducer(initial, {
      type: "addWidget",
      kind: "filter-list",
      title: "Plant filter",
      source: "filter:plant",
      idSeed: "plant-filter",
    });

    expect(next.widgets).toHaveLength(initial.widgets.length + 1);

    const added = next.widgets.at(-1);

    expect(added).toMatchObject({
      id: "w-plant-filter",
      kind: "filter-list",
      title: "Plant filter",
      source: "filter:plant",
      w: 3,
      h: 3,
    });

    expect(added?.x).toBeGreaterThanOrEqual(0);
    expect(added?.x).toBeLessThanOrEqual(9);
  });

  it("removes only the selected widget and preserves the remaining layout", () => {
    const initial = createInitialPageBuilderState();

    const next = pageBuilderReducer(initial, {
      type: "removeWidget",
      id: "w-defects",
    });

    expect(next.widgets.map((widget) => widget.id)).toEqual(["w-risk", "w-trend"]);
    expect(initial.widgets.map((widget) => widget.id)).toEqual([
      "w-risk",
      "w-defects",
      "w-trend",
    ]);
  });

  it("moves widgets inside the 12-column grid boundary", () => {
    const initial = createInitialPageBuilderState();

    const next = pageBuilderReducer(initial, {
      type: "moveWidget",
      id: "w-defects",
      x: 99,
      y: -4,
    });

    const moved = next.widgets.find((widget) => widget.id === "w-defects");

    expect(moved?.w).toBe(5);
    expect(moved?.x).toBe(7);
    expect(moved?.y).toBe(0);
  });

  it("resizes widgets with safe minimum and maximum limits", () => {
    const initial = createInitialPageBuilderState();

    const tooSmall = pageBuilderReducer(initial, {
      type: "resizeWidget",
      id: "w-risk",
      w: -10,
      h: -1,
    });

    const smallWidget = tooSmall.widgets.find((widget) => widget.id === "w-risk");

    expect(smallWidget?.w).toBe(1);
    expect(smallWidget?.h).toBe(1);

    const tooLarge = pageBuilderReducer(initial, {
      type: "resizeWidget",
      id: "w-risk",
      w: 99,
      h: 99,
    });

    const largeWidget = tooLarge.widgets.find((widget) => widget.id === "w-risk");

    expect(largeWidget?.w).toBe(12);
    expect(largeWidget?.h).toBe(12);
  });

  it("updates page metadata and normalizes invalid visibility back to Shared", () => {
    const initial = createInitialPageBuilderState();

    const next = pageBuilderReducer(initial, {
      type: "updateMeta",
      patch: {
        title: "Generated defect page",
        slug: "generated-defect-page",
        visibility: normalizePageVisibility("Invalid"),
      },
    });

    expect(next.title).toBe("Generated defect page");
    expect(next.slug).toBe("generated-defect-page");
    expect(next.visibility).toBe("Shared");
  });

  it("creates a backend-ready PageDefinition payload from reducer state", () => {
    const state = pageBuilderReducer(createInitialPageBuilderState(), {
      type: "addWidget",
      kind: "line",
      title: "Temperature trend",
      source: "schema_view:temperature_daily",
      idSeed: 42,
    });

    const payload = createPageBuilderPayload(state);

    expect(payload.slug).toBe("demo-quality-investigation");
    expect(payload.layoutJson.grid).toEqual({
      columns: 12,
      rowHeight: 80,
    });

    expect(payload.layoutJson.widgets).toHaveLength(4);
    expect(payload.widgetBindingsJson.bindings).toContainEqual({
      widgetId: "w-42",
      source: "schema_view:temperature_daily",
    });
  });
});
