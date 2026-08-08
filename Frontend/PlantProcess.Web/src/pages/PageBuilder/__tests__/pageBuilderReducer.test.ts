import { describe, expect, it } from "vitest";
import {
  createInitialPageBuilderState,
  createPageBuilderPayload,
  normalizePageVisibility,
  pageBuilderReducer,
} from "../pageBuilderReducer";

// PPIQ T-041. THE CONTRACT CHANGED DELIBERATELY, SO THESE EXPECTATIONS MOVED.
//
// The previous version of this file asserted against three demo widgets -
// w-risk, w-defects and w-trend - bound to schema_views of one reference plant.
// A new page now starts genuinely empty and its structural kinds come from
// /analytics/dashboard/metadata, so every proof below builds the state it needs
// instead of inheriting one.

describe("pageBuilderReducer", () => {
  it("starts a new page genuinely empty", () => {
    const initial = createInitialPageBuilderState();

    expect(initial.widgets).toHaveLength(0);
    expect(initial.title).toBe("");
    expect(initial.slug).toBe("");
    expect(initial.audienceRoles).toEqual([]);
  });

  it("carries no demo library and no compiled structural grammar", async () => {
    const module = await import("../pageBuilderReducer");
    const exported = Object.keys(module);

    expect(exported).not.toContain("defaultPageBuilderWidgets");
  });

  it("accepts any structural kind code the endpoint publishes", () => {
    const state = createInitialPageBuilderState();

    for (const kind of ["chart", "table", "kpi", "calculated-label", "filter", "container", "text"]) {
      const next = pageBuilderReducer(state, {
        type: "addWidget",
        kind,
        title: "",
        source: "",
        idSeed: kind,
      });

      expect(next.widgets.at(-1)?.kind).toBe(kind);
    }
  });

  it("adds a widget with a deterministic id and layout, and no invented binding", () => {
    const initial = createInitialPageBuilderState();

    const next = pageBuilderReducer(initial, {
      type: "addWidget",
      kind: "chart",
      title: "Yield by grade",
      source: "",
      idSeed: "yield-by-grade",
    });

    expect(next.widgets).toHaveLength(1);
    expect(next.widgets.at(-1)).toMatchObject({
      id: "w-yield-by-grade",
      kind: "chart",
      title: "Yield by grade",
      source: "",
    });
    expect(initial.widgets).toHaveLength(0);
  });

  it("falls back to the kind code rather than to a second copy of the grammar", () => {
    const next = pageBuilderReducer(createInitialPageBuilderState(), {
      type: "addWidget",
      kind: "calculated-label",
      title: "   ",
      source: "",
      idSeed: "unnamed",
    });

    expect(next.widgets.at(-1)?.title).toBe("calculated-label");
  });

  it("carries the audience roles and keeps visibility a separate answer", () => {
    const next = pageBuilderReducer(createInitialPageBuilderState(), {
      type: "updateMeta",
      patch: {
        title: "Shift production",
        slug: "shift-production",
        audienceRoles: ["Engineer", "Viewer"],
        visibility: normalizePageVisibility("Invalid"),
      },
    });

    expect(next.title).toBe("Shift production");
    expect(next.slug).toBe("shift-production");
    expect(next.audienceRoles).toEqual(["Engineer", "Viewer"]);
    expect(next.visibility).toBe("Shared");
  });

  it("removes only the selected widget and leaves the rest of the layout alone", () => {
    const withTwo = ["first", "second"].reduce(
      (state, seed) =>
        pageBuilderReducer(state, {
          type: "addWidget",
          kind: "chart",
          title: seed,
          source: "",
          idSeed: seed,
        }),
      createInitialPageBuilderState(),
    );

    const next = pageBuilderReducer(withTwo, { type: "removeWidget", id: "w-first" });

    expect(next.widgets.map((widget) => widget.id)).toEqual(["w-second"]);
    expect(withTwo.widgets.map((widget) => widget.id)).toEqual(["w-first", "w-second"]);
  });

  it("keeps a moved widget inside the twelve-column grid", () => {
    const state = pageBuilderReducer(createInitialPageBuilderState(), {
      type: "addWidget",
      kind: "chart",
      title: "Moved",
      source: "",
      idSeed: "moved",
    });

    const next = pageBuilderReducer(state, { type: "moveWidget", id: "w-moved", x: 99, y: -4 });
    const moved = next.widgets.find((widget) => widget.id === "w-moved");

    expect(moved?.x).toBe(12 - (moved?.w ?? 0));
    expect(moved?.y).toBe(0);
  });

  it("resizes within the safe minimum and maximum", () => {
    const state = pageBuilderReducer(createInitialPageBuilderState(), {
      type: "addWidget",
      kind: "table",
      title: "Sized",
      source: "",
      idSeed: "sized",
    });

    const tooSmall = pageBuilderReducer(state, { type: "resizeWidget", id: "w-sized", w: -10, h: -1 });
    const tooLarge = pageBuilderReducer(state, { type: "resizeWidget", id: "w-sized", w: 99, h: 99 });

    expect(tooSmall.widgets.at(-1)?.w).toBe(1);
    expect(tooSmall.widgets.at(-1)?.h).toBe(1);
    expect(tooLarge.widgets.at(-1)?.w).toBe(12);
    expect(tooLarge.widgets.at(-1)?.h).toBe(12);
  });

  it("builds a payload that carries the audience and binds nothing it was not given", () => {
    const state = pageBuilderReducer(
      pageBuilderReducer(createInitialPageBuilderState(), {
        type: "updateMeta",
        patch: { title: "Shift production", slug: "shift-production", audienceRoles: ["Engineer"] },
      }),
      { type: "addWidget", kind: "chart", title: "Yield", source: "", idSeed: 42 },
    );

    const payload = createPageBuilderPayload(state);

    expect(payload.slug).toBe("shift-production");
    expect(payload.audienceRoles).toEqual(["Engineer"]);
    expect(payload.layoutJson.grid).toEqual({ columns: 12, rowHeight: 80 });
    expect(payload.layoutJson.widgets).toHaveLength(1);
    expect(payload.widgetBindingsJson.bindings).toEqual([{ widgetId: "w-42", source: "" }]);
  });
});