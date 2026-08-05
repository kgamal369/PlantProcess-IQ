// PPIQ T-033 items 2 to 6. Board semantics under test.
//
// These are the assertions that make ruling 2 mechanical rather than a promise:
// a field with no resolvable lineage invalidates its block, and the table is
// never inferred from anything else on the board.

import { describe, expect, it } from "vitest";
import type { StagedDataset } from "@/api/canvasApi";
import {
  COLUMN_IN, COLUMN_OUT, FLOW_IN, FLOW_OUT,
  arrangeBoard, blockProblem, boardProblems, fieldsVisibleAt, flowChainFrom,
  joinCluster, orderedBlocks, parseFieldRef, serialiseGraph, wiringRefusal,
  type BoardEdge, type BoardNode, type ProposedWire,
} from "./graphSemantics";

const heats: StagedDataset = {
  table: "alpha", source: "dump_store",
  columns: [
    { name: "alpha_id", sqlType: "text", isKeyCandidate: true },
    { name: "weight_kg", sqlType: "numeric", isKeyCandidate: false },
  ],
};
const coils: StagedDataset = {
  table: "beta", source: "dump_store",
  columns: [
    { name: "alpha_id", sqlType: "text", isKeyCandidate: true },
    { name: "width_mm", sqlType: "numeric", isKeyCandidate: false },
  ],
};

function dsNode(ds: StagedDataset): BoardNode {
  return { id: ds.table, kind: "dataset", data: { table: ds.table, source: ds.source, columns: ds.columns } };
}
function joinEdge(a: string, ac: string, b: string, bc: string): BoardEdge {
  return { source: a, target: b, sourceHandle: COLUMN_OUT + ac, targetHandle: COLUMN_IN + bc };
}
function flowEdge(a: string, b: string): BoardEdge {
  return { source: a, target: b, sourceHandle: FLOW_OUT, targetHandle: FLOW_IN };
}

describe("field references", () => {
  it("splits on the first dot and refuses a malformed reference", () => {
    expect(parseFieldRef("alpha.weight_kg")).toEqual({ table: "alpha", column: "weight_kg" });
    expect(parseFieldRef("no_dot")).toEqual({ table: "", column: "" });
    expect(parseFieldRef(".leading")).toEqual({ table: "", column: "" });
    expect(parseFieldRef("trailing.")).toEqual({ table: "", column: "" });
  });
});

describe("join lineage", () => {
  it("a joined cluster exposes every origin table explicitly", () => {
    const nodes = [dsNode(heats), dsNode(coils)];
    const edges = [joinEdge("alpha", "alpha_id", "beta", "alpha_id")];
    expect(joinCluster("alpha", nodes, edges)).toEqual(["alpha", "beta"]);

    const filter: BoardNode = { id: "f1", kind: "filter", data: { fieldRef: "beta.width_mm", op: ">", value: "1000" } };
    const all = nodes.concat([filter]);
    const wired = edges.concat([flowEdge("alpha", "f1")]);

    const visible = fieldsVisibleAt("f1", all, wired).map((f) => f.displayName);
    expect(visible).toContain("alpha.weight_kg");
    expect(visible).toContain("beta.width_mm");

    // The filter sits below a join and still resolves to ONE table.
    expect(blockProblem(filter, all, wired)).toBeNull();
  });

  it("a field that is not in the upstream output invalidates the block", () => {
    const nodes = [dsNode(heats)];
    const filter: BoardNode = { id: "f1", kind: "filter", data: { fieldRef: "beta.width_mm", op: ">", value: "1" } };
    const all = nodes.concat([filter]);
    const edges = [flowEdge("alpha", "f1")];
    const problem = blockProblem(filter, all, edges);
    expect(problem).not.toBeNull();
    expect(problem).toContain("beta.width_mm");
  });

  it("a block wired to nothing is refused, and the sentence says what to do", () => {
    const filter: BoardNode = { id: "f1", kind: "filter", data: { fieldRef: "alpha.weight_kg", op: ">", value: "1" } };
    const all = [dsNode(heats), filter];
    const problem = blockProblem(filter, all, []);
    expect(problem).toContain("is not connected");
  });
});

