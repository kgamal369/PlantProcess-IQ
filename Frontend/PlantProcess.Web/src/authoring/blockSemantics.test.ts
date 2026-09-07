import { describe, expect, it } from "vitest";

import {
  ARITHMETIC_OPERATORS,
  COMPARISON_OPERATORS,
  LOGIC_OPERATORS,
  booleanValue,
  evaluateBlock,
  numberValue,
  runForEach,
  runRepeatN,
  runWhileBounded,
  textValue,
  validateBlockPorts,
  type BlockValue,
  type LoopEnvironment,
} from "./blockSemantics";
import type { BoardNode } from "./graphSemantics";

function block(kind: BoardNode["kind"], data: Record<string, unknown> = {}): BoardNode {
  return { id: kind + "-1", kind, data: { title: kind, ...data } };
}

function value(outcome: ReturnType<typeof evaluateBlock>): BlockValue {
  if (!outcome.ok) { throw new Error("expected a value, got refusal: " + outcome.refusal.message); }
  return outcome.value;
}

function refusalOf(outcome: ReturnType<typeof evaluateBlock>) {
  if (outcome.ok) { throw new Error("expected a refusal, got a value"); }
  return outcome.refusal;
}

/**
 * A clock and a cancellation signal the test drives by hand. Nothing here
 * waits on real time, and the two are counted SEPARATELY: a loop with no
 * declared budget never reads the clock at all, so cancellation that was
 * keyed off the clock's call count could never fire.
 */
function clock(ticks: readonly number[], cancelAfter = -1): LoopEnvironment {
  let nowCalls = 0;
  let cancelCalls = 0;
  return {
    now: () => ticks[Math.min(nowCalls++, ticks.length - 1)],
    cancelled: () => {
      const seen = cancelCalls++;
      return cancelAfter >= 0 && seen >= cancelAfter;
    },
  };
}

describe("T-242 block algebra: known answers", () => {
  it("C242-10 arithmetic gives the known answer for every declared operator", () => {
    const cases: Array<[string, number, number, number]> = [
      ["add", 7, 5, 12],
      ["subtract", 7, 5, 2],
      ["multiply", 7, 5, 35],
      ["divide", 35, 5, 7],
    ];
    for (const [operator, left, right, expected] of cases) {
      const node = block("arithmetic", { operator });
      const outcome = evaluateBlock(node, [numberValue(left), numberValue(right)]);
      expect(value(outcome), operator).toEqual({ type: "number", value: expected });
    }
    // Every declared operator is covered above. If one is added without a
    // known answer, this count fails before the gap reaches a board.
    expect(cases.length).toBe(ARITHMETIC_OPERATORS.length);
  });

  it("C242-11 division by zero refuses rather than producing Infinity", () => {
    const outcome = evaluateBlock(block("arithmetic", { operator: "divide" }), [numberValue(1), numberValue(0)]);
    expect(refusalOf(outcome).code).toBe("BLOCK_DIVIDE_BY_ZERO");
    expect(refusalOf(outcome).blockId).toBe("arithmetic-1");
  });

  it("C242-12 comparison gives the known answer for every declared operator", () => {
    const cases: Array<[string, number, number, boolean]> = [
      ["equals", 5, 5, true],
      ["not-equals", 5, 5, false],
      ["greater-than", 7, 5, true],
      ["greater-or-equal", 5, 5, true],
      ["less-than", 7, 5, false],
      ["less-or-equal", 5, 5, true],
    ];
    for (const [operator, left, right, expected] of cases) {
      const outcome = evaluateBlock(block("comparison", { operator }), [numberValue(left), numberValue(right)]);
      expect(value(outcome), operator).toEqual({ type: "boolean", value: expected });
    }
    expect(cases.length).toBe(COMPARISON_OPERATORS.length);
  });

  it("C242-13 Boolean logic reproduces the full truth table", () => {
    const table: Array<[string, boolean[], boolean]> = [
      ["and", [true, true], true],
      ["and", [true, false], false],
      ["and", [false, true], false],
      ["and", [false, false], false],
      ["or", [true, true], true],
      ["or", [true, false], true],
      ["or", [false, true], true],
      ["or", [false, false], false],
      ["not", [true], false],
      ["not", [false], true],
    ];
    for (const [operator, inputs, expected] of table) {
      const outcome = evaluateBlock(block("logic", { operator }), inputs.map(booleanValue));
      expect(value(outcome), operator + " " + JSON.stringify(inputs)).toEqual({ type: "boolean", value: expected });
    }
    expect(new Set(table.map((row) => row[0])).size).toBe(LOGIC_OPERATORS.length);
  });

  it("C242-14 IF/ELSE returns the branch the condition selects", () => {
    const node = block("conditional");
    expect(value(evaluateBlock(node, [booleanValue(true), numberValue(10), numberValue(20)])))
      .toEqual({ type: "number", value: 10 });
    expect(value(evaluateBlock(node, [booleanValue(false), numberValue(10), numberValue(20)])))
      .toEqual({ type: "number", value: 20 });
  });
});

