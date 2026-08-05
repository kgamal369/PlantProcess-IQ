// PPIQ T-033 items 2 to 6. BOARD SEMANTICS.
//
// Ruling 5: the board is the source of authoring truth. Everything the server
// receives is DERIVED from nodes and edges here - there is no side form, and no
// second place a filter or a derived column can come from.
//
// This module is deliberately pure: no React, no network, no @xyflow types. It
// takes a structural view of the board so it can be unit tested headlessly, and
// so the shell can stay a rendering component.
//
// THE THREE THINGS IT OWNS
//   1. FIELD LINEAGE (item 5). Which fields a block can see, each one carrying
//      its origin table and column. A Filter below a Join therefore resolves to
//      exactly one table. Ruling 2: unresolvable lineage makes the block
//      INVALID. THE TABLE IS NEVER INFERRED.
//   2. VALIDITY. Every refusal is a sentence in the vocabulary of Chapter 4
//      section 5.2.7. A bare invalid flag with no sentence is a failure of the
//      specification.
//   3. SERIALISATION (item 6). nodes + edges -> the MapperGraph the server
//      already accepts.

import {
  FILTER_OPERATORS, MATH_OPERATORS, isUnaryFilterOperator,
  fieldDisplayName, hasResolvableLineage, type FieldLineage,
} from "./operatorContract";
import type {
  DerivedSpec, FilterSpec, MapperGraph, SelectSpec, StagedDataset,
} from "@/api/canvasApi";
// Port typing lives in the canvas layer and is pure, so the refusal set can
// use it without pulling any React into this module.
import { inferPortType, portsCompatible, type PortType } from "@/canvas/ports";

/**
 * A field as the BOARD sees it.
 *
 * FieldLineage, frozen in T-033a, describes a PHYSICAL field: origin table,
 * origin column, SQL type, display identity. A DERIVED column is a real field
 * of the downstream dataset - it has a name and a known type - but it has no
 * physical table and no physical column.
 *
 * Hiding it would be untruthful about the schema; inventing a table for it
 * would be the lineage guess ruling 2 forbids. So the board carries an ORIGIN
 * KIND alongside the lineage and a derived field STATES its absence:
 *
 *     name / type      known
 *     origin kind      derived
 *     physical table   none
 *     physical column  none
 *
 * hasResolvableLineage() is false for such a field, which is exactly what
 * turns every downstream use of it into a NAMED REFUSAL.
 */
export type FieldOriginKind = "physical" | "derived";

export interface BoardField extends FieldLineage {
  originKind: FieldOriginKind;
}

/**
 * The four operators a derived column may use all produce a number, so its
 * type is KNOWN and is stated rather than left blank.
 */
export const DERIVED_FIELD_TYPE = "numeric";

export type BoardNodeKind = "dataset" | "filter" | "derived" | "select";

/** A structural node. The shell maps React Flow nodes onto this shape. */
export interface BoardNode {
  id: string;
  kind: BoardNodeKind;
  data: Record<string, unknown>;
}

/** A structural edge. Handles carry the wiring vocabulary below. */
export interface BoardEdge {
  source: string;
  target: string;
  sourceHandle: string | null;
  targetHandle: string | null;
}

// THE WIRING VOCABULARY, in one place so the node components and the semantics
// cannot drift. A join wire lands on a COLUMN handle; a dataset wire lands on
// the FLOW handle. Two different wires with two different meanings, and the
// handle name is what tells them apart.
export const COLUMN_OUT = "out:";
export const COLUMN_IN = "in:";
export const FLOW_OUT = "flow:out";
export const FLOW_IN = "flow:in";

export function isJoinEdge(e: BoardEdge): boolean {
  return (e.sourceHandle ?? "").indexOf(COLUMN_OUT) === 0
      && (e.targetHandle ?? "").indexOf(COLUMN_IN) === 0;
}

export function isFlowEdge(e: BoardEdge): boolean {
  return e.sourceHandle === FLOW_OUT && e.targetHandle === FLOW_IN;
}

export function columnOfHandle(handle: string | null, prefix: string): string {
  const h = handle ?? "";
  if (h.indexOf(prefix) !== 0) { return ""; }
  return h.substring(prefix.length);
}

// ---------------------------------------------------------------- FIELD REFS