describe("filter block", () => {
  const base = [dsNode(heats)];
  const wire = [flowEdge("alpha", "f1")];

  it("refuses an operator outside the server whitelist", () => {
    const filter: BoardNode = { id: "f1", kind: "filter", data: { fieldRef: "alpha.weight_kg", op: "REGEXP", value: "x" } };
    expect(blockProblem(filter, base.concat([filter]), wire)).toContain("REGEXP");
  });

  it("requires a value for a binary operator and none for a unary one", () => {
    const empty: BoardNode = { id: "f1", kind: "filter", data: { fieldRef: "alpha.weight_kg", op: ">", value: "  " } };
    expect(blockProblem(empty, base.concat([empty]), wire)).toContain("needs a value");

    const unary: BoardNode = { id: "f1", kind: "filter", data: { fieldRef: "alpha.weight_kg", op: "IS NULL", value: "" } };
    expect(blockProblem(unary, base.concat([unary]), wire)).toBeNull();
  });

  it("serialises a unary filter with a null value", () => {
    const unary: BoardNode = { id: "f1", kind: "filter", data: { fieldRef: "alpha.weight_kg", op: "IS NULL", value: "" } };
    const graph = serialiseGraph("d", "MaterialUnit", base.concat([unary]), wire);
    expect(graph.filters).toEqual([{ table: "alpha", column: "weight_kg", op: "IS NULL", value: null }]);
  });
});

describe("derived block", () => {
  const base = [dsNode(heats)];
  const wire = [flowEdge("alpha", "d1")];

  it("refuses an alias that Ident would refuse on the server", () => {
    const bad: BoardNode = { id: "d1", kind: "derived", data: { alias: "net kg", leftRef: "alpha.weight_kg", op: "*", constant: "2" } };
    expect(blockProblem(bad, base.concat([bad]), wire)).toContain("legal column name");
  });

  it("needs a second column or a numeric constant", () => {
    const bad: BoardNode = { id: "d1", kind: "derived", data: { alias: "net_kg", leftRef: "alpha.weight_kg", op: "*", constant: "two" } };
    expect(blockProblem(bad, base.concat([bad]), wire)).toContain("numeric constant");
  });

  it("always states the right-hand table rather than relying on the server fallback", () => {
    const nodes = [dsNode(heats), dsNode(coils)];
    const derived: BoardNode = {
      id: "d1", kind: "derived",
      data: { alias: "ratio", leftRef: "alpha.weight_kg", op: "/", rightRef: "beta.width_mm" },
    };
    const all = nodes.concat([derived]);
    const edges = [joinEdge("alpha", "alpha_id", "beta", "alpha_id"), flowEdge("alpha", "d1")];
    const graph = serialiseGraph("d", "MaterialUnit", all, edges);
    expect(graph.derived).toEqual([{
      alias: "ratio", leftTable: "alpha", leftColumn: "weight_kg", op: "/",
      rightTable: "beta", rightColumn: "width_mm", constant: null,
    }]);
  });

  it("publishes its alias into the downstream schema, with no physical lineage", () => {
    const derived: BoardNode = { id: "d1", kind: "derived", data: { alias: "net_kg", leftRef: "alpha.weight_kg", op: "*", constant: "2" } };
    const tail: BoardNode = { id: "f1", kind: "filter", data: { fieldRef: "alpha.weight_kg", op: ">", value: "1" } };
    const all = base.concat([derived, tail]);
    const edges = [flowEdge("alpha", "d1"), flowEdge("d1", "f1")];
    const seen = fieldsVisibleAt("f1", all, edges);
    expect(seen.map((f) => f.displayName)).toEqual(["alpha.alpha_id", "alpha.weight_kg", "net_kg"]);
    const produced = seen[2];
    expect(produced.originKind).toBe("derived");
    expect(produced.sqlType).toBe("numeric");
    expect(produced.originTable).toBe("");
    expect(produced.originColumn).toBe("");
  });

  it("refuses a downstream block that addresses the alias, by name, rather than hiding it", () => {
    const derived: BoardNode = { id: "d1", kind: "derived", data: { alias: "net_kg", leftRef: "alpha.weight_kg", op: "*", constant: "2" } };
    const filter: BoardNode = { id: "f1", kind: "filter", data: { fieldRef: "net_kg", op: ">", value: "1" } };
    const all = base.concat([derived, filter]);
    const edges = [flowEdge("alpha", "d1"), flowEdge("d1", "f1")];
    const problem = blockProblem(filter, all, edges);
    expect(problem).toContain("net_kg");
    expect(problem).toContain("derived column");
    expect(problem).toContain("Move this Filter above");
    expect(() => serialiseGraph("d", "MaterialUnit", all, edges)).toThrow();
  });

  it("survives a Select below it, because the server appends it after the projection", () => {
    const derived: BoardNode = { id: "d1", kind: "derived", data: { alias: "net_kg", leftRef: "alpha.weight_kg", op: "*", constant: "2" } };
    const sel: BoardNode = { id: "s1", kind: "select", data: { chosen: ["alpha.alpha_id"] } };
    const tail: BoardNode = { id: "f1", kind: "filter", data: { fieldRef: "alpha.alpha_id", op: "IS NOT NULL", value: "" } };
    const all = base.concat([derived, sel, tail]);
    const edges = [flowEdge("alpha", "d1"), flowEdge("d1", "s1"), flowEdge("s1", "f1")];
    expect(fieldsVisibleAt("f1", all, edges).map((f) => f.displayName)).toEqual(["alpha.alpha_id", "net_kg"]);
    expect(blockProblem(tail, all, edges)).toBeNull();
  });
});

