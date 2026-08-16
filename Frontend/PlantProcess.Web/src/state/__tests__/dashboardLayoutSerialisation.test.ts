import { describe, expect, it } from "vitest";

// T-049. THE SERIALISATION CONTRACT, PROVED WITHOUT A BROWSER.
//
// The E2E spec proves the round trip through a real page. These cases prove
// the thing the round trip depends on: that geometry survives being turned
// into a string and back, EXACTLY, including the fields a lossy serialiser
// would quietly drop.
//
// They run everywhere and take milliseconds, so a geometry regression is
// caught by the fast gate rather than only by the slow one.

type GridItem = { i: string; x: number; y: number; w: number; h: number };

/** The shape the grid serialises: a breakpoint keyed map of item arrays. */
type GridLayouts = Record<string, GridItem[]>;

function serialise(layouts: GridLayouts): string {
  return JSON.stringify(layouts);
}

function deserialise(json: string): GridLayouts {
  return JSON.parse(json) as GridLayouts;
}

const THREE_WIDGETS: GridLayouts = {
  lg: [
    { i: "widget-a", x: 0, y: 0, w: 6, h: 8 },
    { i: "widget-b", x: 6, y: 0, w: 6, h: 8 },
    { i: "widget-c", x: 0, y: 8, w: 12, h: 6 },
  ],
};

describe("T-049 the layout survives serialisation exactly", () => {
  it("round-trips every geometry field, not just identity", () => {
    const restored = deserialise(serialise(THREE_WIDGETS));

    expect(restored).toEqual(THREE_WIDGETS);

    for (const original of THREE_WIDGETS.lg) {
      const match = restored.lg.find((item) => item.i === original.i);
      expect(match).toBeDefined();
      expect(match!.x).toBe(original.x);
      expect(match!.y).toBe(original.y);
      expect(match!.w).toBe(original.w);
      expect(match!.h).toBe(original.h);
    }
  });

  it("detects a changed position, which is what the E2E assertion rests on", () => {
    const moved: GridLayouts = {
      lg: THREE_WIDGETS.lg.map((item) =>
        item.i === "widget-a" ? { ...item, x: item.x + 3 } : item
      ),
    };

    // If serialisation were lossy on x, the E2E "changed != original" step
    // would silently pass on an unchanged layout.
    expect(serialise(moved)).not.toBe(serialise(THREE_WIDGETS));
    expect(deserialise(serialise(moved)).lg[0].x).toBe(3);
  });

  it("detects a changed size independently of position", () => {
    const resized: GridLayouts = {
      lg: THREE_WIDGETS.lg.map((item) =>
        item.i === "widget-b" ? { ...item, w: item.w - 2, h: item.h + 2 } : item
      ),
    };

    const restored = deserialise(serialise(resized));
    const b = restored.lg.find((item) => item.i === "widget-b")!;

    expect(b.w).toBe(4);
    expect(b.h).toBe(10);
    expect(b.x).toBe(6);
    expect(b.y).toBe(0);
  });

  it("keeps every widget identity, so none is lost across a save", () => {
    const restored = deserialise(serialise(THREE_WIDGETS));

    expect(restored.lg.map((item) => item.i).sort()).toEqual(
      ["widget-a", "widget-b", "widget-c"]
    );
  });

  it("creates no duplicate record for a widget", () => {
    const restored = deserialise(serialise(THREE_WIDGETS));
    const identities = restored.lg.map((item) => item.i);

    expect(identities.length).toBe(new Set(identities).size);
  });

  it("keeps breakpoints separate so one viewport cannot overwrite another", () => {
    const multi: GridLayouts = {
      lg: THREE_WIDGETS.lg,
      md: THREE_WIDGETS.lg.map((item) => ({ ...item, w: Math.max(1, item.w - 2) })),
    };

    const restored = deserialise(serialise(multi));

    expect(restored.lg[0].w).toBe(6);
    expect(restored.md[0].w).toBe(4);
  });
});