/**
 * A field reference as the interface stores it: "table.column". The table name
 * is validated by Ident() on the server and cannot contain a dot, so the FIRST
 * dot is the separator and splitting is unambiguous.
 *
 * An unparseable reference returns empty strings, which
 * hasResolvableLineage() then rejects. It is never repaired by guessing.
 */
export function parseFieldRef(ref: string): { table: string; column: string } {
  const at = (ref ?? "").indexOf(".");
  if (at <= 0 || at === ref.length - 1) { return { table: "", column: "" }; }
  return { table: ref.substring(0, at), column: ref.substring(at + 1) };
}

export function fieldsOfDataset(ds: StagedDataset): BoardField[] {
  return ds.columns.map((c) => ({
    originKind: "physical" as const,
    originTable: ds.table,
    originColumn: c.name,
    sqlType: c.sqlType,
    displayName: fieldDisplayName(ds.table, c.name),
    isKeyCandidate: c.isKeyCandidate,
  }));
}

/** The dataset payload a dataset node carries. */
function datasetOf(node: BoardNode): StagedDataset | null {
  const table = typeof node.data.table === "string" ? node.data.table : "";
  const source = typeof node.data.source === "string" ? node.data.source : "";
  const columns = Array.isArray(node.data.columns) ? node.data.columns : null;
  if (!table || !columns) { return null; }
  return { table, source, columns } as StagedDataset;
}

// ---------------------------------------------------------------- TOPOLOGY

export function nodeById(nodes: BoardNode[], id: string): BoardNode | null {
  for (const n of nodes) { if (n.id === id) { return n; } }
  return null;
}

/**
 * Every dataset reachable from one dataset through JOIN wires, in either
 * direction. This is the set of tables a downstream block can see columns of,
 * and it is why a Filter after a Join does not have to guess a table: the
 * cluster carries every origin explicitly.
 */
export function joinCluster(rootId: string, nodes: BoardNode[], edges: BoardEdge[]): string[] {
  const found: string[] = [];
  const seen = new Set<string>();
  const stack = [rootId];
  while (stack.length > 0) {
    const at = stack.pop() as string;
    if (seen.has(at)) { continue; }
    seen.add(at);
    const n = nodeById(nodes, at);
    if (!n || n.kind !== "dataset") { continue; }
    found.push(at);
    for (const e of edges) {
      if (!isJoinEdge(e)) { continue; }
      if (e.source === at) { stack.push(e.target); }
      if (e.target === at) { stack.push(e.source); }
    }
  }
  found.sort();
  return found;
}

/** The node feeding this block through its flow input, or null. */
export function flowParent(id: string, edges: BoardEdge[]): string | null {
  for (const e of edges) {
    if (isFlowEdge(e) && e.target === id) { return e.source; }
  }
  return null;
}

/**
 * The chain of blocks hanging off one dataset, nearest first. A flow loop is
 * impossible to serialise, so the walk stops rather than spinning; the loop is
 * reported as a problem by boardProblems.
 */
export function flowChainFrom(datasetId: string, nodes: BoardNode[], edges: BoardEdge[]): BoardNode[] {
  const chain: BoardNode[] = [];
  const seen = new Set<string>([datasetId]);
  let at = datasetId;
  for (;;) {
    let next: string | null = null;
    for (const e of edges) {
      if (isFlowEdge(e) && e.source === at) { next = e.target; break; }
    }
    if (next === null || seen.has(next)) { return chain; }
    const n = nodeById(nodes, next);
    if (!n || n.kind === "dataset") { return chain; }
    chain.push(n);
    seen.add(next);
    at = next;
  }
}

/** The dataset a block ultimately hangs from, or null when it hangs from nothing. */
export function rootDatasetOf(id: string, nodes: BoardNode[], edges: BoardEdge[]): string | null {
  const seen = new Set<string>();
  let at: string | null = id;
  while (at !== null) {
    if (seen.has(at)) { return null; }
    seen.add(at);
    const parent: string | null = flowParent(at, edges);
    if (parent === null) { return null; }
    const n = nodeById(nodes, parent);
    if (n && n.kind === "dataset") { return parent; }
    at = parent;
  }
  return null;
}

// ---------------------------------------------------------------- LINEAGE

