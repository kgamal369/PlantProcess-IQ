import { describe, expect, it } from "vitest";
import {
  maximizeWidget,
  minimizeWidget,
  moveWidget,
  normalizeWidgetLayout,
  resizeWidget,
  restoreWidgetLayout,
  serializeWidgetLayout,
  type Phase11WidgetLayoutItem,
} from "./phase11WidgetLayout";

const bounds = { cols: 12, maxRows: 20 };

const widget: Phase11WidgetLayoutItem = {
  id: "risk",
  x: 2,
  y: 3,
  w: 4,
  h: 3,
  minW: 2,
  minH: 2,
  maxW: 12,
  maxH: 12,
  minimized: false,
  maximized: false,
};

describe("Phase 11 widget layout correctness", () => {
  it("T-066 clamps dragged widgets inside the grid", () => {
    const moved = moveWidget(widget, bounds, 99, 99);

    expect(moved.x + moved.w).toBeLessThanOrEqual(bounds.cols);
    expect(moved.y + moved.h).toBeLessThanOrEqual(bounds.maxRows);
  });

  it("T-066 clamps resize to min and max", () => {
    const small = resizeWidget(widget, bounds, -99, -99);
    const large = resizeWidget(widget, bounds, 99, 99);

    expect(small.w).toBe(widget.minW);
    expect(small.h).toBe(widget.minH);
    expect(large.w).toBeLessThanOrEqual(widget.maxW);
    expect(large.h).toBeLessThanOrEqual(widget.maxH);
  });

  it("T-066 minimizes and maximizes deterministically", () => {
    const minimized = minimizeWidget(widget);
    const maximized = maximizeWidget(widget, bounds);

    expect(minimized.minimized).toBe(true);
    expect(minimized.maximized).toBe(false);
    expect(maximized.maximized).toBe(true);
    expect(maximized.minimized).toBe(false);
    expect(maximized.x).toBe(0);
    expect(maximized.w).toBe(bounds.cols);
  });

  it("T-066 persists and restores layout exactly within bounds", () => {
    const normalized = normalizeWidgetLayout(widget, bounds);
    const serialized = serializeWidgetLayout([normalized]);
    const restored = restoreWidgetLayout(serialized, bounds);

    expect(restored).toEqual([normalized]);
  });
});