describe("T-242 block algebra: type and port negative controls", () => {
  it("C242-15 arithmetic refuses a non-numeric input before computing anything", () => {
    const outcome = evaluateBlock(block("arithmetic", { operator: "add" }), [numberValue(1), textValue("2")]);
    const refusal = refusalOf(outcome);
    expect(refusal.code).toBe("BLOCK_TYPE");
    expect(refusal.message).toContain("text");
  });

  it("C242-16 comparison refuses two different kinds of value", () => {
    const outcome = evaluateBlock(block("comparison", { operator: "equals" }), [numberValue(1), textValue("1")]);
    expect(refusalOf(outcome).code).toBe("BLOCK_TYPE");
  });

  it("C242-17 logic refuses a non-boolean input", () => {
    const outcome = evaluateBlock(block("logic", { operator: "and" }), [booleanValue(true), numberValue(1)]);
    expect(refusalOf(outcome).code).toBe("BLOCK_TYPE");
  });

  it("C242-18 IF/ELSE refuses branches that return different kinds of value", () => {
    const outcome = evaluateBlock(block("conditional"), [booleanValue(true), numberValue(1), textValue("a")]);
    const refusal = refusalOf(outcome);
    expect(refusal.code).toBe("BLOCK_TYPE");
    expect(refusal.message).toContain("branch");
  });

  it("C242-19 an undeclared operator refuses instead of defaulting to one", () => {
    for (const node of [block("arithmetic"), block("comparison"), block("logic")]) {
      expect(refusalOf(evaluateBlock(node, [numberValue(1), numberValue(2)])).code, node.kind)
        .toBe("BLOCK_OPERATOR");
    }
  });

  it("C242-20 wrong arity refuses", () => {
    const outcome = evaluateBlock(block("arithmetic", { operator: "add" }), [numberValue(1)]);
    expect(refusalOf(outcome).code).toBe("BLOCK_ARITY");
  });

  it("C242-21 incompatible ports refuse BEFORE execution", () => {
    // validateBlockPorts is reachable on its own, so the board can refuse a
    // wire at the moment it is drawn rather than at the moment it is run.
    const refusal = validateBlockPorts(block("arithmetic", { operator: "add" }), ["number", "text"]);
    expect(refusal).not.toBeNull();
    expect(refusal?.code).toBe("BLOCK_TYPE");
    expect(validateBlockPorts(block("arithmetic", { operator: "add" }), ["number", "number"])).toBeNull();
  });

  it("C242-22 a flow connection is never a value input", () => {
    const refusal = validateBlockPorts(block("logic", { operator: "and" }), ["flow", "boolean"]);
    expect(refusal?.code).toBe("BLOCK_TYPE");
  });
});

describe("T-242 governed families are refused, not guessed", () => {
  it("C242-23 aggregate and window refuse: their semantics are declared elsewhere", () => {
    for (const kind of ["aggregate", "window"] as const) {
      const refusal = refusalOf(evaluateBlock(block(kind), [numberValue(1)]));
      expect(refusal.code, kind).toBe("BLOCK_NOT_EVALUABLE");
      expect(refusal.message, kind).toContain("governed");
    }
  });

  it("C242-24 a relational block is not block algebra", () => {
    const refusal = validateBlockPorts(block("filter"), ["number"]);
    expect(refusal?.code).toBe("BLOCK_NOT_EVALUABLE");
  });
});

