// PPIQ T-242 Stage 2. THE BLOCK ALGEBRA.
//
// WHAT THIS IS. A pure, deterministic evaluator for the block families whose
// meaning is algebra rather than data access: arithmetic, comparison, Boolean
// logic, IF/ELSE, and the three bounded loops. Given the same inputs it gives
// the same answer, every time, with no clock, no network and no database. That
// is what makes a known-answer test possible at all.
//
// WHAT THIS IS NOT, stated because the boundary is the whole point. This is
// NOT the production Canvas runtime. Production execution is the governed job
// path, and that arrives with the compile step. This module therefore never:
//
//   - queries a database or reaches the network;
//   - executes or schedules a job;
//   - persists a run outcome;
//   - implements an analytics kernel;
//   - computes a governed aggregate or a window.
//
// The last two are not omissions. An aggregate's meaning is DECLARED by the
// canonical aggregation authority, and a module that quietly computed an
// average would be inventing a semantic the plant never agreed to. Ask this
// evaluator for an aggregate and it refuses, by name, and says where the
// answer lives. That refusal is the feature.
//
// TIME IS INJECTED. A loop that consults a wall clock is not deterministic and
// cannot be certified. Every bounded loop takes its clock and its cancellation
// signal from the caller, so a test can drive a budget to exhaustion in zero
// real milliseconds and get the same answer on every machine.

import { portsCompatible, type PortType } from "@/canvas/ports";
import {
  ARITHMETIC_OPERATORS as ARITHMETIC, COMPARISON_OPERATORS as COMPARISON,
  LOGIC_OPERATORS as LOGIC, describeSignature,
  type ArithmeticOperator as ArithmeticOp, type ComparisonOperator as ComparisonOp,
  type LogicOperator as LogicOp,
} from "./blockParameters";
import { contractForKind } from "./blockRegistry";
import {
  isComputeBoardNodeKind,
  isLoopBoardNodeKind,
  loopBoundProblem,
  titleOf,
  type BoardNode,
} from "./graphSemantics";

// ============================================================================
// VALUES
// ============================================================================

/**
 * A value carries its own type. The alternative - inferring the type from the
 * JavaScript runtime shape at the moment of use - is exactly how a text "12"
 * ends up silently added to a number, so the type travels with the value and
 * is checked before anything is computed.
 *
 * Dates are deliberately absent. A date literal has no unambiguous meaning
 * without a declared timezone and calendar, and this module refuses to guess
 * one. Date-typed COLUMNS still flow through the relational representation,
 * where the server owns their semantics.
 */
export type BlockValue =
  | { readonly type: "number"; readonly value: number }
  | { readonly type: "text"; readonly value: string }
  | { readonly type: "boolean"; readonly value: boolean };

export function numberValue(value: number): BlockValue {
  return { type: "number", value };
}
export function textValue(value: string): BlockValue {
  return { type: "text", value };
}
export function booleanValue(value: boolean): BlockValue {
  return { type: "boolean", value };
}

export function portTypeOf(value: BlockValue): PortType {
  return value.type;
}

// ============================================================================
// REFUSALS
// ============================================================================

/**
 * Evaluator diagnostics. These are NOT a new product refusal family: the
 * message keeps the board's own sentence shape - which block, what rule, what
 * would fix it - and the code exists so a test can assert on the REASON rather
 * than on wording that may legitimately be rewritten.
 */
export type BlockRefusalCode =
  | "BLOCK_ARITY"
  | "BLOCK_OPERATOR"
  | "BLOCK_TYPE"
  | "BLOCK_DIVIDE_BY_ZERO"
  | "BLOCK_LOOP_BOUND"
  | "BLOCK_NOT_EVALUABLE";

export interface BlockRefusal {
  readonly code: BlockRefusalCode;
  readonly blockId: string;
  readonly kind: string;
  readonly message: string;
}

export type BlockOutcome =
  | { readonly ok: true; readonly value: BlockValue }
  | { readonly ok: false; readonly refusal: BlockRefusal };

function refuse(node: BoardNode, code: BlockRefusalCode, message: string): BlockOutcome {
  return {
    ok: false,
    refusal: { code, blockId: node.id, kind: node.kind, message: titleOf(node) + " " + message },
  };
}

// ============================================================================
// OPERATORS
//
// Operators are block CONFIGURATION, not graph kinds. This is where "add" and
// "greater than" live, and the reason BoardNodeKind has nine new members
// rather than twenty.
// ============================================================================