/**
 * The field a Derived block produces, or null while it has no legal name yet.
 * No physical lineage is attached, because it has none.
 */
export function derivedFieldOf(node: BoardNode): BoardField | null {
  if (node.kind !== "derived") { return null; }
  const alias = str(node, "alias").trim();
  if (!alias || !IDENT.test(alias)) { return null; }
  return {
    originKind: "derived",
    originTable: "",
    originColumn: "",
    sqlType: DERIVED_FIELD_TYPE,
    displayName: alias,
  };
}

/**
 * The fields visible AT a node: the union of its join cluster's columns,
 * narrowed by every Select block above it, and EXTENDED by every Derived
 * block above it.
 *
 * A Derived block produces a field, so the downstream schema contains it -
 * with origin kind "derived" and no physical table or column. It is truthfully
 * present and it is not addressable, and blockProblem says so by name.
 */
export function fieldsVisibleAt(id: string, nodes: BoardNode[], edges: BoardEdge[]): BoardField[] {
  const node = nodeById(nodes, id);
  if (!node) { return []; }

  const rootId = node.kind === "dataset" ? node.id : rootDatasetOf(id, nodes, edges);
  if (rootId === null) { return []; }

  let pool: BoardField[] = [];
  for (const table of joinCluster(rootId, nodes, edges)) {
    const dsNode = nodeById(nodes, table);
    const ds = dsNode ? datasetOf(dsNode) : null;
    if (ds) { pool = pool.concat(fieldsOfDataset(ds)); }
  }
  if (node.kind === "dataset") { return pool; }

  for (const block of flowChainFrom(rootId, nodes, edges)) {
    if (block.id === id) { break; }
    if (block.kind === "select") {
      const chosen = chosenOf(block);
      // A DERIVED FIELD SURVIVES A SELECT, and this is not a convenience.
      // BuildSafeSelect emits the projection and then APPENDS one column per
      // derived expression, so the derived column is in the result whether or
      // not it was ticked. Dropping it from the pool here would describe an
      // output the server does not produce.
      pool = pool.filter((f) => f.originKind === "derived" || chosen.indexOf(f.displayName) >= 0);
      continue;
    }
    if (block.kind === "derived") {
      const produced = derivedFieldOf(block);
      if (produced) { pool = pool.concat([produced]); }
    }
  }
  return pool;
}

export function findField(pool: BoardField[], ref: string): BoardField | null {
  for (const f of pool) { if (f.displayName === ref) { return f; } }
  return null;
}

// ---------------------------------------------------------------- BLOCK DATA

export function titleOf(node: BoardNode): string {
  const t = typeof node.data.title === "string" ? node.data.title : "";
  if (t) { return t; }
  if (node.kind === "filter") { return "Filter"; }
  if (node.kind === "derived") { return "Derived column"; }
  if (node.kind === "select") { return "Select columns"; }
  return node.id;
}

function str(node: BoardNode, key: string): string {
  const v = node.data[key];
  return typeof v === "string" ? v : "";
}

export function chosenOf(node: BoardNode): string[] {
  const v = node.data.chosen;
  if (!Array.isArray(v)) { return []; }
  return v.filter((x): x is string => typeof x === "string");
}

const IDENT = /^[A-Za-z0-9_]+$/;

/**
 * The refusal for addressing a derived field where the server needs a table.
 * It names the field, states why it cannot be used, and says what to do -
 * section 5.2.8 asks for all three. Returns null when the field is physical.
 */
function derivedRefusal(title: string, field: BoardField | null, remedy: string): string | null {
  if (!field || field.originKind !== "derived") { return null; }
  return title + ": " + field.displayName
    + " is a derived column. It has a name and a type but no table,"
    + " so this release cannot address it. "
    + remedy;
}

// ---------------------------------------------------------------- VALIDITY

/**
 * The sentence explaining why this block cannot run, or null. Section 5.2.8:
 * an Error must state which block, what rule was broken, and what would fix it.
 */
