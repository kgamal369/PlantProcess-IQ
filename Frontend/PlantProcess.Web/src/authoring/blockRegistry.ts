import type { BoardNodeKind } from "./graphSemantics";
import {
  arithmeticContract, boundedWhileContract, comparisonContract, conditionalContract,
  forEachContract, logicContract, repeatContract,
  type BlockFamilyContract,
} from "./blockParameters";

// PPIQ T-032. Chapter 4 section 5.2.5 - the toolbox is grouped, searchable and
// drag-and-drop onto the board, and "GROUPS ARE EXTENDED BY REGISTRY ENTRY,
// NEVER BY A CODE BRANCH". This file is that registry. AuthoringToolbox reads
// it and contains no knowledge of any individual block.
//
// T-242 Stage 3. THE REGISTRY IS NOW THE ONLY BLOCK CATALOGUE.
//
// It previously shared that job with four hardcoded maps in the authoring
// shell - an addable-id list, a block-id-to-kind map, a kind-to-title map and
// a seed ternary - so adding one family meant editing four places that could
// disagree with each other and with this file. A row here is now the whole
// declaration: identity, kind, title, ports, group, starting parameters and
// what the product can actually DO with the block.

export interface BlockGroupDefinition {
  id: string;
  label: string;
  /** Section 5.2.14 - the advanced set is collapsed by default, never hidden. */
  advanced: boolean;
}

/**
 * WHAT THE PRODUCT CAN DO WITH A BLOCK, stated as separate facts rather than
 * one overloaded boolean nobody can define a year later.
 *
 *   implemented  its behaviour and validation exist;
 *   evaluable    the deterministic block algebra can compute it;
 *   persistable  the canonical save/version/reopen path can REPRESENT it.
 *
 * These are independent. A relational block is implemented and persistable but
 * not evaluable, because its meaning is a SELECT the server runs. A compute
 * block is implemented and evaluable but not yet persistable, because the
 * transformation representation cannot carry it.
 */
export interface BlockCapabilities {
  readonly evaluable: boolean;
  readonly persistable: boolean;
}

/** The parameters a freshly placed block starts with. */
export type BlockSeed = Record<string, unknown>;

interface BlockDefinitionBase {
  id: string;
  label: string;
  group: string;
  inputs: string;
  outputs: string;
}

/**
 * A block the product claims to have IMPLEMENTED must name the graph kind it
 * becomes and the parameters it starts with. That is enforced here by the type
 * system rather than by review: there is no way to declare implemented: true
 * without a boardKind and a seed.
 */
export type BlockDefinition =
  | (BlockDefinitionBase & {
      implemented: true;
      boardKind: BoardNodeKind;
      seed: () => BlockSeed;
      capabilities: BlockCapabilities;
      /**
       * T-242 Stage 4. The family's semantic contract - required parameters
       * and TYPED PORTS - BY REFERENCE to a reusable schema. The registry
       * names the schema; it never restates it, so a signature is written in
       * exactly one place. The relational families carry none: their meaning
       * is a SELECT the server runs, and their bespoke nodes are fed from
       * live schema rather than from a declared port list.
       */
      contract?: BlockFamilyContract;
    })
  | (BlockDefinitionBase & {
      implemented: false;
      boardKind?: BoardNodeKind;
      capabilities: BlockCapabilities;
    });

/**
 * PALETTE ELIGIBILITY IS DERIVED, NEVER STORED.
 *
 * A block may be placed only when the product can validate it AND has an
 * honest way to save it. Anything less is a dead end the product walked the
 * author into knowingly. When canonical persistence arrives for a family, its
 * persistable capability becomes true and eligibility follows - no list of ids
 * is flipped, and no task-shaped condition appears anywhere in runtime code.
 */
export function isPaletteEligible(block: BlockDefinition): boolean {
  return block.implemented && block.capabilities.persistable;
}

export const BLOCK_GROUPS: readonly BlockGroupDefinition[] = [
  { id: "source-output", label: "Source and output", advanced: false },
  { id: "relational", label: "Relational", advanced: false },
  { id: "expression", label: "Arithmetic, comparison and logic", advanced: false },
  { id: "control-flow", label: "Loops and control flow", advanced: true },
  { id: "statistics", label: "Statistics and correlation", advanced: true },
  { id: "model-feature", label: "Model and feature", advanced: true },
  { id: "condition-action", label: "Condition and action", advanced: false },
];