// T-242 Stage 4. The operator lists now live with the parameter schemas, and
// are re-exported here so every existing importer keeps working unchanged.
export {
  ARITHMETIC_OPERATORS, COMPARISON_OPERATORS, LOGIC_OPERATORS,
} from "./blockParameters";
export type {
  ArithmeticOperator, ComparisonOperator, LogicOperator,
} from "./blockParameters";

/**
 * THE SIGNATURE IS DERIVED, NEVER WRITTEN TWICE.
 *
 * Stage 2 kept a hand-written sentence per family - "two numbers and produces
 * a number" - beside the real port checks. Two authorities for one fact, and
 * the prose would have drifted the first time a signature changed. The
 * sentence is now assembled from the SAME typed ports that validation reads,
 * so it cannot describe something the contract does not do.
 */
export function familySignature(node: BoardNode): string {
  const contract = contractForKind(node.kind);
  return contract ? describeSignature(contract.ports(node.data)) : "no declared port signature";
}

export function operatorOf(node: BoardNode): string {
  const raw = node.data.operator;
  return typeof raw === "string" ? raw.trim() : "";
}

/**
 * Arity is READ FROM THE PORTS, not restated. A logic block configured as
 * "not" has one input because its contract says so, and there is no second
 * place that fact can be written down differently.
 */
function arityOf(node: BoardNode): number {
  const contract = contractForKind(node.kind);
  return contract ? contract.ports(node.data).inputs.length : 0;
}

// ============================================================================
// PORT VALIDATION - REFUSAL HAPPENS BEFORE EVALUATION
//
// Nothing is computed until the wiring is proven sound. A type mismatch is a
// refusal at the port, not a NaN discovered three blocks downstream.
// ============================================================================

const COMPARABLE: readonly PortType[] = ["number", "text", "date", "key"];

export function validateBlockPorts(node: BoardNode, inputs: readonly PortType[]): BlockRefusal | null {
  const kind = node.kind;
  const asRefusal = (code: BlockRefusalCode, message: string): BlockRefusal =>
    ({ code, blockId: node.id, kind, message: titleOf(node) + " " + message });

  if (!isComputeBoardNodeKind(kind) && !isLoopBoardNodeKind(kind)) {
    return asRefusal("BLOCK_NOT_EVALUABLE",
      "is a relational block. Its meaning belongs to the transformation representation, not the block algebra.");
  }
  if (kind === "aggregate" || kind === "window") {
    return asRefusal("BLOCK_NOT_EVALUABLE",
      "carries governed semantics that this module does not compute. Its aggregation is declared by the canonical authority and executed on the server.");
  }

  if (isLoopBoardNodeKind(kind)) {
    const bound = loopBoundProblem(node);
    if (bound) { return asRefusal("BLOCK_LOOP_BOUND", "cannot run: " + bound); }
    return null;
  }

  const operator = operatorOf(node);
  const expected = arityOf(node);
  if (inputs.length !== expected) {
    return asRefusal("BLOCK_ARITY",
      "expects " + expected + " input(s) and was given " + inputs.length + ". It takes " + familySignature(node) + ".");
  }
  for (const port of inputs) {
    if (port === "flow") {
      return asRefusal("BLOCK_TYPE", "cannot take a flow connection as a value input.");
    }
  }

  if (kind === "arithmetic") {
    if (ARITHMETIC.indexOf(operator as ArithmeticOp) < 0) {
      return asRefusal("BLOCK_OPERATOR", "has no declared arithmetic operator. Choose one of: " + ARITHMETIC.join(", ") + ".");
    }
    for (const port of inputs) {
      if (port !== "number") {
        return asRefusal("BLOCK_TYPE", "takes " + familySignature(node) + ", and one input is " + port + ".");
      }
    }
    return null;
  }

  if (kind === "comparison") {
    if (COMPARISON.indexOf(operator as ComparisonOp) < 0) {
      return asRefusal("BLOCK_OPERATOR", "has no declared comparison operator. Choose one of: " + COMPARISON.join(", ") + ".");
    }
    for (const port of inputs) {
      if (COMPARABLE.indexOf(port) < 0) {
        return asRefusal("BLOCK_TYPE", "cannot compare a " + port + " value.");
      }
    }
    if (!portsCompatible(inputs[0], inputs[1])) {
      return asRefusal("BLOCK_TYPE", "compares " + inputs[0] + " with " + inputs[1] + ", which are not the same kind of value.");
    }
    return null;
  }

  if (kind === "logic") {
    if (LOGIC.indexOf(operator as LogicOp) < 0) {
      return asRefusal("BLOCK_OPERATOR", "has no declared logic operator. Choose one of: " + LOGIC.join(", ") + ".");
    }
    for (const port of inputs) {
      if (port !== "boolean") {
        return asRefusal("BLOCK_TYPE", "takes " + familySignature(node) + ", and one input is " + port + ".");
      }
    }
    return null;
  }

  // conditional
  if (inputs[0] !== "boolean") {
    return asRefusal("BLOCK_TYPE", "needs a boolean condition and was given " + inputs[0] + ".");
  }
  if (!portsCompatible(inputs[1], inputs[2])) {
    return asRefusal("BLOCK_TYPE",
      "would return " + inputs[1] + " on one branch and " + inputs[2] + " on the other. Both branches must produce the same kind of value.");
  }
  return null;
}

