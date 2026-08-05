// PPIQ T-033 items 2 to 4. The board nodes under test.
//
// The React Flow Handle is mocked: it reads the board store, which needs a
// provider these tests deliberately do not stand up. What the mock removes -
// that the ports EXIST and carry the flow vocabulary - is asserted separately
// by reading the source, so nothing is left unproven by the mock.

import { readFileSync } from "node:fs";
import { join } from "node:path";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { FILTER_OPERATORS, MATH_OPERATORS } from "./operatorContract";
import type { BoardField } from "./graphSemantics";
import { DerivedNode, FilterNode, SelectNode } from "./BlockNodes";

vi.mock("@xyflow/react", () => ({
  Handle: () => null,
  Position: { Left: "left", Right: "right", Top: "top", Bottom: "bottom" },
}));

const fields: BoardField[] = [
  { originKind: "physical", originTable: "alpha", originColumn: "alpha_id", sqlType: "text", displayName: "alpha.alpha_id", isKeyCandidate: true },
  { originKind: "physical", originTable: "alpha", originColumn: "weight_kg", sqlType: "numeric", displayName: "alpha.weight_kg" },
];

const derivedField: BoardField = {
  originKind: "derived", originTable: "", originColumn: "", sqlType: "numeric", displayName: "net_kg",
};

// NodeProps carries board geometry these components never read - position,
// zIndex, drag state. Rather than invent values the components ignore, each
// node is viewed through the two props it actually uses. The cast is stated
// once, here, instead of at every render site.
type NodeView = (props: { id: string; data: unknown }) => ReturnType<typeof FilterNode>;
const Filter = FilterNode as unknown as NodeView;
const Derived = DerivedNode as unknown as NodeView;
const Select = SelectNode as unknown as NodeView;

describe("filter node", () => {
  it("offers the server whitelist and nothing else", () => {
    render(<Filter id="f1" data={{ title: "Filter", fields, problem: null, fieldRef: "", op: "", value: "" }} />);
    const ops = screen.getByLabelText("Comparison");
    const offered = Array.from(ops.querySelectorAll("option")).map((o) => o.getAttribute("value")).filter((v) => v !== "");
    expect(offered).toEqual(FILTER_OPERATORS.slice());
  });

  it("feeds the column dropdown from the upstream lineage, never free text", () => {
    render(<Filter id="f1" data={{ title: "Filter", fields, problem: null, fieldRef: "", op: "", value: "" }} />);
    const col = screen.getByLabelText("Filter column");
    const offered = Array.from(col.querySelectorAll("option")).map((o) => o.getAttribute("value")).filter((v) => v !== "");
    expect(offered).toEqual(["alpha.alpha_id", "alpha.weight_kg"]);
  });

  it("lists a derived field truthfully and refuses to let it be chosen", () => {
    render(<Filter id="f1" data={{ title: "Filter", fields: fields.concat([derivedField]), problem: null, fieldRef: "", op: "", value: "" }} />);
    const col = screen.getByLabelText("Filter column");
    const options = Array.from(col.querySelectorAll("option"));
    const derivedOption = options.filter((o) => o.getAttribute("value") === "net_kg")[0];
    expect(derivedOption).toBeTruthy();
    expect(derivedOption.hasAttribute("disabled")).toBe(true);
    expect(derivedOption.textContent).toContain("no table to address");
  });

  it("removes the value field for a unary operator rather than disabling it", () => {
    const { rerender } = render(<Filter id="f1" data={{ title: "Filter", fields, problem: null, fieldRef: "alpha.weight_kg", op: ">", value: "1" }} />);
    expect(screen.queryByLabelText("Value")).not.toBeNull();
    rerender(<Filter id="f1" data={{ title: "Filter", fields, problem: null, fieldRef: "alpha.weight_kg", op: "IS NULL", value: "" }} />);
    expect(screen.queryByLabelText("Value")).toBeNull();
  });

  it("shows the status badge and the sentence on the node itself", () => {
    render(<Filter id="f1" data={{ title: "Filter", fields, problem: "Filter needs a value for operator >.", fieldRef: "alpha.weight_kg", op: ">", value: "" }} />);
    expect(screen.getByTestId("filter-status-f1")).toHaveTextContent("Error");
    expect(screen.getByTestId("filter-status-f1")).toHaveTextContent("needs a value for operator");
  });

  it("reports every edit to the board, which owns the state", async () => {
    const onChange = vi.fn();
    render(<Filter id="f1" data={{ title: "Filter", fields, problem: null, fieldRef: "", op: "", value: "", onChange }} />);
    await userEvent.selectOptions(screen.getByLabelText("Filter column"), "alpha.weight_kg");
    expect(onChange).toHaveBeenCalledWith("f1", "fieldRef", "alpha.weight_kg");
  });
});

