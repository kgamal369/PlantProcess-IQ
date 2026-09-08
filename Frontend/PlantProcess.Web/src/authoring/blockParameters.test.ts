import { describe, expect, it } from "vitest";

import {
  ARITHMETIC_OPERATORS, COMPARISON_OPERATORS, LOGIC_OPERATORS,
  arithmeticContract, boundedWhileContract, comparisonContract, conditionalContract,
  describePortType, describeSignature, forEachContract, logicContract,
  parameterValueProblem, repeatContract,
} from "./blockParameters";
import { BLOCK_REGISTRY, contractForKind } from "./blockRegistry";
import { blockProblem, FLOW_IN, FLOW_OUT, type BoardEdge, type BoardNode } from "./graphSemantics";
import { familySignature } from "./blockSemantics";

function source(): BoardNode {
  return { id: "source_a", kind: "dataset", data: { table: "source_a", source: "source_a", columns: [] } };
}
function wire(from: string, to: string): BoardEdge {
  return { source: from, target: to, sourceHandle: FLOW_OUT, targetHandle: FLOW_IN };
}
function node(kind: BoardNode["kind"], data: Record<string, unknown> = {}): BoardNode {
  return { id: kind + "-1", kind, data: { title: kind, ...data } };
}
function problemOf(n: BoardNode): string | null {
  const s = source();
  return blockProblem(n, [s, n], [wire(s.id, n.id)]);
}

describe("T-242 typed ports are a real contract", () => {
  it("C242-54 every executable family declares typed input and output ports", () => {
    let counted = 0;
    for (const block of BLOCK_REGISTRY) {
      if (!block.implemented || block.contract === undefined) { continue; }
      counted++;
      const signature = block.contract.ports({});
      expect(signature.output, block.id).not.toBeNull();
      for (const p of signature.inputs) {
        expect(p.id.length, block.id).toBeGreaterThan(0);
        expect(p.label.length, block.id).toBeGreaterThan(0);
      }
    }
    expect(counted).toBe(7);
  });

  it("C242-55 the declared port types are the real ones, not a description", () => {
    const add = arithmeticContract.ports({});
    expect(add.inputs.map((p) => p.type)).toEqual([{ concrete: "number" }, { concrete: "number" }]);
    expect(add.output?.type).toEqual({ concrete: "number" });

    const gt = comparisonContract.ports({});
    expect(gt.output?.type).toEqual({ concrete: "boolean" });
    expect(gt.inputs.every((p) => "variable" in p.type)).toBe(true);

    const and = logicContract.ports({ operator: "and" });
    expect(and.inputs.map((p) => p.type)).toEqual([{ concrete: "boolean" }, { concrete: "boolean" }]);
    expect(and.output?.type).toEqual({ concrete: "boolean" });

    expect(conditionalContract.ports({}).inputs[0].type).toEqual({ concrete: "boolean" });
    expect(boundedWhileContract.ports({}).inputs[0].type).toEqual({ concrete: "boolean" });
  });

  it("C242-56 ports are a function of the declared parameters", () => {
    // "not" takes one operand and "and" takes two. That fact is stated once,
    // in the contract, and everything else reads it from there.
    expect(logicContract.ports({ operator: "not" }).inputs.length).toBe(1);
    expect(logicContract.ports({ operator: "and" }).inputs.length).toBe(2);
    expect(logicContract.ports({ operator: "or" }).inputs.length).toBe(2);
  });

  it("C242-57 the human sentence is DERIVED from the typed ports", () => {
    expect(describeSignature(arithmeticContract.ports({}))).toBe("two numbers and produces a number");
    expect(describeSignature(comparisonContract.ports({}))).toBe("values of the same kind and produces a boolean");
    expect(describeSignature(logicContract.ports({ operator: "not" }))).toBe("one boolean and produces a boolean");
    expect(describeSignature(repeatContract.ports({}))).toBe("no data input and produces a value");
    expect(describeSignature(forEachContract.ports({}))).toBe("one dataset and produces a value");
    expect(describePortType({ concrete: "key" })).toBe("dataset");
  });

  it("C242-58 the evaluator's own sentence comes from the same ports", () => {
    // One authority. If the contract changed and the prose did not, this fails.
    expect(familySignature(node("arithmetic"))).toBe(describeSignature(arithmeticContract.ports({})));
    expect(familySignature(node("logic", { operator: "not" })))
      .toBe(describeSignature(logicContract.ports({ operator: "not" })));
  });
});

