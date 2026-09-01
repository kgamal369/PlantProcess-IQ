import { describe, expect, it } from "vitest";

import {
  EXECUTABLE_BOARD_NODE_KINDS,
  FLOW_IN,
  FLOW_OUT,
  blockProblem,
  boardProblems,
  serialiseGraph,
  type BoardEdge,
  type BoardNode,
} from "./graphSemantics";
import { BLOCK_REGISTRY } from "./blockRegistry";
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

  it("C241-10 every available board block declares an executable BoardNode kind", () => {
    const available = BLOCK_REGISTRY.filter(
      (b) => b.placement === "board" && b.available,
    );
    expect(available.map((b) => b.id)).toEqual([
      "filter", "select-columns", "derived-column",
    ]);
    for (const block of available) {
      expect(block.boardKind).toBeDefined();
      expect(EXECUTABLE_BOARD_NODE_KINDS).toContain(block.boardKind);
    }
  });

  it("does not make Group by executable merely because it exists in the palette", () => {
    const groupBy = BLOCK_REGISTRY.find((b) => b.id === "group-by");
    expect(groupBy).toBeDefined();
    expect(groupBy?.available).toBe(false);
    expect(groupBy?.boardKind).toBeUndefined();
  });

  it("a valid source-only graph remains serialisable", () => {
    const graph = serialiseGraph("definition_a", "entity_a", [sourceNode()], []);
    expect(graph.tables).toEqual(["source_a"]);
    expect(graph.joins).toEqual([]);
  });
});