export function blockProblem(node: BoardNode, nodes: BoardNode[], edges: BoardEdge[]): string | null {
  if (node.kind === "dataset") { return null; }

  const title = titleOf(node);
  if (rootDatasetOf(node.id, nodes, edges) === null) {
    return title + " cannot run: its dataset input is not connected. Wire a table, or another block, into its left port.";
  }
  const pool = fieldsVisibleAt(node.id, nodes, edges);

  if (node.kind === "filter") {
    const ref = str(node, "fieldRef");
    const field = findField(pool, ref);
    const derivedHere = derivedRefusal(title, field,
      "Move this Filter above the derived column, or filter on the columns it is built from.");
    if (derivedHere) { return derivedHere; }
    if (!hasResolvableLineage(field)) {
      return title + ": " + (ref ? ref + " is not in the output of the block above it." : "no column is chosen. Pick one from the dataset above it.");
    }
    const op = str(node, "op");
    if ((FILTER_OPERATORS as readonly string[]).indexOf(op) < 0) {
      return title + ": " + (op ? op + " is not an operator this product can compile." : "no comparison is chosen.");
    }
    if (!isUnaryFilterOperator(op) && str(node, "value").trim() === "") {
      return title + " needs a value for operator " + op + ".";
    }
    return null;
  }

  if (node.kind === "derived") {
    const alias = str(node, "alias").trim();
    if (!alias) { return title + ": the new column has no name. Give it one."; }
    if (!IDENT.test(alias)) {
      return title + ": " + alias + " is not a legal column name. Use letters, digits and underscore only.";
    }
    const left = findField(pool, str(node, "leftRef"));
    const derivedLeft = derivedRefusal(title, left,
      "Use the columns it is built from as the first operand.");
    if (derivedLeft) { return derivedLeft; }
    if (!hasResolvableLineage(left)) {
      return title + ": the first operand is not in the output of the block above it.";
    }
    const op = str(node, "op");
    if ((MATH_OPERATORS as readonly string[]).indexOf(op) < 0) {
      return title + ": " + (op ? op + " is not permitted in a derived column." : "no operation is chosen.");
    }
    const rightRef = str(node, "rightRef");
    if (rightRef) {
      const right = findField(pool, rightRef);
      const derivedRight = derivedRefusal(title, right,
        "Use the columns it is built from as the second operand.");
      if (derivedRight) { return derivedRight; }
      if (!hasResolvableLineage(right)) {
        return title + ": the second operand is not in the output of the block above it.";
      }
      return null;
    }
    const constant = str(node, "constant").trim();
    if (constant === "" || isNaN(Number(constant))) {
      return title + " needs a second column or a numeric constant.";
    }
    return null;
  }

  // select
  const chosen = chosenOf(node);
  if (chosen.length === 0) {
    return title + " has no columns chosen. Choose at least one column, or remove the block.";
  }
  for (const ref of chosen) {
    const picked = findField(pool, ref);
    const derivedPick = derivedRefusal(title, picked,
      "It is added to the output after the selected columns, so it does not need to be listed here.");
    if (derivedPick) { return derivedPick; }
    if (!hasResolvableLineage(picked)) {
      return title + ": " + ref + " is not in the output of the block above it.";
    }
  }
  return null;
}

/**
 * Every problem on the board, in node order. An empty array means the graph
 * can be serialised and run.
 */
export function boardProblems(nodes: BoardNode[], edges: BoardEdge[]): string[] {
  const out: string[] = [];
  const datasets = nodes.filter((n) => n.kind === "dataset");
  if (datasets.length === 0) {
    out.push("Nothing on the board yet. Add a source from the schema tree.");
    return out;
  }
  if (datasets.length > 1) {
    const joined = new Set<string>();
    for (const e of edges) {
      if (!isJoinEdge(e)) { continue; }
      joined.add(e.source); joined.add(e.target);
    }
    for (const d of datasets) {
      if (!joined.has(d.id)) {
        out.push(d.id + " has no join to the rest of the board. Wire a column of it to a column of another table, or remove it.");
      }
    }
  }
  for (const n of nodes) {
    const p = blockProblem(n, nodes, edges);
    if (p) { out.push(p); }
  }
  return out;
}

// ---------------------------------------------------------------- ORDERING

/**
 * Every block on the board, in the order its chain applies it. Blocks hanging
 * from nothing are omitted here and reported by boardProblems, so a detached
 * block can never silently reach the server.
 */