describe("T-242 board validity reads the declared parameter contract", () => {
  it("C242-59 an operator that was never chosen makes the block invalid", () => {
    expect(problemOf(node("arithmetic"))).toContain("no declared arithmetic operator");
    expect(problemOf(node("comparison"))).toContain("no declared comparison operator");
    expect(problemOf(node("logic"))).toContain("no declared logic operator");
  });

  it("C242-60 a declared operator makes the same block valid", () => {
    expect(problemOf(node("arithmetic", { operator: "add" }))).toBeNull();
    expect(problemOf(node("comparison", { operator: "equals" }))).toBeNull();
    expect(problemOf(node("logic", { operator: "not" }))).toBeNull();
    expect(problemOf(node("conditional"))).toBeNull();
    expect(problemOf(node("for-each"))).toBeNull();
  });

  it("C242-61 the loop law is now a declared parameter, and still absolute", () => {
    expect(problemOf(node("repeat-n"))).toContain("no finite iteration bound");
    expect(problemOf(node("repeat-n", { maxIterations: 3 }))).toBeNull();
    expect(problemOf(node("while-bounded", { budgetMs: 1000 }))).toContain("no finite iteration bound");
    expect(problemOf(node("while-bounded", { maxIterations: 5 }))).toContain("no runtime budget");
    expect(problemOf(node("while-bounded", { maxIterations: 5, budgetMs: 1000 }))).toBeNull();
  });

  it("C242-62 a zero, negative or fractional count is not a bound", () => {
    for (const bad of [0, -1, 2.5, "", "abc"]) {
      expect(problemOf(node("repeat-n", { maxIterations: bad })), String(bad))
        .toContain("no finite iteration bound");
    }
  });

  it("C242-63 an operator outside the declared set is refused, never coerced", () => {
    expect(problemOf(node("arithmetic", { operator: "power" }))).toContain("no declared arithmetic operator");
    expect(parameterValueProblem(
      { key: "operator", label: "Operation", control: { kind: "choice", options: ARITHMETIC_OPERATORS }, missing: "nope" },
      "power",
    )).toBe("nope");
  });

  it("C242-64 the problem sentence names the block it belongs to", () => {
    const n = node("arithmetic");
    n.data.title = "Weight per unit";
    expect(problemOf(n)).toContain("Weight per unit");
  });

  it("C242-65 every declared operator option is accepted by its own schema", () => {
    const sets: Array<[readonly string[], ReturnType<typeof contractForKind>]> = [
      [ARITHMETIC_OPERATORS, contractForKind("arithmetic")],
      [COMPARISON_OPERATORS, contractForKind("comparison")],
      [LOGIC_OPERATORS, contractForKind("logic")],
    ];
    for (const [options, contract] of sets) {
      if (contract === null) { throw new Error("a declared family has no contract"); }
      const spec = contract.parameters[0];
      for (const option of options) {
        expect(parameterValueProblem(spec, option), option).toBeNull();
      }
    }
  });
});

describe("T-242 the schema module is not a second registry", () => {
  it("C242-66 relational families declare no contract, and every executable one does", () => {
    for (const block of BLOCK_REGISTRY) {
      if (!block.implemented) { continue; }
      const isRelational = ["filter", "select", "derived"].indexOf(block.boardKind) >= 0;
      expect(block.contract === undefined, block.id).toBe(isRelational);
    }
  });

  it("C242-67 a kind resolves to its contract through the catalogue alone", () => {
    expect(contractForKind("arithmetic")).toBe(arithmeticContract);
    expect(contractForKind("while-bounded")).toBe(boundedWhileContract);
    // Relational and unimplemented kinds have none, and say so rather than
    // returning an empty contract that would silently validate anything.
    expect(contractForKind("filter")).toBeNull();
    expect(contractForKind("aggregate")).toBeNull();
  });
});