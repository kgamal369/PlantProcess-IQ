import { describe, expect, it } from "vitest";

import {
  COMPUTE_BOARD_NODE_KINDS,
  EXECUTABLE_BOARD_NODE_KINDS,
  LOOP_BOARD_NODE_KINDS,
  RELATIONAL_BOARD_NODE_KINDS,
  FLOW_IN,
  FLOW_OUT,
  blockProblem,
  boardProblems,
  serialiseGraph,
  type BoardEdge,
  type BoardNode,
} from "./graphSemantics";
import {
  BLOCK_REGISTRY, blockById, isPaletteEligible, paletteEligibleBlocks,
  type BlockDefinition,
} from "./blockRegistry";
import { portsCompatible } from "../canvas/ports";

function sourceNode(): BoardNode {
  return {
    id: "source_a",
    kind: "dataset",
    data: { table: "source_a", source: "source_a", columns: [] },
  };
}

function unsupportedNode(): BoardNode {
  return {
    id: "group-1",
    kind: "group-by",
    data: { chosen: ["source_a.value_a"] },
  } as unknown as BoardNode;
}

function unsupportedBoard(): { nodes: BoardNode[]; edges: BoardEdge[] } {
  const source = sourceNode();
  const unsupported = unsupportedNode();
  return {
    nodes: [source, unsupported],
    edges: [{
      source: source.id,
      target: unsupported.id,
      sourceHandle: FLOW_OUT,
      targetHandle: FLOW_IN,
    }],
  };
}

