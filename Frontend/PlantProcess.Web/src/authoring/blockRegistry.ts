// PPIQ T-032. Chapter 4 section 5.2.5 - the toolbox is grouped, searchable and
// drag-and-drop onto the board, and "GROUPS ARE EXTENDED BY REGISTRY ENTRY,
// NEVER BY A CODE BRANCH". This file is that registry. AuthoringToolbox reads
// it and contains no knowledge of any individual block.
//
// SCOPE NOTE, stated rather than implied. T-032 builds the shell contract and
// the four regions. The board behaviour of these blocks - typed ports, the
// drag from toolbox to board, the per-block inspector - is T-033. Every block
// is therefore declared here and rendered UNAVAILABLE, which is the pattern
// T-033's own contract names for blocks the design defines but the current
// step does not implement. Nothing here is a placeholder for a block the
// design does not have.

export type BlockPlacement = "board" | "expression";

export interface BlockGroupDefinition {
  id: string;
  label: string;
  /** Section 5.2.14 - the advanced set is collapsed by default, never hidden. */
  advanced: boolean;
}

export interface BlockDefinition {
  id: string;
  label: string;
  group: string;
  /**
   * Section 5.2.5 group 3 - arithmetic, comparison and logic are EXPRESSION
   * blocks, not board blocks. They live inside the block they configure and
   * are opened by double-click, so the toolbox does not offer them as nodes.
   */
  placement: BlockPlacement;
  inputs: string;
  outputs: string;
  /**
   * False until the block has real board behaviour. The toolbox renders an
   * unavailable block with the reason, never a control that does nothing.
   */
  available: boolean;
}

export const BLOCK_GROUPS: readonly BlockGroupDefinition[] = [
  { id: "source-output", label: "Source and output", advanced: false },
  { id: "relational", label: "Relational", advanced: false },
  { id: "expression", label: "Arithmetic, comparison and logic", advanced: false },
  { id: "statistics", label: "Statistics and correlation", advanced: true },
  { id: "model-feature", label: "Model and feature", advanced: true },
  { id: "condition-action", label: "Condition and action", advanced: false },
];

export const BLOCK_REGISTRY: readonly BlockDefinition[] = [
  // Group 1 - source and output
  { id: "source-table", label: "Source table", group: "source-output", placement: "board", inputs: "-", outputs: "dataset", available: false },
  { id: "output-canonical", label: "Output to canonical entity", group: "source-output", placement: "board", inputs: "dataset", outputs: "-", available: false },
  { id: "output-dataset", label: "Output to named dataset", group: "source-output", placement: "board", inputs: "dataset", outputs: "-", available: false },

  // Group 2 - relational
  { id: "join", label: "Join", group: "relational", placement: "board", inputs: "two datasets", outputs: "dataset", available: false },
  { id: "filter", label: "Filter", group: "relational", placement: "board", inputs: "dataset", outputs: "dataset", available: false },
  { id: "select-columns", label: "Select columns", group: "relational", placement: "board", inputs: "dataset", outputs: "dataset", available: false },
  { id: "rename", label: "Rename / alias", group: "relational", placement: "board", inputs: "dataset", outputs: "dataset", available: false },
  { id: "group-by", label: "Group by", group: "relational", placement: "board", inputs: "dataset", outputs: "dataset", available: false },
  { id: "sort", label: "Sort", group: "relational", placement: "board", inputs: "dataset", outputs: "dataset", available: false },
  { id: "union", label: "Union", group: "relational", placement: "board", inputs: "two datasets", outputs: "dataset", available: false },
  { id: "distinct", label: "Distinct", group: "relational", placement: "board", inputs: "dataset", outputs: "dataset", available: false },
  { id: "limit", label: "Limit", group: "relational", placement: "board", inputs: "dataset", outputs: "dataset", available: false },
  { id: "pivot", label: "Pivot / unpivot", group: "relational", placement: "board", inputs: "dataset", outputs: "dataset", available: false },
  { id: "derived-column", label: "Derived column", group: "relational", placement: "board", inputs: "dataset", outputs: "dataset", available: false },
  { id: "cast", label: "Cast", group: "relational", placement: "board", inputs: "dataset", outputs: "dataset", available: false },
  { id: "lookup", label: "Lookup", group: "relational", placement: "board", inputs: "dataset + dataset", outputs: "dataset", available: false },

  // Group 3 - expression blocks. Declared so the registry is complete and the
  // toolbox can state where they live, never rendered as board nodes.
  { id: "expr-arithmetic", label: "Arithmetic", group: "expression", placement: "expression", inputs: "values", outputs: "value", available: false },
  { id: "expr-comparison", label: "Comparison", group: "expression", placement: "expression", inputs: "values", outputs: "boolean", available: false },
  { id: "expr-logic", label: "Logic", group: "expression", placement: "expression", inputs: "booleans", outputs: "boolean", available: false },
  { id: "expr-conditional", label: "If / else", group: "expression", placement: "expression", inputs: "condition, values", outputs: "value", available: false },

  // Group 4 - statistics and correlation (S3)
  { id: "stat-correlation", label: "Correlation", group: "statistics", placement: "board", inputs: "dataset", outputs: "result", available: false },
  { id: "stat-distribution", label: "Distribution", group: "statistics", placement: "board", inputs: "dataset", outputs: "result", available: false },
  { id: "stat-comparison", label: "Group comparison", group: "statistics", placement: "board", inputs: "dataset", outputs: "result", available: false },

  // Group 5 - model and feature (S4)
  { id: "feature-assembly", label: "Feature assembly", group: "model-feature", placement: "board", inputs: "dataset", outputs: "dataset", available: false },
  { id: "model-split", label: "Split", group: "model-feature", placement: "board", inputs: "dataset", outputs: "datasets", available: false },
  { id: "model-train", label: "Train", group: "model-feature", placement: "board", inputs: "dataset", outputs: "model", available: false },
  { id: "model-score", label: "Score", group: "model-feature", placement: "board", inputs: "model + dataset", outputs: "dataset", available: false },
  { id: "model-evaluate", label: "Evaluate", group: "model-feature", placement: "board", inputs: "model + dataset", outputs: "result", available: false },

  // Group 6 - condition and action (S5)
  { id: "condition-threshold", label: "Threshold condition", group: "condition-action", placement: "board", inputs: "dataset", outputs: "condition", available: false },
  { id: "condition-range", label: "Range condition", group: "condition-action", placement: "board", inputs: "dataset", outputs: "condition", available: false },
  { id: "condition-routing", label: "Routing-deviation condition", group: "condition-action", placement: "board", inputs: "dataset", outputs: "condition", available: false },
  { id: "emit-info", label: "Emit info", group: "condition-action", placement: "board", inputs: "condition", outputs: "-", available: false },
  { id: "emit-warning", label: "Emit warning", group: "condition-action", placement: "board", inputs: "condition", outputs: "-", available: false },
  { id: "emit-error", label: "Emit error", group: "condition-action", placement: "board", inputs: "condition", outputs: "-", available: false },
];

export function groupsForPalette(paletteGroups: readonly string[]): BlockGroupDefinition[] {
  return BLOCK_GROUPS.filter((g) => paletteGroups.indexOf(g.id) >= 0);
}

export function blocksInGroup(groupId: string): BlockDefinition[] {
  return BLOCK_REGISTRY.filter((b) => b.group === groupId && b.placement === "board");
}