describe("select block", () => {
  const base = [dsNode(heats)];

  it("an empty Select block is refused with the same sentence the server uses", () => {
    const sel: BoardNode = { id: "s1", kind: "select", data: { chosen: [] } };
    const problem = blockProblem(sel, base.concat([sel]), [flowEdge("alpha", "s1")]);
    expect(problem).toContain("no columns chosen");
  });

  it("narrows what a downstream block can see", () => {
    const sel: BoardNode = { id: "s1", kind: "select", data: { chosen: ["alpha.alpha_id"] } };
    const filter: BoardNode = { id: "f1", kind: "filter", data: { fieldRef: "alpha.weight_kg", op: ">", value: "1" } };
    const all = base.concat([sel, filter]);
    const edges = [flowEdge("alpha", "s1"), flowEdge("s1", "f1")];
    expect(fieldsVisibleAt("f1", all, edges).map((f) => f.displayName)).toEqual(["alpha.alpha_id"]);
    expect(blockProblem(filter, all, edges)).toContain("alpha.weight_kg");
  });

  it("no Select block leaves selects undefined, which keeps SELECT * on the server", () => {
    const graph = serialiseGraph("d", "MaterialUnit", base, []);
    expect(graph.selects).toBeUndefined();
    expect(graph.filters).toBeUndefined();
    expect(graph.derived).toBeUndefined();
  });

  it("the last Select in the chain is the projection", () => {
    const first: BoardNode = { id: "s1", kind: "select", data: { chosen: ["alpha.alpha_id", "alpha.weight_kg"] } };
    const second: BoardNode = { id: "s2", kind: "select", data: { chosen: ["alpha.alpha_id"] } };
    const all = base.concat([first, second]);
    const edges = [flowEdge("alpha", "s1"), flowEdge("s1", "s2")];
    const graph = serialiseGraph("d", "MaterialUnit", all, edges);
    expect(graph.selects).toEqual([{ table: "alpha", column: "alpha_id" }]);
  });
});