describe("derived node", () => {
  it("offers only the four arithmetic operators the server compiles", () => {
    render(<Derived id="d1" data={{ title: "Derived column", fields, problem: null, alias: "", leftRef: "", op: "", rightRef: "", constant: "" }} />);
    const ops = screen.getByLabelText("Operation");
    const offered = Array.from(ops.querySelectorAll("option")).map((o) => o.getAttribute("value")).filter((v) => v !== "");
    expect(offered).toEqual(MATH_OPERATORS.slice());
  });

  it("shows a number field only while no second column is chosen", () => {
    const { rerender } = render(<Derived id="d1" data={{ title: "Derived column", fields, problem: null, alias: "r", leftRef: "alpha.weight_kg", op: "*", rightRef: "", constant: "2" }} />);
    expect(screen.queryByLabelText("Number")).not.toBeNull();
    rerender(<Derived id="d1" data={{ title: "Derived column", fields, problem: null, alias: "r", leftRef: "alpha.weight_kg", op: "*", rightRef: "alpha.alpha_id", constant: "" }} />);
    expect(screen.queryByLabelText("Number")).toBeNull();
  });
});

describe("select node", () => {
  it("renders one toggle per upstream field and marks the chosen ones", () => {
    render(<Select id="s1" data={{ title: "Select columns", fields, problem: null, chosen: ["alpha.weight_kg"] }} />);
    expect(screen.getByRole("button", { name: "alpha.alpha_id" })).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByRole("button", { name: "alpha.weight_kg" })).toHaveAttribute("aria-pressed", "true");
  });

  it("says what to do when nothing is upstream, instead of showing an empty box", () => {
    render(<Select id="s1" data={{ title: "Select columns", fields: [], problem: null, chosen: [] }} />);
    expect(screen.getByTestId("select-node-s1")).toHaveTextContent("Wire a dataset into this block");
  });

  it("reports a toggle to the board", async () => {
    const onToggle = vi.fn();
    render(<Select id="s1" data={{ title: "Select columns", fields, problem: null, chosen: [], onToggle }} />);
    await userEvent.click(screen.getByRole("button", { name: "alpha.alpha_id" }));
    expect(onToggle).toHaveBeenCalledWith("s1", "alpha.alpha_id");
  });
});

describe("ports, which the Handle mock cannot show", () => {
  const source = readFileSync(join(process.cwd(), "src/authoring/BlockNodes.tsx"), "utf8");

  it("every block declares one flow input and one flow output from the shared vocabulary", () => {
    // The import list grows as the module grows, so this names the two
    // identifiers and the module rather than pinning the exact line.
    expect(source).toMatch(/import \{[^}]*FLOW_IN[^}]*FLOW_OUT[^}]*\} from "\.\/graphSemantics";/);
    expect((source.match(/id=\{FLOW_IN\}/g) ?? []).length).toBe(3);
    expect((source.match(/id=\{FLOW_OUT\}/g) ?? []).length).toBe(3);
  });

  it("declares no handle with a literal string, which would drift from the semantics", () => {
    expect(source).not.toContain('id="flow:');
  });
});