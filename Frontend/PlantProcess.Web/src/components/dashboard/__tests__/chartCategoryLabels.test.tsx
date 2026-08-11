import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it, vi } from "vitest";
import { ExtraChart } from "../ChartExtras";

// T-044 D7. IDENTITY AND DISPLAY ARE TWO DIFFERENT THINGS.
//
// categoryKey used to carry both. EO_EQDEF is a bar chart bound to equipment,
// so its axis read 1933641c-3014-5aaa-ac52-4b47077372de instead of
// "Continuous caster 1". The obvious fix - point the chart at dimensionLabel -
// would have fixed the picture and BROKEN FILTERING, because ChartExtras
// writes the same value into page-level filter state via setFilter. A label
// there matches no row, so every click would have selected nothing while
// looking perfectly correct.
//
// The heatmap path is proved by MOUNTING it, because it renders plain buttons
// and needs no Recharts container. The Cartesian and pie paths go through
// Recharts, which does not lay out in jsdom without a sized container, so they
// are proved by a comment-stripped source guard instead. That is stated rather
// than hidden: a mounted proof where mounting works, a source proof where it
// does not, and no pretence that the second is the first.

const UUID = "1933641c-3014-5aaa-ac52-4b47077372de";
const LABEL = "Continuous caster 1";

const setFilter = vi.fn();
const mergeFilters = vi.fn();

vi.mock("../../../state/DashboardFilterContext", () => ({
  useDashboardFilters: () => ({ filters: {}, setFilter, mergeFilters }),
}));

const ROWS = [
  { equipment: UUID, dimensionLabel: LABEL, value: 4558.82 },
  { equipment: "79227b1e-0000-4000-8000-000000000002", dimensionLabel: "Hot strip mill", value: 4133.73 },
];

describe("T-044 D7 chart category: identity is not display", () => {
  it("shows the human label on a mounted chart path", () => {
    render(
      <ExtraChart
        type="heatmap"
        rows={ROWS}
        categoryKey="equipment"
        labelKey="dimensionLabel"
        valueKey="value"
        field="equipmentId"
      />
    );

    expect(screen.getByText(LABEL)).toBeInTheDocument();
    expect(screen.queryByText(UUID)).toBeNull();
  });

  it("sends the CANONICAL UUID to the filter, never the label", async () => {
    const user = userEvent.setup();
    setFilter.mockClear();

    render(
      <ExtraChart
        type="heatmap"
        rows={ROWS}
        categoryKey="equipment"
        labelKey="dimensionLabel"
        valueKey="value"
        field="equipmentId"
      />
    );

    await user.click(screen.getByText(LABEL));

    expect(setFilter).toHaveBeenCalledTimes(1);
    expect(setFilter).toHaveBeenCalledWith("equipmentId", UUID);
    expect(setFilter).not.toHaveBeenCalledWith("equipmentId", LABEL);
  });

  it("falls back to the identity value when no label column is given", () => {
    render(
      <ExtraChart
        type="heatmap"
        rows={ROWS}
        categoryKey="equipment"
        valueKey="value"
        field="equipmentId"
      />
    );

    expect(screen.getByText(UUID)).toBeInTheDocument();
  });
});

describe("T-044 D7 the Recharts paths, by source", () => {
  const widget = readFileSync(
    resolve(__dirname, "../SavedDashboardWidget.tsx"),
    "utf8"
  ).replace(/\/\*[\s\S]*?\*\//g, "").replace(/^\s*\/\/.*$/gm, "");

  it("resolves a display key separate from the category identity", () => {
    expect(widget).toContain("const displayKey =");
    expect(widget).toContain('column.code === "dimensionLabel"');
  });

  it("gives the three Recharts charts the DISPLAY key", () => {
    const occurrences = widget.split("categoryKey={displayKey}").length - 1;
    expect(occurrences).toBe(3);
  });

  it("keeps the selection value on the IDENTITY key", () => {
    const occurrences = widget.split("valueKey: categoryKey,").length - 1;
    expect(occurrences).toBe(3);
  });

  it("hands ExtraChart the identity and the label separately", () => {
    expect(widget).toContain("categoryKey={categoryKey} labelKey={displayKey}");
  });

  it("introduces no widget-code or dimension-code special case", () => {
    expect(widget).not.toContain('"equipment"');
    expect(widget).not.toContain("EO_EQDEF");
  });
});