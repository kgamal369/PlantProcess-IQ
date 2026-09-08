// PPIQ T-242 Stage 4. PARAMETER AND PORT SCHEMAS.
//
// WHAT THIS FILE IS. The reusable semantic contracts a block family is built
// from: which parameters it must declare, and which typed ports it exposes.
// The block registry references these; it does not restate them.
//
// WHAT THIS FILE IS NOT. It is not a second block catalogue. There is no
// "block id -> anything" table here, and there never will be: the moment two
// files each map an id to a title or a kind, they can disagree, and the shell
// spent Stage 3 removing exactly that. A family here is a SHAPE - "takes an
// operator from this list", "takes two numbers and produces a number" - and
// the registry is where a shape is given an id.
//
// IMPORT DISCIPLINE, enforced by an architecture guard. This module imports
// TYPES only. graphSemantics reads it at runtime to validate a board, and
// blockSemantics reads it at runtime to check ports, so a runtime import in the
// other direction would be a cycle. If a value from those modules is needed
// here, the design is wrong, not the guard.

import type { PortType } from "@/canvas/ports";

// ============================================================================
// OPERATORS. Block CONFIGURATION, never graph kinds.
// ============================================================================

export const ARITHMETIC_OPERATORS = ["add", "subtract", "multiply", "divide"] as const;
export const COMPARISON_OPERATORS = [
  "equals", "not-equals", "greater-than", "greater-or-equal", "less-than", "less-or-equal",
] as const;
export const LOGIC_OPERATORS = ["and", "or", "not"] as const;

export type ArithmeticOperator = (typeof ARITHMETIC_OPERATORS)[number];
export type ComparisonOperator = (typeof COMPARISON_OPERATORS)[number];
export type LogicOperator = (typeof LOGIC_OPERATORS)[number];

// ============================================================================
// PARAMETERS
// ============================================================================

export type ParameterControl =
  | { readonly kind: "choice"; readonly options: readonly string[] }
  | { readonly kind: "integer"; readonly min: number }
  | { readonly kind: "milliseconds"; readonly min: number };

export interface ParameterSpec {
  readonly key: string;
  readonly label: string;
  readonly control: ParameterControl;
  /**
   * The sentence used when the parameter is undeclared or invalid. It states
   * the rule and what would satisfy it, because a block that only says
   * "required" leaves the author guessing at what counts.
   */
  readonly missing: string;
}

/** A parameter's value is either acceptable or explained. */
export function parameterValueProblem(spec: ParameterSpec, raw: unknown): string | null {
  const control = spec.control;
  if (control.kind === "choice") {
    const value = typeof raw === "string" ? raw.trim() : "";
    return control.options.indexOf(value) >= 0 ? null : spec.missing;
  }
  const n = typeof raw === "number"
    ? raw
    : (typeof raw === "string" && raw.trim() !== "" ? Number(raw) : NaN);
  if (control.kind === "integer") {
    return Number.isInteger(n) && n >= control.min ? null : spec.missing;
  }
  return Number.isFinite(n) && n >= control.min ? null : spec.missing;
}

// ============================================================================
// TYPED PORTS
// ============================================================================

/**
 * A port type is either concrete, or a TYPE VARIABLE that must resolve to the
 * same concrete type wherever it appears on the block. That is how comparison
 * and IF/ELSE are stated honestly - "two values of the same kind" rather than
 * "two numbers" - because a plant compares text codes as often as numbers.
 * The variable is constrained to what can actually take part.
 */
export type PortTypeRef =
  | { readonly concrete: PortType }
  | { readonly variable: "T"; readonly accepts: readonly PortType[] };

export interface PortSpec {
  readonly id: string;
  readonly label: string;
  readonly type: PortTypeRef;
}

export interface PortSignature {
  readonly inputs: readonly PortSpec[];
  readonly output: PortSpec | null;
}

export type ParameterValues = Readonly<Record<string, unknown>>;

/**
 * THE FAMILY CONTRACT. Parameters, and ports as a FUNCTION of the declared
 * parameters, because a logic block configured as "not" has one input where
 * "and" has two. The signature is the single authority: board validation
 * reads it, the evaluator reads it, and the rendered sentence is derived from
 * it. There is no second description anywhere.
 */
export interface BlockFamilyContract {
  readonly parameters: readonly ParameterSpec[];
  readonly ports: (values: ParameterValues) => PortSignature;
}

const NUMBER: PortTypeRef = { concrete: "number" };
const BOOLEAN: PortTypeRef = { concrete: "boolean" };
const DATASET: PortTypeRef = { concrete: "key" };
const COMPARABLE: PortTypeRef = { variable: "T", accepts: ["number", "text", "date", "key"] };
const ANY_VALUE: PortTypeRef = { variable: "T", accepts: ["number", "text", "date", "key", "boolean"] };

