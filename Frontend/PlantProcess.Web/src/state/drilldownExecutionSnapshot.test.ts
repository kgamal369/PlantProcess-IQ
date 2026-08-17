import { describe, expect, it, vi } from "vitest";
import {
  executeWithEvidence, executionSnapshot, stampExecutionSnapshot,
  type WidgetExecutionSnapshot,
} from "./drilldownExecutionSnapshot";

const A: WidgetExecutionSnapshot = {
  kind: "catalogue",
  widgetType: "chart", chartType: "bar",
  dimensionCode: "shift", measureCode: "defectRate", parameterCode: null,
  filters: { shiftCode: "A" } as never,
  options: { maxRows: 100, includeWarnings: true },
  identity: { pageCode: "PRODUCTION_OVERVIEW", widgetCode: "PO_BAR", widgetDefinitionId: "w-1" },
  rowPopulations: [{ rowIndex: 0 } as never],
};

describe("T-050 execution snapshot", () => {
  it("travels with the row through reorder and slice", () => {
    const rows = stampExecutionSnapshot([{ c: "x" }, { c: "y" }, { c: "z" }], A);
    const rendered = [rows[2], rows[0]];

    expect(executionSnapshot(rendered[0])).toBe(A);
    expect(executionSnapshot(rendered[1])).toBe(A);
  });

  it("does not mutate the source rows", () => {
    const source = [{ c: "x" }];
    stampExecutionSnapshot(source, A);
    expect(executionSnapshot(source[0])).toBeNull();
  });

  it("returns null for an unstamped datum rather than a guess", () => {
    expect(executionSnapshot({ c: "x" })).toBeNull();
    expect(executionSnapshot(null)).toBeNull();
    expect(executionSnapshot("x")).toBeNull();
  });

  it("re-executes the CAPTURED context, not whatever the filters are now", async () => {
    // The point was rendered under shift A. The page has since moved to B.
    // The evidence request must still be A.
    const catalogue = vi.fn().mockResolvedValue({ rows: [] });
    await executeWithEvidence(A, catalogue, vi.fn());

    const sent = catalogue.mock.calls[0][0] as Record<string, unknown>;
    expect(sent.filters).toEqual({ shiftCode: "A" });
    expect(sent.dimensionCode).toBe("shift");
    expect(sent.measureCode).toBe("defectRate");
  });

  it("opts in to evidence and supplies the complete identity", async () => {
    const catalogue = vi.fn().mockResolvedValue({ rows: [] });
    await executeWithEvidence(A, catalogue, vi.fn());

    const sent = catalogue.mock.calls[0][0] as Record<string, unknown>;
    expect((sent.options as Record<string, unknown>).includeExecutionEvidence).toBe(true);
    expect(sent.executionIdentity).toEqual({
      pageCode: "PRODUCTION_OVERVIEW", widgetCode: "PO_BAR", widgetDefinitionId: "w-1",
    });
    // The captured options are preserved, not replaced.
    expect((sent.options as Record<string, unknown>).maxRows).toBe(100);
  });

  it("does not mutate the captured options when opting in", async () => {
    await executeWithEvidence(A, vi.fn().mockResolvedValue({ rows: [] }), vi.fn());
    expect(A.options).not.toHaveProperty("includeExecutionEvidence");
  });

  it("routes an expression widget to the expression executor", async () => {
    const expression = vi.fn().mockResolvedValue({ rows: [] });
    const catalogue = vi.fn();

    await executeWithEvidence(
      { ...A, kind: "expression", expression: "sum(x)" }, catalogue, expression,
    );

    expect(catalogue).not.toHaveBeenCalled();
    const sent = expression.mock.calls[0][0] as Record<string, unknown>;
    expect(sent.expression).toBe("sum(x)");
    expect((sent.options as Record<string, unknown>).includeExecutionEvidence).toBe(true);
  });
});
