import { describe, expect, it } from "vitest";

import {
  BLOCK_GROUPS,
  BLOCK_REGISTRY,
  blockById,
  blockForKind,
  isPaletteEligible,
  paletteEligibleBlocks,
  seedForKind,
  titleForKind,
} from "./blockRegistry";
import {
  BoardRefusalError,
  EXECUTABLE_BOARD_NODE_KINDS,
  FLOW_IN,
  FLOW_OUT,
  serialisationOutcome,
  type BoardEdge,
  type BoardNode,
} from "./graphSemantics";

function sourceNode(): BoardNode {
  return {
    id: "source_a",
    kind: "dataset",
    data: { table: "source_a", source: "source_a", columns: [] },
  };
}

function wire(from: string, to: string): BoardEdge {
  return { source: from, target: to, sourceHandle: FLOW_OUT, targetHandle: FLOW_IN };
}

describe("T-242 the registry is the only block catalogue", () => {
  it("C242-34 every implemented block declares a kind, a title and a seed", () => {
    for (const block of BLOCK_REGISTRY) {
      if (!block.implemented) { continue; }
      expect(block.boardKind, block.id).toBeDefined();
      expect(EXECUTABLE_BOARD_NODE_KINDS, block.id).toContain(block.boardKind);
      expect(typeof block.seed, block.id).toBe("function");
      expect(block.label.length, block.id).toBeGreaterThan(0);
    }
  });

  it("C242-35 a kind resolves to exactly one catalogue row", () => {
    const claimed = BLOCK_REGISTRY.filter((b) => b.boardKind !== undefined);
    const kinds = claimed.map((b) => b.boardKind);
    expect(new Set(kinds).size, "no two rows may claim the same kind").toBe(kinds.length);
    for (const block of claimed) {
      expect(blockForKind(block.boardKind!)?.id).toBe(block.id);
    }
  });

  it("C242-36 title and seed come from the catalogue, not from a second map", () => {
    expect(titleForKind("filter")).toBe("Filter");
    expect(titleForKind("select")).toBe("Select columns");
    expect(titleForKind("derived")).toBe("Derived column");
    expect(seedForKind("filter")).toEqual({ fieldRef: "", op: "", value: "" });
    expect(seedForKind("select")).toEqual({ chosen: [] });
    expect(seedForKind("derived")).toEqual({ alias: "", leftRef: "", op: "", rightRef: "", constant: "" });
  });

  it("C242-37 a seed is a fresh object, so two placed blocks never share state", () => {
    const first = seedForKind("select") as Record<string, unknown>;
    const second = seedForKind("select") as Record<string, unknown>;
    expect(first).toEqual(second);
    expect(first).not.toBe(second);
    (first.chosen as string[]).push("source_a.value_a");
    expect((second.chosen as string[]).length).toBe(0);
  });

  it("C242-38 an operator or a loop bound is never seeded with a guess", () => {
    for (const kind of ["arithmetic", "comparison", "logic"] as const) {
      expect(seedForKind(kind), kind).toEqual({ operator: "" });
    }
    for (const kind of ["for-each", "repeat-n", "while-bounded"] as const) {
      const seed = seedForKind(kind) as Record<string, unknown>;
      expect(seed, kind).toEqual({});
      expect(seed.maxIterations, kind).toBeUndefined();
      expect(seed.budgetMs, kind).toBeUndefined();
    }
  });

  it("C242-39 an unimplemented block yields no seed rather than an invented shape", () => {
    expect(seedForKind("aggregate")).toBeNull();
    expect(seedForKind("window")).toBeNull();
    expect(blockById("group-by")?.implemented).toBe(false);
  });

  it("C242-40 every block belongs to a declared group", () => {
    const groupIds = BLOCK_GROUPS.map((g) => g.id);
    for (const block of BLOCK_REGISTRY) {
      expect(groupIds, block.id).toContain(block.group);
    }
  });

  it("C242-41 palette eligibility is derived and currently excludes every compute family", () => {
    expect(paletteEligibleBlocks().map((b) => b.id)).toEqual([
      "filter", "select-columns", "derived-column",
    ]);
    for (const block of BLOCK_REGISTRY) {
      expect(isPaletteEligible(block), block.id)
        .toBe(block.implemented && block.capabilities.persistable);
    }
  });
});

describe("T-242 serialisation reports, never swallows", () => {
  it("C242-42 a valid relational board serialises", () => {
    const outcome = serialisationOutcome("definition_a", "entity_a", [sourceNode()], []);
    expect(outcome.ok).toBe(true);
    expect(outcome.ok && outcome.graph.tables).toEqual(["source_a"]);
  });

  it("C242-43 an invalid board comes back as a governed refusal, not null", () => {
    const source = sourceNode();
    const filter: BoardNode = { id: "filter-1", kind: "filter", data: { title: "Filter 1" } };
    const outcome = serialisationOutcome("definition_a", "entity_a", [source, filter], []);
    expect(outcome.ok).toBe(false);
    expect(!outcome.ok && outcome.refusal.code).toBe("BOARD_INVALID");
    expect(!outcome.ok && outcome.refusal.message.length).toBeGreaterThan(0);
  });

  it("C242-44 an unrepresentable block names itself and its kind", () => {
    const source = sourceNode();
    const arithmetic: BoardNode = { id: "arithmetic-1", kind: "arithmetic", data: { title: "Arithmetic 1" } };
    const outcome = serialisationOutcome(
      "definition_a", "entity_a", [source, arithmetic], [wire(source.id, arithmetic.id)],
    );
    expect(outcome.ok).toBe(false);
    if (outcome.ok) { return; }
    expect(outcome.refusal.code).toBe("REPRESENTATION_UNSUPPORTED");
    expect(outcome.refusal.blockId).toBe("arithmetic-1");
    expect(outcome.refusal.blockKind).toBe("arithmetic");
    expect(outcome.refusal.message).toContain("cannot be saved as a transformation");
  });

  it("C242-45 a governed refusal never escapes as an exception", () => {
    const source = sourceNode();
    const arithmetic: BoardNode = { id: "arithmetic-2", kind: "arithmetic", data: {} };
    let thrown: unknown = null;
    let outcomeOk: boolean | null = null;
    try {
      const outcome = serialisationOutcome(
        "definition_a", "entity_a", [source, arithmetic], [wire(source.id, arithmetic.id)],
      );
      outcomeOk = outcome.ok;
    } catch (error) {
      thrown = error;
    }
    expect(thrown, "a governed refusal must come back as data").toBeNull();
    expect(outcomeOk).toBe(false);
  });

  it("C242-46 a defect is distinguishable from a refusal, so it cannot be disguised as one", () => {
    // An unknown kind is caught by validation and comes back GOVERNED.
    const source = sourceNode();
    const broken = { id: "x-1", kind: "not-a-kind", data: {} } as unknown as BoardNode;
    const outcome = serialisationOutcome(
      "definition_a", "entity_a", [source, broken], [wire(source.id, "x-1")],
    );
    expect(outcome.ok).toBe(false);
    expect(!outcome.ok && outcome.refusal.code).toBe("BOARD_INVALID");

    // And the discriminator the catch relies on genuinely separates the two,
    // so an ordinary defect can never be reported as an authoring refusal.
    expect(new Error("boom")).not.toBeInstanceOf(BoardRefusalError);
    expect(new BoardRefusalError({ code: "BOARD_INVALID", message: "m" })).toBeInstanceOf(Error);
  });
});