function port(id: string, label: string, type: PortTypeRef): PortSpec {
  return { id, label, type };
}

// ---- reusable parameter schemas -------------------------------------------

export const arithmeticOperatorParameter: ParameterSpec = {
  key: "operator",
  label: "Operation",
  control: { kind: "choice", options: ARITHMETIC_OPERATORS },
  missing: "has no declared arithmetic operator. Choose one of: " + ARITHMETIC_OPERATORS.join(", ") + ".",
};

export const comparisonOperatorParameter: ParameterSpec = {
  key: "operator",
  label: "Comparison",
  control: { kind: "choice", options: COMPARISON_OPERATORS },
  missing: "has no declared comparison operator. Choose one of: " + COMPARISON_OPERATORS.join(", ") + ".",
};

export const logicOperatorParameter: ParameterSpec = {
  key: "operator",
  label: "Logic",
  control: { kind: "choice", options: LOGIC_OPERATORS },
  missing: "has no declared logic operator. Choose one of: " + LOGIC_OPERATORS.join(", ") + ".",
};

export const iterationCountParameter: ParameterSpec = {
  key: "maxIterations",
  label: "Iterations",
  control: { kind: "integer", min: 1 },
  missing: "has no finite iteration bound. Declare a whole number of iterations greater than zero;"
    + " an unbounded loop cannot be validated or published.",
};

export const runtimeBudgetParameter: ParameterSpec = {
  key: "budgetMs",
  label: "Budget (ms)",
  control: { kind: "milliseconds", min: 1 },
  missing: "has no runtime budget. Declare a positive budget in milliseconds so the loop can be"
    + " cancelled rather than left running.",
};

// ---- family contracts -----------------------------------------------------

export const arithmeticContract: BlockFamilyContract = {
  parameters: [arithmeticOperatorParameter],
  ports: () => ({
    inputs: [port("left", "Left", NUMBER), port("right", "Right", NUMBER)],
    output: port("value", "Value", NUMBER),
  }),
};

export const comparisonContract: BlockFamilyContract = {
  parameters: [comparisonOperatorParameter],
  ports: () => ({
    inputs: [port("left", "Left", COMPARABLE), port("right", "Right", COMPARABLE)],
    output: port("value", "Result", BOOLEAN),
  }),
};

export const logicContract: BlockFamilyContract = {
  parameters: [logicOperatorParameter],
  ports: (values) => ({
    inputs: values.operator === "not"
      ? [port("operand", "Operand", BOOLEAN)]
      : [port("left", "Left", BOOLEAN), port("right", "Right", BOOLEAN)],
    output: port("value", "Result", BOOLEAN),
  }),
};

export const conditionalContract: BlockFamilyContract = {
  parameters: [],
  ports: () => ({
    inputs: [
      port("condition", "Condition", BOOLEAN),
      port("whenTrue", "When true", ANY_VALUE),
      port("whenFalse", "When false", ANY_VALUE),
    ],
    output: port("value", "Value", ANY_VALUE),
  }),
};

export const forEachContract: BlockFamilyContract = {
  parameters: [],
  ports: () => ({
    inputs: [port("collection", "Collection", DATASET)],
    output: port("value", "Result", ANY_VALUE),
  }),
};

export const repeatContract: BlockFamilyContract = {
  parameters: [iterationCountParameter],
  ports: () => ({ inputs: [], output: port("value", "Result", ANY_VALUE) }),
};

export const boundedWhileContract: BlockFamilyContract = {
  parameters: [iterationCountParameter, runtimeBudgetParameter],
  ports: () => ({
    inputs: [port("condition", "Condition", BOOLEAN)],
    output: port("value", "Result", ANY_VALUE),
  }),
};

// ============================================================================
// DERIVED PRESENTATION. The sentence comes FROM the ports, never beside them.
// ============================================================================

export function describePortType(ref: PortTypeRef): string {
  if ("concrete" in ref) {
    return ref.concrete === "key" ? "dataset" : ref.concrete;
  }
  return "value";
}

/**
 * "two numbers and produces a number" - assembled from the typed signature so
 * the node can never say one thing while the contract means another.
 */
export function describeSignature(signature: PortSignature): string {
  const inputs = signature.inputs;
  const output = signature.output ? describePortType(signature.output.type) : null;
  let takes: string;
  if (inputs.length === 0) {
    takes = "no data input";
  } else if (inputs.length > 1 && inputs.every((p) => "variable" in p.type)) {
    takes = "values of the same kind";
  } else {
    const names = inputs.map((p) => describePortType(p.type));
    const uniform = names.every((n) => n === names[0]);
    if (uniform) {
      const count = names.length === 1 ? "one" : names.length === 2 ? "two" : String(names.length);
      takes = count + " " + names[0] + (names.length > 1 ? "s" : "");
    } else {
      takes = names.join(", ");
    }
  }
  return output ? takes + " and produces a " + output : takes;
}