/** Nothing declared. A block whose parameters are all chosen by the author. */
const NO_SEED = (): BlockSeed => ({});

export const BLOCK_REGISTRY: readonly BlockDefinition[] = [
  // Group 1 - source and output
  { id: "source-table", label: "Source table", group: "source-output", inputs: "-", outputs: "dataset",
    implemented: false, capabilities: { evaluable: false, persistable: true } },
  { id: "output-canonical", label: "Output to canonical entity", group: "source-output", inputs: "dataset", outputs: "-",
    implemented: false, capabilities: { evaluable: false, persistable: true } },
  { id: "output-dataset", label: "Output to named dataset", group: "source-output", inputs: "dataset", outputs: "-",
    implemented: false, capabilities: { evaluable: false, persistable: true } },

  // Group 2 - relational. A JOIN IS NOT A BLOCK: the board already states one
  // as a column wire between two tables, and a join block would be a second
  // lawful way to say the same thing. The row stays so the toolbox can tell
  // the truth about where joins are authored.
  { id: "join", label: "Join (drawn as a column wire)", group: "relational", inputs: "two datasets", outputs: "dataset",
    implemented: false, capabilities: { evaluable: false, persistable: true } },
  { id: "filter", label: "Filter", group: "relational", inputs: "dataset", outputs: "dataset",
    boardKind: "filter", seed: () => ({ fieldRef: "", op: "", value: "" }),
    implemented: true, capabilities: { evaluable: false, persistable: true } },
  { id: "select-columns", label: "Select columns", group: "relational", inputs: "dataset", outputs: "dataset",
    boardKind: "select", seed: () => ({ chosen: [] as string[] }),
    implemented: true, capabilities: { evaluable: false, persistable: true } },
  { id: "derived-column", label: "Derived column", group: "relational", inputs: "dataset", outputs: "dataset",
    boardKind: "derived", seed: () => ({ alias: "", leftRef: "", op: "", rightRef: "", constant: "" }),
    implemented: true, capabilities: { evaluable: false, persistable: true } },
  { id: "rename", label: "Rename / alias", group: "relational", inputs: "dataset", outputs: "dataset",
    implemented: false, capabilities: { evaluable: false, persistable: true } },
  { id: "group-by", label: "Group by", group: "relational", inputs: "dataset", outputs: "dataset",
    implemented: false, capabilities: { evaluable: false, persistable: true } },
  { id: "sort", label: "Sort", group: "relational", inputs: "dataset", outputs: "dataset",
    implemented: false, capabilities: { evaluable: false, persistable: true } },
  { id: "union", label: "Union", group: "relational", inputs: "two datasets", outputs: "dataset",
    implemented: false, capabilities: { evaluable: false, persistable: true } },
  { id: "distinct", label: "Distinct", group: "relational", inputs: "dataset", outputs: "dataset",
    implemented: false, capabilities: { evaluable: false, persistable: true } },
  { id: "limit", label: "Limit", group: "relational", inputs: "dataset", outputs: "dataset",
    implemented: false, capabilities: { evaluable: false, persistable: true } },
  { id: "pivot", label: "Pivot / unpivot", group: "relational", inputs: "dataset", outputs: "dataset",
    implemented: false, capabilities: { evaluable: false, persistable: true } },
  { id: "cast", label: "Cast", group: "relational", inputs: "dataset", outputs: "dataset",
    implemented: false, capabilities: { evaluable: false, persistable: true } },
  { id: "lookup", label: "Lookup", group: "relational", inputs: "dataset + dataset", outputs: "dataset",
    implemented: false, capabilities: { evaluable: false, persistable: true } },

  // Group 3 - expression. Implemented and evaluable since Stage 2, and NOT
  // persistable: the transformation representation cannot carry them, so they
  // are not palette eligible and cannot be placed. The operator seeds EMPTY on
  // purpose - a block that quietly defaulted to "add" would be choosing a
  // semantic on the author's behalf.
  { id: "expr-arithmetic", label: "Arithmetic", group: "expression", inputs: "two numbers", outputs: "number",
    boardKind: "arithmetic", contract: arithmeticContract, seed: () => ({ operator: "" }),
    implemented: true, capabilities: { evaluable: true, persistable: false } },
  { id: "expr-comparison", label: "Comparison", group: "expression", inputs: "two values", outputs: "boolean",
    boardKind: "comparison", contract: comparisonContract, seed: () => ({ operator: "" }),
    implemented: true, capabilities: { evaluable: true, persistable: false } },
  { id: "expr-logic", label: "Logic", group: "expression", inputs: "booleans", outputs: "boolean",
    boardKind: "logic", contract: logicContract, seed: () => ({ operator: "" }),
    implemented: true, capabilities: { evaluable: true, persistable: false } },
  { id: "expr-conditional", label: "If / else", group: "expression", inputs: "condition, two values", outputs: "value",
    boardKind: "conditional", contract: conditionalContract, seed: NO_SEED,
    implemented: true, capabilities: { evaluable: true, persistable: false } },

  // Group 4 - loops and control flow. Three families and no fourth. Every
  // bound seeds EMPTY, so a freshly placed loop is invalid until the author
  // declares how it stops. That is the loop law, not an oversight.
  { id: "loop-for-each", label: "For each", group: "control-flow", inputs: "collection", outputs: "value",
    boardKind: "for-each", contract: forEachContract, seed: NO_SEED,
    implemented: true, capabilities: { evaluable: true, persistable: false } },
  { id: "loop-repeat-n", label: "Repeat", group: "control-flow", inputs: "iteration count", outputs: "value",
    boardKind: "repeat-n", contract: repeatContract, seed: NO_SEED,
    implemented: true, capabilities: { evaluable: true, persistable: false } },
  { id: "loop-while-bounded", label: "Bounded while", group: "control-flow", inputs: "condition, count, budget", outputs: "value",
    boardKind: "while-bounded", contract: boundedWhileContract, seed: NO_SEED,
    implemented: true, capabilities: { evaluable: true, persistable: false } },

  // Group 5 - statistics and correlation. The governed aggregate and window
  // take their meaning from the canonical aggregation authority, so they are
  // declared here and NOT implemented until that binding exists.
  { id: "aggregate", label: "Aggregate", group: "statistics", inputs: "dataset", outputs: "value",
    boardKind: "aggregate",
    implemented: false, capabilities: { evaluable: false, persistable: false } },
  { id: "window", label: "Window", group: "statistics", inputs: "dataset", outputs: "dataset",
    boardKind: "window",
    implemented: false, capabilities: { evaluable: false, persistable: false } },
  { id: "stat-correlation", label: "Correlation", group: "statistics", inputs: "dataset", outputs: "result",
    implemented: false, capabilities: { evaluable: false, persistable: false } },
  { id: "stat-distribution", label: "Distribution", group: "statistics", inputs: "dataset", outputs: "result",
    implemented: false, capabilities: { evaluable: false, persistable: false } },
  { id: "stat-comparison", label: "Group comparison", group: "statistics", inputs: "dataset", outputs: "result",
    implemented: false, capabilities: { evaluable: false, persistable: false } },

  // Group 6 - model and feature (S4)
  { id: "feature-assembly", label: "Feature assembly", group: "model-feature", inputs: "dataset", outputs: "dataset",
    implemented: false, capabilities: { evaluable: false, persistable: false } },
  { id: "model-split", label: "Split", group: "model-feature", inputs: "dataset", outputs: "datasets",
    implemented: false, capabilities: { evaluable: false, persistable: false } },
  { id: "model-train", label: "Train", group: "model-feature", inputs: "dataset", outputs: "model",
    implemented: false, capabilities: { evaluable: false, persistable: false } },
  { id: "model-score", label: "Score", group: "model-feature", inputs: "model + dataset", outputs: "dataset",
    implemented: false, capabilities: { evaluable: false, persistable: false } },
  { id: "model-evaluate", label: "Evaluate", group: "model-feature", inputs: "model + dataset", outputs: "result",
    implemented: false, capabilities: { evaluable: false, persistable: false } },

  // Group 7 - condition and action (S5)
  { id: "condition-threshold", label: "Threshold condition", group: "condition-action", inputs: "dataset", outputs: "condition",
    implemented: false, capabilities: { evaluable: false, persistable: false } },
  { id: "condition-range", label: "Range condition", group: "condition-action", inputs: "dataset", outputs: "condition",
    implemented: false, capabilities: { evaluable: false, persistable: false } },
  { id: "condition-routing", label: "Routing-deviation condition", group: "condition-action", inputs: "dataset", outputs: "condition",
    implemented: false, capabilities: { evaluable: false, persistable: false } },
  { id: "emit-info", label: "Emit info", group: "condition-action", inputs: "condition", outputs: "-",
    implemented: false, capabilities: { evaluable: false, persistable: false } },
  { id: "emit-warning", label: "Emit warning", group: "condition-action", inputs: "condition", outputs: "-",
    implemented: false, capabilities: { evaluable: false, persistable: false } },
  { id: "emit-error", label: "Emit error", group: "condition-action", inputs: "condition", outputs: "-",
    implemented: false, capabilities: { evaluable: false, persistable: false } },
];