// ============================================================================
// EVALUATION
// ============================================================================

export function evaluateBlock(node: BoardNode, inputs: readonly BlockValue[]): BlockOutcome {
  const portRefusal = validateBlockPorts(node, inputs.map(portTypeOf));
  if (portRefusal) { return { ok: false, refusal: portRefusal }; }

  const operator = operatorOf(node);

  if (node.kind === "arithmetic") {
    const left = inputs[0].value as number;
    const right = inputs[1].value as number;
    switch (operator as ArithmeticOp) {
      case "add": return { ok: true, value: numberValue(left + right) };
      case "subtract": return { ok: true, value: numberValue(left - right) };
      case "multiply": return { ok: true, value: numberValue(left * right) };
      case "divide": {
        // Division by zero REFUSES. JavaScript would hand back Infinity or
        // NaN and let it travel silently into a published result; a governed
        // board says no at the block that did it.
        if (right === 0) {
          return refuse(node, "BLOCK_DIVIDE_BY_ZERO",
            "divides by zero. Guard the divisor, or use a condition to handle the zero case.");
        }
        return { ok: true, value: numberValue(left / right) };
      }
    }
  }

  if (node.kind === "comparison") {
    const a = inputs[0].value;
    const b = inputs[1].value;
    switch (operator as ComparisonOp) {
      case "equals": return { ok: true, value: booleanValue(a === b) };
      case "not-equals": return { ok: true, value: booleanValue(a !== b) };
      case "greater-than": return { ok: true, value: booleanValue(a > b) };
      case "greater-or-equal": return { ok: true, value: booleanValue(a >= b) };
      case "less-than": return { ok: true, value: booleanValue(a < b) };
      case "less-or-equal": return { ok: true, value: booleanValue(a <= b) };
    }
  }

  if (node.kind === "logic") {
    const a = inputs[0].value as boolean;
    switch (operator as LogicOp) {
      case "not": return { ok: true, value: booleanValue(!a) };
      case "and": return { ok: true, value: booleanValue(a && (inputs[1].value as boolean)) };
      case "or": return { ok: true, value: booleanValue(a || (inputs[1].value as boolean)) };
    }
  }

  if (node.kind === "conditional") {
    const condition = inputs[0].value as boolean;
    return { ok: true, value: condition ? inputs[1] : inputs[2] };
  }

  return refuse(node, "BLOCK_NOT_EVALUABLE", "has no block algebra in this module.");
}

// ============================================================================
// BOUNDED LOOPS
//
// THE LOOP LAW, ENFORCED RATHER THAN DOCUMENTED. Three families, each of which
// stops for a reason that was DECLARED before it started. Every stop is
// reported, so a loop that ran out of budget can never be mistaken for one
// that finished its work.
// ============================================================================

export type LoopStop = "collection" | "condition" | "bound" | "budget" | "cancelled";

export interface LoopEnvironment {
  /** Injected clock. Tests drive a budget to exhaustion without waiting. */
  readonly now: () => number;
  /** Injected cancellation signal, consulted before every iteration. */
  readonly cancelled: () => boolean;
}

export const UNBOUNDED_CLOCK: LoopEnvironment = {
  now: () => 0,
  cancelled: () => false,
};

export type LoopOutcome =
  | { readonly ok: true; readonly value: BlockValue; readonly iterations: number; readonly stoppedBy: LoopStop }
  | { readonly ok: false; readonly refusal: BlockRefusal };