describe("ordering and refusal", () => {
  it("applies blocks in chain order, not in node order", () => {
    const b2: BoardNode = { id: "f2", kind: "filter", data: { fieldRef: "alpha.weight_kg", op: "<", value: "9" } };
    const b1: BoardNode = { id: "f1", kind: "filter", data: { fieldRef: "alpha.weight_kg", op: ">", value: "1" } };
    // f2 is listed FIRST but wired SECOND.
    const all = [dsNode(heats), b2, b1];
    const edges = [flowEdge("alpha", "f1"), flowEdge("f1", "f2")];
    expect(orderedBlocks(all, edges).map((n) => n.id)).toEqual(["f1", "f2"]);
    const graph = serialiseGraph("d", "MaterialUnit", all, edges);
    expect(graph.filters?.map((f) => f.op)).toEqual([">", "<"]);
  });

  it("a flow loop terminates the walk instead of spinning", () => {
    const a: BoardNode = { id: "f1", kind: "filter", data: { fieldRef: "alpha.weight_kg", op: ">", value: "1" } };
    const b: BoardNode = { id: "f2", kind: "filter", data: { fieldRef: "alpha.weight_kg", op: "<", value: "9" } };
    const all = [dsNode(heats), a, b];
    const edges = [flowEdge("alpha", "f1"), flowEdge("f1", "f2"), flowEdge("f2", "f1")];
    expect(flowChainFrom("alpha", all, edges).map((n) => n.id)).toEqual(["f1", "f2"]);
  });

  it("an unjoined second table is reported, and serialisation refuses", () => {
    const all = [dsNode(heats), dsNode(coils)];
    expect(boardProblems(all, []).some((p) => p.indexOf("no join to the rest of the board") >= 0)).toBe(true);
    expect(() => serialiseGraph("d", "MaterialUnit", all, [])).toThrow();
  });

  it("a detached block never reaches the server", () => {
    const orphan: BoardNode = { id: "f9", kind: "filter", data: { fieldRef: "alpha.weight_kg", op: ">", value: "1" } };
    const all = [dsNode(heats), orphan];
    expect(orderedBlocks(all, [])).toEqual([]);
    expect(() => serialiseGraph("d", "MaterialUnit", all, [])).toThrow();
  });
});
describe("wiring refusal, the enumerated set of section 5.2.7", () => {
  const twoTables = [dsNode(heats), dsNode(coils)];
  const filter: BoardNode = { id: "f1", kind: "filter", data: { title: "Filter", fieldRef: "", op: "", value: "" } };

  function wire(a: string, ah: string | null, b: string, bh: string | null): ProposedWire {
    return { source: a, sourceHandle: ah, target: b, targetHandle: bh };
  }

  it("accepts a legal key-to-key join and a legal dataset wire", () => {
    expect(wiringRefusal(
      wire("alpha", COLUMN_OUT + "alpha_id", "beta", COLUMN_IN + "alpha_id"), twoTables, [],
    )).toBeNull();
    expect(wiringRefusal(
      wire("alpha", FLOW_OUT, "f1", FLOW_IN), twoTables.concat([filter]), [],
    )).toBeNull();
  });

  it("refuses a wire that does not land on a port", () => {
    expect(wiringRefusal(wire("alpha", null, "beta", null), twoTables, []))
      .toContain("must land on a port");
  });

  it("refuses a block wired to itself", () => {
    expect(wiringRefusal(wire("alpha", COLUMN_OUT + "alpha_id", "alpha", COLUMN_IN + "alpha_id"), twoTables, []))
      .toContain("cannot be wired to itself");
  });

  it("refuses rows into a column port, and says which end carries what", () => {
    const all = twoTables.concat([filter]);
    const problem = wiringRefusal(wire("alpha", FLOW_OUT, "beta", COLUMN_IN + "alpha_id"), all, []);
    expect(problem).toContain("carries rows");
    expect(problem).toContain("expects a single column");
  });

  it("refuses the same wire twice, in either orientation", () => {
    const existing = [joinEdge("alpha", "alpha_id", "beta", "alpha_id")];
    expect(wiringRefusal(wire("alpha", COLUMN_OUT + "alpha_id", "beta", COLUMN_IN + "alpha_id"), twoTables, existing))
      .toContain("already wired");
    expect(wiringRefusal(wire("beta", COLUMN_OUT + "alpha_id", "alpha", COLUMN_IN + "alpha_id"), twoTables, existing))
      .toContain("already wired");
  });

  it("refuses a join between a table and a block, and points at the dataset port", () => {
    const all = twoTables.concat([filter]);
    expect(wiringRefusal(wire("alpha", COLUMN_OUT + "alpha_id", "f1", COLUMN_IN + "x"), all, []))
      .toContain("through its dataset port");
  });

  it("refuses a type mismatch and names both types", () => {
    const problem = wiringRefusal(
      wire("alpha", COLUMN_OUT + "weight_kg", "beta", COLUMN_IN + "alpha_id"), twoTables, [],
    );
    // alpha_id is a key candidate, and a key may join a typed column, so the
    // mismatch has to be built from two NON-key columns of different types.
    expect(problem).toBeNull();

    const mixed = wiringRefusal(
      wire("beta", COLUMN_OUT + "width_mm", "alpha", COLUMN_IN + "text_note"), 
      [dsNode({ table: "alpha", source: "s", columns: [{ name: "text_note", sqlType: "text", isKeyCandidate: false }] }), dsNode(coils)],
      [],
    );
    expect(mixed).toContain("cannot be joined to a");
  });

  it("refuses a join that closes a loop, regardless of the direction it was dragged", () => {
    const existing = [joinEdge("alpha", "alpha_id", "beta", "alpha_id")];
    // The mirrored direction: T-032's directed check missed this one.
    const problem = wiringRefusal(
      wire("beta", COLUMN_OUT + "width_mm", "alpha", COLUMN_IN + "weight_kg"), twoTables, existing,
    );
    expect(problem).toContain("close a loop");
  });

  it("refuses a dataset wire into a table, because a table is a source", () => {
    const all = twoTables.concat([filter]);
    expect(wiringRefusal(wire("f1", FLOW_OUT, "alpha", FLOW_IN), all, []))
      .toContain("has no dataset input");
  });

  it("refuses a second dataset input on one block", () => {
    const all = twoTables.concat([filter]);
    const existing = [flowEdge("alpha", "f1")];
    expect(wiringRefusal(wire("beta", FLOW_OUT, "f1", FLOW_IN), all, existing))
      .toContain("already has a dataset input");
  });

  it("refuses a second chain from one block, because serialisation walks one", () => {
    const other: BoardNode = { id: "f2", kind: "filter", data: { title: "Second filter" } };
    const all = twoTables.concat([filter, other]);
    const existing = [flowEdge("alpha", "f1")];
    expect(wiringRefusal(wire("alpha", FLOW_OUT, "f2", FLOW_IN), all, existing))
      .toContain("already feeds");
  });

  it("refuses a dataset wire that closes a loop", () => {
    const other: BoardNode = { id: "f2", kind: "filter", data: { title: "Second filter" } };
    const all = twoTables.concat([filter, other]);
    const existing = [flowEdge("f1", "f2")];
    expect(wiringRefusal(wire("f2", FLOW_OUT, "f1", FLOW_IN), all, existing))
      .toContain("create a loop");
  });
});