export function orderedBlocks(nodes: BoardNode[], edges: BoardEdge[]): BoardNode[] {
  const out: BoardNode[] = [];
  const seen = new Set<string>();
  for (const d of nodes) {
    if (d.kind !== "dataset") { continue; }
    for (const b of flowChainFrom(d.id, nodes, edges)) {
      if (seen.has(b.id)) { continue; }
      seen.add(b.id);
      out.push(b);
    }
  }
  return out;
}

// ---------------------------------------------------------------- SERIALISE

/**
 * The board, and only the board, produces the definition (ruling 5).
 *
 * REFUSES rather than emitting a partial definition: if any block is invalid,
 * this throws with the first sentence, because a graph that compiles to SQL
 * missing one of its filters is worse than one that does not compile at all.
 * The shell calls boardProblems first and never reaches the throw.
 */
export function serialiseGraph(
  name: string,
  targetEntity: string,
  nodes: BoardNode[],
  edges: BoardEdge[],
): MapperGraph {
  const problems = boardProblems(nodes, edges);
  if (problems.length > 0) { throw new Error(problems[0]); }

  const tables = nodes.filter((n) => n.kind === "dataset").map((n) => n.id);

  const joins = edges.filter(isJoinEdge).map((e) => ({
    leftTable: e.source,
    leftColumn: columnOfHandle(e.sourceHandle, COLUMN_OUT),
    rightTable: e.target,
    rightColumn: columnOfHandle(e.targetHandle, COLUMN_IN),
  }));

  const filters: FilterSpec[] = [];
  const derived: DerivedSpec[] = [];
  let selects: SelectSpec[] | undefined;

  for (const b of orderedBlocks(nodes, edges)) {
    if (b.kind === "filter") {
      const f = parseFieldRef(str(b, "fieldRef"));
      const op = str(b, "op");
      filters.push({
        table: f.table, column: f.column, op,
        value: isUnaryFilterOperator(op) ? null : str(b, "value"),
      });
      continue;
    }
    if (b.kind === "derived") {
      const l = parseFieldRef(str(b, "leftRef"));
      const rightRef = str(b, "rightRef");
      const r = rightRef ? parseFieldRef(rightRef) : null;
      derived.push({
        alias: str(b, "alias").trim(),
        leftTable: l.table, leftColumn: l.column,
        op: str(b, "op"),
        // Explicit on BOTH sides. The server would fall back to the left table
        // when rightTable is blank; ruling 2 says the table is never inferred,
        // so the board always states it and never relies on that fallback.
        rightTable: r ? r.table : null,
        rightColumn: r ? r.column : null,
        constant: r ? null : str(b, "constant").trim(),
      });
      continue;
    }
    // select. The LAST Select in the chain is the projection: each Select can
    // only choose fields its upstream still exposes, so a chain narrows and the
    // final one is what reaches the output.
    selects = chosenOf(b).map((ref) => {
      const p = parseFieldRef(ref);
      return { table: p.table, column: p.column };
    });
  }

  const graph: MapperGraph = { name, targetEntity, tables, joins };
  if (filters.length > 0) { graph.filters = filters; }
  if (derived.length > 0) { graph.derived = derived; }
  // undefined means NO Select block, which keeps SELECT * on the server. An
  // empty Select block is caught by boardProblems above and never gets here.
  if (selects !== undefined) { graph.selects = selects; }
  return graph;
}
// ============================================================================
// T-033 item 9. ILLEGAL WIRING IS REFUSED AT DRAG TIME.
//
// Chapter 4 section 5.2.7: "This is the clause that separates a professional
// tool from a toy." A wire that is not legal is rejected at drag time WITH A
// STATED REASON. It never lands, and it is never left to fail at run time.
//
// The set is implemented here rather than inside an event handler so every
// entry can be tested without a browser, and so one function - not two - owns
// what is legal. Every branch returns a SENTENCE. A bare refusal with no
// sentence is a failure of the specification.
//
// TWO WIRE FAMILIES, and confusing them is itself one of the refusals:
//   COLUMN wire   out:<column> to in:<column>, between two TABLES. A join.
//   DATASET wire  flow:out to flow:in. Rows flowing into a relational block.
// ============================================================================

export interface ProposedWire {
  source: string;
  target: string;
  sourceHandle: string | null;
  targetHandle: string | null;
}