function loopRefusal(node: BoardNode): BlockRefusal | null {
  const bound = loopBoundProblem(node);
  if (!bound) { return null; }
  return { code: "BLOCK_LOOP_BOUND", blockId: node.id, kind: node.kind, message: bound };
}

function budgetOf(node: BoardNode): number | null {
  const raw = node.data.budgetMs;
  const ms = typeof raw === "number" ? raw : Number(typeof raw === "string" ? raw : NaN);
  return Number.isFinite(ms) && ms > 0 ? ms : null;
}

function iterationsOf(node: BoardNode): number {
  const raw = node.data.maxIterations;
  return typeof raw === "number" ? raw : Number(raw);
}

interface LoopGuard {
  readonly stop: LoopStop | null;
}

function guard(env: LoopEnvironment, started: number, budgetMs: number | null): LoopGuard {
  if (env.cancelled()) { return { stop: "cancelled" }; }
  if (budgetMs !== null && env.now() - started >= budgetMs) { return { stop: "budget" }; }
  return { stop: null };
}

/**
 * ForEach is bounded by the collection it walks. It declares no count, because
 * the collection already is one. It still honours cancellation, and a budget
 * if the board declared one.
 */
export function runForEach(
  node: BoardNode,
  items: readonly BlockValue[],
  seed: BlockValue,
  fold: (accumulator: BlockValue, item: BlockValue, index: number) => BlockValue,
  env: LoopEnvironment = UNBOUNDED_CLOCK,
): LoopOutcome {
  const refusal = loopRefusal(node);
  if (refusal) { return { ok: false, refusal }; }

  const started = env.now();
  const budgetMs = budgetOf(node);
  let accumulator = seed;
  let iterations = 0;

  for (let index = 0; index < items.length; index++) {
    const check = guard(env, started, budgetMs);
    if (check.stop) { return { ok: true, value: accumulator, iterations, stoppedBy: check.stop }; }
    accumulator = fold(accumulator, items[index], index);
    iterations++;
  }
  return { ok: true, value: accumulator, iterations, stoppedBy: "collection" };
}

/** RepeatN runs exactly the count it declared, and refuses without one. */
export function runRepeatN(
  node: BoardNode,
  seed: BlockValue,
  fold: (accumulator: BlockValue, index: number) => BlockValue,
  env: LoopEnvironment = UNBOUNDED_CLOCK,
): LoopOutcome {
  const refusal = loopRefusal(node);
  if (refusal) { return { ok: false, refusal }; }

  const started = env.now();
  const budgetMs = budgetOf(node);
  const limit = iterationsOf(node);
  let accumulator = seed;
  let iterations = 0;

  for (let index = 0; index < limit; index++) {
    const check = guard(env, started, budgetMs);
    if (check.stop) { return { ok: true, value: accumulator, iterations, stoppedBy: check.stop }; }
    accumulator = fold(accumulator, index);
    iterations++;
  }
  return { ok: true, value: accumulator, iterations, stoppedBy: "bound" };
}

/**
 * WhileBounded is the only family whose stopping condition is computed, and it
 * is therefore the only one that must declare BOTH a count and a budget. It
 * stops on the condition, on the count, on the budget or on cancellation - and
 * it always says which. There is no fifth outcome and no unbounded path.
 */
export function runWhileBounded(
  node: BoardNode,
  condition: (accumulator: BlockValue, index: number) => boolean,
  seed: BlockValue,
  fold: (accumulator: BlockValue, index: number) => BlockValue,
  env: LoopEnvironment = UNBOUNDED_CLOCK,
): LoopOutcome {
  const refusal = loopRefusal(node);
  if (refusal) { return { ok: false, refusal }; }

  const started = env.now();
  const budgetMs = budgetOf(node);
  const limit = iterationsOf(node);
  let accumulator = seed;
  let iterations = 0;

  for (let index = 0; index < limit; index++) {
    const check = guard(env, started, budgetMs);
    if (check.stop) { return { ok: true, value: accumulator, iterations, stoppedBy: check.stop }; }
    if (!condition(accumulator, index)) {
      return { ok: true, value: accumulator, iterations, stoppedBy: "condition" };
    }
    accumulator = fold(accumulator, index);
    iterations++;
  }
  return { ok: true, value: accumulator, iterations, stoppedBy: "bound" };
}