describe("T-242 bounded loops", () => {
  const sum = (accumulator: BlockValue, item: BlockValue): BlockValue =>
    numberValue((accumulator.value as number) + (item.value as number));

  it("C242-25 ForEach walks its collection and stops on it", () => {
    const outcome = runForEach(
      block("for-each"),
      [numberValue(1), numberValue(2), numberValue(3)],
      numberValue(0),
      sum,
    );
    expect(outcome.ok && outcome.value).toEqual({ type: "number", value: 6 });
    expect(outcome.ok && outcome.iterations).toBe(3);
    expect(outcome.ok && outcome.stoppedBy).toBe("collection");
  });

  it("C242-26 RepeatN runs exactly the declared count", () => {
    const outcome = runRepeatN(
      block("repeat-n", { maxIterations: 4 }),
      numberValue(0),
      (accumulator) => numberValue((accumulator.value as number) + 10),
    );
    expect(outcome.ok && outcome.value).toEqual({ type: "number", value: 40 });
    expect(outcome.ok && outcome.iterations).toBe(4);
    expect(outcome.ok && outcome.stoppedBy).toBe("bound");
  });

  it("C242-27 WhileBounded stops on its condition when the condition comes first", () => {
    const outcome = runWhileBounded(
      block("while-bounded", { maxIterations: 100, budgetMs: 1000 }),
      (accumulator) => (accumulator.value as number) < 3,
      numberValue(0),
      (accumulator) => numberValue((accumulator.value as number) + 1),
    );
    expect(outcome.ok && outcome.value).toEqual({ type: "number", value: 3 });
    expect(outcome.ok && outcome.iterations).toBe(3);
    expect(outcome.ok && outcome.stoppedBy).toBe("condition");
  });

  it("C242-28 WhileBounded stops on its bound when the condition never turns false", () => {
    const outcome = runWhileBounded(
      block("while-bounded", { maxIterations: 5, budgetMs: 1000 }),
      () => true,
      numberValue(0),
      (accumulator) => numberValue((accumulator.value as number) + 1),
    );
    expect(outcome.ok && outcome.iterations).toBe(5);
    expect(outcome.ok && outcome.stoppedBy).toBe("bound");
  });

  it("C242-29 WhileBounded stops on its budget, and says so", () => {
    // The clock jumps past the budget on the third reading. No real time passes.
    const outcome = runWhileBounded(
      block("while-bounded", { maxIterations: 1000, budgetMs: 50 }),
      () => true,
      numberValue(0),
      (accumulator) => numberValue((accumulator.value as number) + 1),
      clock([0, 10, 20, 500]),
    );
    expect(outcome.ok && outcome.stoppedBy).toBe("budget");
    expect(outcome.ok && outcome.iterations).toBeLessThan(1000);
  });

  it("C242-30 cancellation stops a loop and is reported as cancellation", () => {
    const outcome = runRepeatN(
      block("repeat-n", { maxIterations: 100 }),
      numberValue(0),
      (accumulator) => numberValue((accumulator.value as number) + 1),
      clock([0], 2),
    );
    expect(outcome.ok && outcome.stoppedBy).toBe("cancelled");
    expect(outcome.ok && outcome.iterations).toBe(2);
  });

  it("C242-31 a loop with no declared bound refuses before it runs", () => {
    const outcome = runWhileBounded(
      block("while-bounded", { budgetMs: 1000 }),
      () => true,
      numberValue(0),
      (accumulator) => accumulator,
    );
    expect(outcome.ok).toBe(false);
    expect(!outcome.ok && outcome.refusal.code).toBe("BLOCK_LOOP_BOUND");
  });

  it("C242-32 a bounded while with no budget refuses before it runs", () => {
    const outcome = runWhileBounded(
      block("while-bounded", { maxIterations: 10 }),
      () => true,
      numberValue(0),
      (accumulator) => accumulator,
    );
    expect(!outcome.ok && outcome.refusal.code).toBe("BLOCK_LOOP_BOUND");
  });

  it("C242-33 the same inputs give the same answer on every run", () => {
    const run = () => runWhileBounded(
      block("while-bounded", { maxIterations: 20, budgetMs: 1000 }),
      (accumulator) => (accumulator.value as number) < 7,
      numberValue(0),
      (accumulator) => numberValue((accumulator.value as number) + 1),
    );
    const first = run();
    for (let attempt = 0; attempt < 5; attempt++) {
      expect(run()).toEqual(first);
    }
  });
});