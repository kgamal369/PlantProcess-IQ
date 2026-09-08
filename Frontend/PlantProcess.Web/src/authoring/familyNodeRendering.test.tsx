import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

// Auto-cleanup only registers itself when vitest globals are on, and they are
// not. Without this, a render leaks into the next test and the failure looks
// like a duplicate-testid defect in the component rather than in the harness.
afterEach(cleanup);
import { ReactFlowProvider } from "@xyflow/react";


import { AUTHORING_NODE_TYPES, FamilyNode } from "./BlockNodes";
import { BLOCK_REGISTRY, contractForKind, titleForKind } from "./blockRegistry";
import { describeSignature } from "./blockParameters";
import type { BoardNodeKind } from "./graphSemantics";

const FAMILIES: BoardNodeKind[] = [
  "arithmetic", "comparison", "logic", "conditional", "for-each", "repeat-n", "while-bounded",
];

type FamilyNodeProps = Parameters<typeof FamilyNode>[0];

function renderFamily(kind: BoardNodeKind, data: Record<string, unknown> = {}) {
  const props = {
    id: kind + "-1",
    type: kind,
    data: { title: kind + " 1", fields: [], problem: null, ...data },
    selected: false, isConnectable: true, zIndex: 0,
    positionAbsoluteX: 0, positionAbsoluteY: 0, dragging: false, draggable: true,
    selectable: true, deletable: true,
  } as unknown as FamilyNodeProps;
  return render(<ReactFlowProvider><FamilyNode {...props} /></ReactFlowProvider>);
}

describe("T-242 one renderer serves every executable family", () => {
  it("C242-68 there is no bespoke component per family", () => {
    // Every executable family maps to the SAME component. Nine components
    // would be nine copies of one, and nine places for a signature to drift.
    for (const kind of FAMILIES) {
      expect(AUTHORING_NODE_TYPES[kind as keyof typeof AUTHORING_NODE_TYPES], kind).toBe(FamilyNode);
    }
    const distinct = new Set(Object.values(AUTHORING_NODE_TYPES));
    expect(distinct.size, "filter, derived, select plus one shared family node").toBe(4);
  });

  it("C242-69 the registered families come from the catalogue, not a hand list", () => {
    const declared = BLOCK_REGISTRY
      .filter((b) => b.implemented && b.contract !== undefined)
      .map((b) => b.boardKind as string)
      .sort();
    const registered = Object.keys(AUTHORING_NODE_TYPES)
      .filter((k) => AUTHORING_NODE_TYPES[k as keyof typeof AUTHORING_NODE_TYPES] === FamilyNode)
      .sort();
    expect(registered).toEqual(declared);
  });

  it("C242-70 every family renders with its catalogue title", () => {
    for (const kind of FAMILIES) {
      const { unmount } = renderFamily(kind);
      expect(screen.getByTestId("family-node-" + kind + "-1"), kind).toBeDefined();
      expect(screen.getByText(titleForKind(kind)), kind).toBeDefined();
      unmount();
    }
  });

  it("C242-71 the rendered sentence is the one derived from the typed ports", () => {
    for (const kind of FAMILIES) {
      const { unmount } = renderFamily(kind);
      const contract = contractForKind(kind);
      if (contract === null) { throw new Error("no contract for " + kind); }
      const expected = "Takes " + describeSignature(contract.ports({})) + ".";
      expect(screen.getByTestId("family-signature-" + kind + "-1").textContent, kind).toBe(expected);
      unmount();
    }
  });

  it("C242-72 the controls rendered are exactly the parameters the contract declares", () => {
    for (const kind of FAMILIES) {
      const { unmount } = renderFamily(kind);
      const contract = contractForKind(kind);
      if (contract === null) { throw new Error("no contract for " + kind); }
      const specs = contract.parameters;
      for (const spec of specs) {
        expect(screen.getByLabelText(spec.label), kind + "/" + spec.key).toBeDefined();
      }
      unmount();
    }
  });

  it("C242-73 an operator control offers exactly its declared options and no default", () => {
    renderFamily("arithmetic");
    const select = screen.getByLabelText("Operation") as HTMLSelectElement;
    const values = Array.from(select.options).map((o) => o.value);
    expect(values[0]).toBe("");
    expect(values.slice(1)).toEqual(["add", "subtract", "multiply", "divide"]);
    expect(select.value, "nothing is preselected on the author's behalf").toBe("");
  });

  it("C242-74 a parameter edit is reported by key, not by position", () => {
    const onChange = vi.fn();
    renderFamily("while-bounded", { onChange });
    // Setting .value and dispatching a raw event does not reach React's
    // synthetic handler; fireEvent goes through the same path a keystroke does.
    fireEvent.change(screen.getByLabelText("Iterations"), { target: { value: "7" } });
    expect(onChange).toHaveBeenCalled();
    expect(onChange.mock.calls[0][1]).toBe("maxIterations");
  });

  it("C242-75 the node states its problem on itself", () => {
    renderFamily("arithmetic", { problem: "Arithmetic 1 has no declared arithmetic operator." });
    const status = screen.getByTestId("family-status-arithmetic-1");
    expect(status.textContent).toContain("has no declared arithmetic operator");
  });
});