type HandleKind = "column" | "flow" | "none";

function handleKind(h: string | null): HandleKind {
  const s = h ?? "";
  if (s === FLOW_IN || s === FLOW_OUT) { return "flow"; }
  if (s.indexOf(COLUMN_OUT) === 0 || s.indexOf(COLUMN_IN) === 0) { return "column"; }
  return "none";
}

function portTypeOfNode(node: BoardNode, column: string): PortType | null {
  const ds = datasetOf(node);
  if (!ds) { return null; }
  for (const c of ds.columns) {
    if (c.name === column) { return c.isKeyCandidate ? "key" : inferPortType(c.sqlType); }
  }
  return null;
}

/**
 * Connected through JOIN wires, IGNORING DIRECTION.
 *
 * The T-032 check followed join edges from source to target only, so a loop
 * closed against the direction the author happened to drag in was not caught.
 * t0 JOIN t1 is the same relationship read either way, so connectivity is
 * undirected here. This refuses strictly more than T-032 did, and everything
 * extra it refuses genuinely would have produced a join graph with more than
 * one path between two tables.
 */
function joinConnected(a: string, b: string, edges: BoardEdge[]): boolean {
  const seen = new Set<string>();
  const stack = [a];
  while (stack.length > 0) {
    const at = stack.pop() as string;
    if (at === b) { return true; }
    if (seen.has(at)) { continue; }
    seen.add(at);
    for (const e of edges) {
      if (!isJoinEdge(e)) { continue; }
      if (e.source === at) { stack.push(e.target); }
      if (e.target === at) { stack.push(e.source); }
    }
  }
  return false;
}

/** Reachable by following DATASET wires forward. Direction matters here. */
function flowReaches(from: string, to: string, edges: BoardEdge[]): boolean {
  const seen = new Set<string>();
  const stack = [from];
  while (stack.length > 0) {
    const at = stack.pop() as string;
    if (at === to) { return true; }
    if (seen.has(at)) { continue; }
    seen.add(at);
    for (const e of edges) {
      if (isFlowEdge(e) && e.source === at) { stack.push(e.target); }
    }
  }
  return false;
}

function flowChildOf(id: string, edges: BoardEdge[]): string | null {
  for (const e of edges) {
    if (isFlowEdge(e) && e.source === id) { return e.target; }
  }
  return null;
}

function nameOf(node: BoardNode): string {
  return node.kind === "dataset" ? node.id : titleOf(node);
}

/**
 * The sentence explaining why this wire is refused, or null when it is legal.
 * The order of the checks is the order an author meets them.
 */