export function groupsForPalette(paletteGroups: readonly string[]): BlockGroupDefinition[] {
  return BLOCK_GROUPS.filter((g) => paletteGroups.indexOf(g.id) >= 0);
}

export function blocksInGroup(groupId: string): BlockDefinition[] {
  return BLOCK_REGISTRY.filter((b) => b.group === groupId);
}

export function blockById(id: string): BlockDefinition | null {
  const found = BLOCK_REGISTRY.filter((b) => b.id === id);
  return found.length === 1 ? found[0] : null;
}

/** The single place a graph kind is resolved back to its catalogue row. */
export function blockForKind(kind: BoardNodeKind): BlockDefinition | null {
  const found = BLOCK_REGISTRY.filter((b) => b.boardKind === kind);
  return found.length === 1 ? found[0] : null;
}

/**
 * The title a freshly placed block carries, taken from the catalogue rather
 * than from a second map that can drift away from it.
 */
export function titleForKind(kind: BoardNodeKind): string {
  const block = blockForKind(kind);
  return block ? block.label : kind;
}

/**
 * The starting parameters for a placed block. Returns null when the catalogue
 * does not implement the block, which is the caller's cue to refuse rather
 * than to invent a shape.
 */
export function seedForKind(kind: BoardNodeKind): BlockSeed | null {
  const block = blockForKind(kind);
  if (!block) { return null; }
  return block.implemented ? block.seed() : null;
}