describe("T-241 executable Canvas contract", () => {
  it("C241-01 exposes one runtime-checkable executable node vocabulary", () => {
    expect([...EXECUTABLE_BOARD_NODE_KINDS]).toEqual([
      "dataset", "filter", "derived", "select",
      "arithmetic", "comparison", "logic", "conditional", "aggregate", "window",
      "for-each", "repeat-n", "while-bounded",
    ]);
    expect(new Set(EXECUTABLE_BOARD_NODE_KINDS).size).toBe(EXECUTABLE_BOARD_NODE_KINDS.length);
  });

  it("C241-02 refuses a required dataset input that is not connected", () => {
    const source = sourceNode();
    const filter: BoardNode = {
      id: "filter-1",
      kind: "filter",
      data: { title: "Filter 1", fieldRef: "", op: "", value: "" },
    };
    expect(blockProblem(filter, [source, filter], [])).toContain(
      "dataset input is not connected",
    );
  });

  it("C241-03 refuses incompatible typed column ports", () => {
    expect(portsCompatible("number", "text")).toBe(false);
    expect(portsCompatible("date", "number")).toBe(false);
    expect(portsCompatible("flow", "number")).toBe(false);
  });

  it("C241-04 preserves compatible typed-port behaviour", () => {
    expect(portsCompatible("number", "number")).toBe(true);
    expect(portsCompatible("text", "text")).toBe(true);
    expect(portsCompatible("flow", "flow")).toBe(true);
  });

  it("C241-05 refuses an unknown persisted block kind before Select fallback", () => {
    const { nodes, edges } = unsupportedBoard();
    expect(blockProblem(nodes[1], nodes, edges)).toBe(
      "Group by cannot run: this build has no behaviour for a group-by block. Remove it, or use a block the toolbox offers.",
    );
  });

  it("C241-05 propagates the unknown-kind refusal through board validation", () => {
    const { nodes, edges } = unsupportedBoard();
    expect(boardProblems(nodes, edges)).toEqual([
      "Group by cannot run: this build has no behaviour for a group-by block. Remove it, or use a block the toolbox offers.",
    ]);
  });

  it("C241-05 refuses serialisation of an unknown persisted block kind", () => {
    const { nodes, edges } = unsupportedBoard();
    expect(() => serialiseGraph("definition_a", "entity_a", nodes, edges)).toThrow(
      "Group by cannot run: this build has no behaviour for a group-by block.",
    );
  });

  it("C241-08 cannot serialise an invalid graph", () => {
    const source = sourceNode();
    const filter: BoardNode = {
      id: "filter-1",
      kind: "filter",
      data: { title: "Filter 1", fieldRef: "", op: "", value: "" },
    };
    expect(() => serialiseGraph("definition_a", "entity_a", [source, filter], [])).toThrow(
      "dataset input is not connected",
    );
  });

  it("C241-09 validation is deterministic", () => {
    const { nodes, edges } = unsupportedBoard();
    expect(boardProblems(nodes, edges)).toEqual(boardProblems(nodes, edges));
  });

  it("C241-10 every palette-eligible block declares an executable BoardNode kind", () => {
    // Amended under T-242 Stage 3. The control used to enumerate a stored
    // available flag; eligibility is now DERIVED from capability, so the
    // control asserts the law instead of the list. Its invariant is unchanged:
    // nothing reaches a palette without a kind the graph can execute.
    const eligible = paletteEligibleBlocks();
    expect(eligible.map((b) => b.id)).toEqual([
      "filter", "select-columns", "derived-column",
    ]);
    for (const block of eligible) {
      expect(block.boardKind).toBeDefined();
      expect(EXECUTABLE_BOARD_NODE_KINDS).toContain(block.boardKind);
    }
  });

  it("C241-10b a block is eligible only when it is implemented AND persistable", () => {
    for (const block of BLOCK_REGISTRY) {
      if (isPaletteEligible(block)) {
        expect(block.implemented, block.id).toBe(true);
        expect(block.capabilities.persistable, block.id).toBe(true);
      }
    }
    // The compute and loop families are implemented and evaluable, and are
    // deliberately NOT eligible: the canonical representation cannot carry
    // them yet, and a block you can place but never save is a dead end.
    for (const id of ["expr-arithmetic", "expr-comparison", "expr-logic",
                      "expr-conditional", "loop-for-each", "loop-repeat-n",
                      "loop-while-bounded"]) {
      const block = blockById(id);
      expect(block, id).not.toBeNull();
      expect(block?.implemented, id).toBe(true);
      expect(block?.capabilities.evaluable, id).toBe(true);
      expect(block?.capabilities.persistable, id).toBe(false);
      expect(isPaletteEligible(block as BlockDefinition), id).toBe(false);
    }
  });

  it("does not make Group by executable merely because it exists in the palette", () => {
    const groupBy = blockById("group-by");
    expect(groupBy).not.toBeNull();
    expect(groupBy?.implemented).toBe(false);
    expect(groupBy?.boardKind).toBeUndefined();
  });
  // ==========================================================================
  // T-242 STAGE 1. THE VOCABULARY IS EXTENDED AND THE SERIALISER IS EXHAUSTIVE.
  // ==========================================================================

  it("C242-01 the three behaviour subsets partition the vocabulary exactly", () => {
    const union = [
      ...RELATIONAL_BOARD_NODE_KINDS,
      ...COMPUTE_BOARD_NODE_KINDS,
      ...LOOP_BOARD_NODE_KINDS,
    ];
    expect([...EXECUTABLE_BOARD_NODE_KINDS]).toEqual(union);
    expect(new Set(union).size).toBe(union.length);
  });

  it("C242-02 refuses a compute block BY NAME instead of routing it into Select", () => {
    const source = sourceNode();
    const arithmetic: BoardNode = {
      id: "arithmetic-1",
      kind: "arithmetic",
      data: { title: "Arithmetic 1" },
    };
    const nodes = [source, arithmetic];
    const edges: BoardEdge[] = [{
      source: source.id,
      target: arithmetic.id,
      sourceHandle: FLOW_OUT,
      targetHandle: FLOW_IN,
    }];

    // It is a VALID block. Its run path is the job path, not SELECT.
    expect(boardProblems(nodes, edges)).toEqual([]);

    let thrown: unknown = null;
    try {
      serialiseGraph("definition_a", "entity_a", nodes, edges);
    } catch (error) {
      thrown = error;
    }
    const message = String(thrown);
    expect(message).toContain("arithmetic-1");
    expect(message).toContain("kind arithmetic");
    expect(message).toContain("cannot be saved as a transformation");
  });

  it("C242-03 the wrong-branch negative control: no compute block ever emits an empty projection", () => {
    const source = sourceNode();
    const nodes: BoardNode[] = [source];
    const edges: BoardEdge[] = [];

    for (const kind of [...COMPUTE_BOARD_NODE_KINDS, ...LOOP_BOARD_NODE_KINDS]) {
      const node: BoardNode = {
        id: kind + "-1",
        kind,
        data: { title: kind, maxIterations: 3, budgetMs: 1000 },
      };
      const board = [source, node];
      const wiring: BoardEdge[] = [{
        source: source.id,
        target: node.id,
        sourceHandle: FLOW_OUT,
        targetHandle: FLOW_IN,
      }];

      let thrown: unknown = null;
      try {
        serialiseGraph("definition_a", "entity_a", board, wiring);
      } catch (error) {
        thrown = error;
      }
      expect(String(thrown), kind + " must refuse by name").toContain(node.id);
      expect(String(thrown), kind + " must not borrow Select's branch")
        .toContain("cannot be saved as a transformation");
    }

    // The relational board itself is untouched by any of that.
    expect(serialiseGraph("definition_a", "entity_a", nodes, edges).tables)
      .toEqual(["source_a"]);
  });

  it("C242-04 refuses a bounded while with no finite iteration bound", () => {
    const source = sourceNode();
    const loop: BoardNode = {
      id: "while-1",
      kind: "while-bounded",
      data: { title: "While 1", budgetMs: 1000 },
    };
    const edges: BoardEdge[] = [{
      source: source.id, target: loop.id,
      sourceHandle: FLOW_OUT, targetHandle: FLOW_IN,
    }];
    expect(blockProblem(loop, [source, loop], edges))
      .toContain("no finite iteration bound");
  });

  it("C242-05 refuses a bounded while with no runtime budget", () => {
    const source = sourceNode();
    const loop: BoardNode = {
      id: "while-2",
      kind: "while-bounded",
      data: { title: "While 2", maxIterations: 10 },
    };
    const edges: BoardEdge[] = [{
      source: source.id, target: loop.id,
      sourceHandle: FLOW_OUT, targetHandle: FLOW_IN,
    }];
    expect(blockProblem(loop, [source, loop], edges))
      .toContain("no runtime budget");
  });

  it("C242-06 accepts a bounded while that declares both, and RepeatN that declares a count", () => {
    const source = sourceNode();
    const loop: BoardNode = {
      id: "while-3",
      kind: "while-bounded",
      data: { title: "While 3", maxIterations: 10, budgetMs: 1000 },
    };
    const repeat: BoardNode = {
      id: "repeat-1",
      kind: "repeat-n",
      data: { title: "Repeat 1", maxIterations: 5 },
    };
    const edges: BoardEdge[] = [
      { source: source.id, target: loop.id, sourceHandle: FLOW_OUT, targetHandle: FLOW_IN },
      { source: loop.id, target: repeat.id, sourceHandle: FLOW_OUT, targetHandle: FLOW_IN },
    ];
    const nodes = [source, loop, repeat];
    expect(blockProblem(loop, nodes, edges)).toBeNull();
    expect(blockProblem(repeat, nodes, edges)).toBeNull();
  });

  it("C242-07 a zero or fractional iteration count is not a bound", () => {
    const source = sourceNode();
    for (const value of [0, -1, 2.5, "", "abc"]) {
      const repeat: BoardNode = {
        id: "repeat-x",
        kind: "repeat-n",
        data: { title: "Repeat X", maxIterations: value },
      };
      const edges: BoardEdge[] = [{
        source: source.id, target: repeat.id,
        sourceHandle: FLOW_OUT, targetHandle: FLOW_IN,
      }];
      expect(blockProblem(repeat, [source, repeat], edges), String(value))
        .toContain("no finite iteration bound");
    }
  });

  it("C242-08 ForEach declares no count and no budget, and is not refused for lacking them", () => {
    const source = sourceNode();
    const forEach: BoardNode = {
      id: "for-each-1",
      kind: "for-each",
      data: { title: "For each 1" },
    };
    const edges: BoardEdge[] = [{
      source: source.id, target: forEach.id,
      sourceHandle: FLOW_OUT, targetHandle: FLOW_IN,
    }];
    expect(blockProblem(forEach, [source, forEach], edges)).toBeNull();
  });

  it("C242-09 an extended kind still requires its dataset input", () => {
    const source = sourceNode();
    const aggregate: BoardNode = {
      id: "aggregate-1",
      kind: "aggregate",
      data: { title: "Aggregate 1" },
    };
    expect(blockProblem(aggregate, [source, aggregate], []))
      .toContain("dataset input is not connected");
  });


  it("a valid source-only graph remains serialisable", () => {
    const graph = serialiseGraph("definition_a", "entity_a", [sourceNode()], []);
    expect(graph.tables).toEqual(["source_a"]);
    expect(graph.joins).toEqual([]);
  });
});