describe("arrange", () => {
  const filter: BoardNode = { id: "f1", kind: "filter", data: { title: "Filter" } };
  const orphan: BoardNode = { id: "z9", kind: "select", data: { title: "Select columns" } };

  it("is deterministic: the same board arranges to the same placements twice", () => {
    const all = [dsNode(coils), dsNode(heats), filter];
    const edges = [flowEdge("alpha", "f1")];
    expect(arrangeBoard(all, edges)).toEqual(arrangeBoard(all, edges));
  });

  it("does not depend on the order the author dropped things", () => {
    const dropped = [dsNode(coils), dsNode(heats)];
    const other = [dsNode(heats), dsNode(coils)];
    expect(arrangeBoard(dropped, [])).toEqual(arrangeBoard(other, []));
  });

  it("puts tables down the left and each chain to the right of its table", () => {
    const all = [dsNode(heats), filter];
    const places = arrangeBoard(all, [flowEdge("alpha", "f1")]);
    const table = places.filter((p) => p.id === "alpha")[0];
    const block = places.filter((p) => p.id === "f1")[0];
    expect(block.x).toBeGreaterThan(table.x);
    expect(block.y).toBe(table.y);
  });

  it("parks a block that hangs from nothing below everything, not behind it", () => {
    const all = [dsNode(heats), orphan];
    const places = arrangeBoard(all, []);
    const table = places.filter((p) => p.id === "alpha")[0];
    const stranded = places.filter((p) => p.id === "z9")[0];
    expect(stranded.y).toBeGreaterThan(table.y);
    expect(places.length).toBe(2);
  });
});