export function wiringRefusal(w: ProposedWire, nodes: BoardNode[], edges: BoardEdge[]): string | null {
  const src = nodeById(nodes, w.source);
  const tgt = nodeById(nodes, w.target);
  if (!src || !tgt) {
    return "Both ends of a wire must be a block that is on the board.";
  }
  if (w.source === w.target) {
    return nameOf(src) + " cannot be wired to itself.";
  }

  const sk = handleKind(w.sourceHandle);
  const tk = handleKind(w.targetHandle);
  if (sk === "none" || tk === "none") {
    return "Both ends of a wire must land on a port, not on the body of a block.";
  }
  if (sk !== tk) {
    const carries = sk === "flow" ? "rows" : "a single column";
    const expects = tk === "flow" ? "rows" : "a single column";
    return "A wire from " + nameOf(src) + " carries " + carries + "; "
      + nameOf(tgt) + " expects " + expects + ".";
  }

  for (const e of edges) {
    const same = e.source === w.source && e.target === w.target
      && e.sourceHandle === w.sourceHandle && e.targetHandle === w.targetHandle;
    const mirrored = sk === "column" && e.source === w.target && e.target === w.source
      && e.sourceHandle === COLUMN_OUT + columnOfHandle(w.targetHandle, COLUMN_IN)
      && e.targetHandle === COLUMN_IN + columnOfHandle(w.sourceHandle, COLUMN_OUT);
    if (same || mirrored) {
      return nameOf(src) + " and " + nameOf(tgt) + " are already wired that way.";
    }
  }

  if (sk === "column") {
    if (src.kind !== "dataset" || tgt.kind !== "dataset") {
      const block = src.kind === "dataset" ? tgt : src;
      return "Only tables are joined column to column. Wire " + nameOf(block)
        + " through its dataset port instead.";
    }
    const leftColumn = columnOfHandle(w.sourceHandle, COLUMN_OUT);
    const rightColumn = columnOfHandle(w.targetHandle, COLUMN_IN);
    const leftType = portTypeOfNode(src, leftColumn);
    const rightType = portTypeOfNode(tgt, rightColumn);
    if (leftType && rightType && !portsCompatible(leftType, rightType)) {
      return src.id + "." + leftColumn + " is " + leftType + "; "
        + tgt.id + "." + rightColumn + " is " + rightType + ". "
        + "A " + leftType + " column cannot be joined to a " + rightType + " column.";
    }
    if (joinConnected(w.source, w.target, edges)) {
      return "That join would close a loop between " + src.id + " and " + tgt.id
        + ". A join path has to stay a tree so the result has one meaning.";
    }
    return null;
  }

  // dataset wire
  if (w.sourceHandle !== FLOW_OUT || w.targetHandle !== FLOW_IN) {
    return "A dataset wire runs from the output port on the right of one block "
      + "to the input port on the left of the next.";
  }
  if (tgt.kind === "dataset") {
    return nameOf(tgt) + " is a table, and a table is a source. It has no dataset input.";
  }
  if (flowParent(w.target, edges) !== null) {
    return nameOf(tgt) + " already has a dataset input. Remove that wire first, "
      + "or wire this one into a different block.";
  }
  // ONE CHAIN PER SOURCE. serialiseGraph walks a single chain, so a second
  // chain hanging off the same block would be silently dropped from the
  // definition - which is worse than refusing it here with a reason.
  const existingChild = flowChildOf(w.source, edges);
  if (existingChild !== null) {
    const child = nodeById(nodes, existingChild);
    return nameOf(src) + " already feeds " + (child ? nameOf(child) : existingChild)
      + ". A definition has one chain, so wire this block after that one instead.";
  }
  if (flowReaches(w.target, w.source, edges)) {
    return "This wire would create a loop: " + nameOf(src) + " -> " + nameOf(tgt)
      + " -> " + nameOf(src) + ".";
  }
  return null;
}

// ============================================================================
// T-033 item 8. ARRANGE.
//
// Ruling 7: the smallest deterministic arrangement appropriate to the existing
// canvas, NOT an auto-layout subsystem. So: tables down the left in a stable
// order, each block chain laid out to the right of the table it hangs from, and
// anything hanging from nothing parked below where it can be seen rather than
// hidden behind a node.
//
// DETERMINISTIC MEANS DETERMINISTIC. The order is by node id, never by the
// order the author happened to drop things, so pressing Arrange twice cannot
// produce two different boards.
// ============================================================================

export interface BoardPlacement {
  id: string;
  x: number;
  y: number;
}

const ARRANGE_X0 = 80;
const ARRANGE_Y0 = 90;
const ARRANGE_COLUMN = 300;
const ARRANGE_ROW = 190;

export function arrangeBoard(nodes: BoardNode[], edges: BoardEdge[]): BoardPlacement[] {
  const placements: BoardPlacement[] = [];
  const placed = new Set<string>();

  const datasets = nodes.filter((n) => n.kind === "dataset").map((n) => n.id).sort();
  let row = 0;
  for (const dsId of datasets) {
    placements.push({ id: dsId, x: ARRANGE_X0, y: ARRANGE_Y0 + row * ARRANGE_ROW });
    placed.add(dsId);
    let column = 1;
    for (const block of flowChainFrom(dsId, nodes, edges)) {
      placements.push({
        id: block.id,
        x: ARRANGE_X0 + column * ARRANGE_COLUMN,
        y: ARRANGE_Y0 + row * ARRANGE_ROW,
      });
      placed.add(block.id);
      column = column + 1;
    }
    row = row + 1;
  }

  const stranded = nodes.filter((n) => !placed.has(n.id)).map((n) => n.id).sort();
  for (const id of stranded) {
    placements.push({ id, x: ARRANGE_X0, y: ARRANGE_Y0 + row * ARRANGE_ROW });
    row = row + 1;
  }

  return placements;
}