/** Every block a palette may offer, derived from capability alone. */
export function paletteEligibleBlocks(): BlockDefinition[] {
  return BLOCK_REGISTRY.filter(isPaletteEligible);
}

/** The declared semantic contract for a graph kind, or null if it has none. */
export function contractForKind(kind: BoardNodeKind): BlockFamilyContract | null {
  const block = blockForKind(kind);
  if (!block || !block.implemented || !block.contract) { return null; }
  return block.contract;
}

/**
 * WHY A BLOCK CANNOT BE PLACED, in words derived from its capability state.
 * No task name, no milestone and no date appears here: the product describes
 * what is true now, and a sentence promising a future release would be a
 * schedule leaking into runtime copy. Returns null when the block CAN be
 * placed, so the caller has nothing to explain.
 */
export function blockUnavailableReason(block: BlockDefinition): string | null {
  if (isPaletteEligible(block)) { return null; }
  if (!block.implemented) {
    return "Declared in the catalogue. Its behaviour is not built yet.";
  }
  return "Built and validated, but it cannot be placed here yet: this surface"
    + " cannot save a definition that contains it.";
}

/**
 * The sentence at the head of the toolbox, assembled from the catalogue rather
 * than typed by hand, so it stays true the moment a family's capabilities
 * change and nobody has to remember to edit copy.
 */
export function paletteSummary(): string {
  const labels = paletteEligibleBlocks().map((b) => b.label);
  if (labels.length === 0) {
    return "Nothing can be placed on this surface yet. Every block below is declared here"
      + " and says why it is unavailable.";
  }
  const list = labels.length === 1
    ? labels[0]
    : labels.slice(0, -1).join(", ") + " and " + labels[labels.length - 1];
  return list + " can be placed on the board. The rest are declared here and each says why"
    + " it cannot be placed yet.";
}