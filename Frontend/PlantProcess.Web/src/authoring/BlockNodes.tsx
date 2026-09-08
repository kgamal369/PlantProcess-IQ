// PPIQ T-033 items 2 to 4. THE THREE RELATIONAL BOARD NODES.
//
// Chapter 4 section 5.2.5 group 2 puts Filter, Select columns and Derived
// column on the BOARD. Ruling 4 of T-033 limits this task to exactly these
// three: no Rename, Group by, Sort, Union, Cast or Lookup.
//
// SECTION 5.2.6 IS THE CONTRACT THESE COMPONENTS MEET:
//   Title        the user's name for the step
//   Subtitle     the block type
//   Status badge ON THE NODE, not only in a problems list
//   Ports        typed, coloured, labelled
//   Inspector    typed controls FED FROM LIVE SCHEMA - a column is a dropdown
//                of real fields, never free text
//
// The dropdowns are built from the field lineage the shell computes with
// graphSemantics.fieldsVisibleAt, so a column that is not in the upstream
// output cannot be chosen in the first place. Section 5.2.7 calls an illegal
// state that is rejected afterwards a weaker product than one that is
// unreachable, and this is where that principle is applied.
//
// NO RAW CONTROLS. Every input is a Standard primitive, because these files
// are inside the same no-raw-control ratchet the shell passes. There is no
// checkbox primitive in the standard set, so the Select block's column
// checklist is a row of toggle buttons carrying aria-pressed, which is a
// truthful accessible checklist rather than a raw input smuggled in.

import { Handle, Position, type Node, type NodeProps } from "@xyflow/react";
import { StandardP2Button, StandardP2Input, StandardP2Select } from "@/components/standard/StandardP2Controls";
import { FILTER_OPERATORS, MATH_OPERATORS, isUnaryFilterOperator } from "./operatorContract";
import { FLOW_IN, FLOW_OUT, type BoardField, type BoardNodeKind } from "./graphSemantics";
import {
  describePortType, describeSignature,
  type ParameterSpec, type PortSpec, type PortTypeRef,
} from "./blockParameters";
import { BLOCK_REGISTRY, contractForKind, titleForKind } from "./blockRegistry";

/** What every board block carries. `problem` is the sentence, or null. */
export interface BlockCommonData {
  title: string;
  fields: BoardField[];
  problem: string | null;
  onChange?: (nodeId: string, key: string, value: string) => void;
  [key: string]: unknown;
}

export interface FilterNodeData extends BlockCommonData {
  fieldRef: string;
  op: string;
  value: string;
}

export interface DerivedNodeData extends BlockCommonData {
  alias: string;
  leftRef: string;
  op: string;
  rightRef: string;
  constant: string;
}

export interface SelectNodeData extends BlockCommonData {
  chosen: string[];
  onToggle?: (nodeId: string, ref: string) => void;
}

type FilterNodeType = Node<FilterNodeData, "filter">;
type DerivedNodeType = Node<DerivedNodeData, "derived">;
type SelectNodeType = Node<SelectNodeData, "select">;

function shellClass(problem: string | null): string {
  return "blk-node" + (problem ? " blk-node--error" : " blk-node--ok");
}

/** Section 5.2.6: the status badge is ON THE NODE, with its sentence beside it. */
function NodeStatus({ problem, testId }: { problem: string | null; testId: string }) {
  return (
    <div className="blk-node__status" data-testid={testId}>
      <span className={"blk-node__badge" + (problem ? " blk-node__badge--error" : " blk-node__badge--ok")}>
        {problem ? "Error" : "OK"}
      </span>
      {problem ? <span className="blk-node__problem">{problem}</span> : null}
    </div>
  );
}

// A DERIVED FIELD IS LISTED AND IS NOT CHOOSABLE, and both halves matter.
// Listing it is truthful: the block above really does produce it. Disabling
// it keeps the illegal state UNREACHABLE rather than rejected afterwards,
// which is what section 5.2.7 asks for. The label says why, so the author is
// not left guessing at a greyed-out row.
function FieldOptions({ fields, placeholder }: { fields: BoardField[]; placeholder: string }) {
  return (
    <>
      <option value="">{placeholder}</option>
      {fields.map((f) => (
        <option key={f.displayName} value={f.displayName} disabled={f.originKind === "derived"}>
          {f.displayName}{f.originKind === "derived" ? " - derived, no table to address" : ""}
        </option>
      ))}
    </>
  );
}

export function FilterNode({ id, data }: NodeProps<FilterNodeType>) {
  const fields = data.fields ?? [];
  const unary = isUnaryFilterOperator(data.op ?? "");
  return (
    <div className={shellClass(data.problem)} data-testid={"filter-node-" + id}>
      <Handle type="target" position={Position.Left} id={FLOW_IN} className="ppiq-port ppiq-port--flow" />
      <div className="blk-node__kind">Filter</div>
      <div className="blk-node__title">{data.title}</div>

      <StandardP2Select
        className="blk-node__field"
        aria-label="Filter column"
        value={data.fieldRef ?? ""}
        onChange={(e) => data.onChange?.(id, "fieldRef", e.target.value)}
      >
        <FieldOptions fields={fields} placeholder="choose a column" />
      </StandardP2Select>

      <StandardP2Select
        className="blk-node__field"
        aria-label="Comparison"
        value={data.op ?? ""}
        onChange={(e) => data.onChange?.(id, "op", e.target.value)}
      >
        <option value="">choose a comparison</option>
        {FILTER_OPERATORS.map((o) => <option key={o} value={o}>{o}</option>)}
      </StandardP2Select>

      {/* An operator the server treats as unary takes no value, so the field is
          ABSENT rather than disabled: a control that cannot be used and is
          still on screen invites the author to try. */}
      {!unary && (
        <StandardP2Input
          className="blk-node__field"
          aria-label="Value"
          value={data.value ?? ""}
          onChange={(e) => data.onChange?.(id, "value", e.target.value)}
        />
      )}

      <NodeStatus problem={data.problem} testId={"filter-status-" + id} />
      <Handle type="source" position={Position.Right} id={FLOW_OUT} className="ppiq-port ppiq-port--flow" />
    </div>
  );
}

export function DerivedNode({ id, data }: NodeProps<DerivedNodeType>) {
  const fields = data.fields ?? [];
  const usesColumn = Boolean(data.rightRef);
  return (
    <div className={shellClass(data.problem)} data-testid={"derived-node-" + id}>
      <Handle type="target" position={Position.Left} id={FLOW_IN} className="ppiq-port ppiq-port--flow" />
      <div className="blk-node__kind">Derived column</div>
      <div className="blk-node__title">{data.title}</div>

      <StandardP2Input
        className="blk-node__field"
        aria-label="New column name"
        value={data.alias ?? ""}
        onChange={(e) => data.onChange?.(id, "alias", e.target.value)}
      />

      <StandardP2Select
        className="blk-node__field"
        aria-label="First operand"
        value={data.leftRef ?? ""}
        onChange={(e) => data.onChange?.(id, "leftRef", e.target.value)}
      >
        <FieldOptions fields={fields} placeholder="choose a column" />
      </StandardP2Select>

      <StandardP2Select
        className="blk-node__field"
        aria-label="Operation"
        value={data.op ?? ""}
        onChange={(e) => data.onChange?.(id, "op", e.target.value)}
      >
        <option value="">choose an operation</option>
        {MATH_OPERATORS.map((o) => <option key={o} value={o}>{o}</option>)}
      </StandardP2Select>

      <StandardP2Select
        className="blk-node__field"
        aria-label="Second operand"
        value={data.rightRef ?? ""}
        onChange={(e) => data.onChange?.(id, "rightRef", e.target.value)}
      >
        <FieldOptions fields={fields} placeholder="use a number instead" />
      </StandardP2Select>

      {/* One or the other, never both on screen at once. The server refuses a
          derived column that has neither, and the board says so before it is
          sent rather than after. */}
      {!usesColumn && (
        <StandardP2Input
          className="blk-node__field"
          aria-label="Number"
          value={data.constant ?? ""}
          onChange={(e) => data.onChange?.(id, "constant", e.target.value)}
        />
      )}

      <NodeStatus problem={data.problem} testId={"derived-status-" + id} />
      <Handle type="source" position={Position.Right} id={FLOW_OUT} className="ppiq-port ppiq-port--flow" />
    </div>
  );
}

export function SelectNode({ id, data }: NodeProps<SelectNodeType>) {
  const fields = data.fields ?? [];
  const chosen = data.chosen ?? [];
  return (
    <div className={shellClass(data.problem)} data-testid={"select-node-" + id}>
      <Handle type="target" position={Position.Left} id={FLOW_IN} className="ppiq-port ppiq-port--flow" />
      <div className="blk-node__kind">Select columns</div>
      <div className="blk-node__title">{data.title}</div>

      <div className="blk-node__checklist" role="group" aria-label="Columns to keep">
        {fields.length === 0 ? (
          <span className="blk-node__empty">Wire a dataset into this block to choose its columns.</span>
        ) : fields.map((f) => {
          const on = chosen.indexOf(f.displayName) >= 0;
          // A derived field cannot be listed in a qualified projection, and it
          // does not need to be: the server appends it after the selected
          // columns either way. The chip states that instead of vanishing.
          const isDerived = f.originKind === "derived";
          return (
            <StandardP2Button
              key={f.displayName}
              type="button"
              variant={on ? "primary" : "ghost"}
              className="blk-node__chip"
              disabled={isDerived}
              aria-pressed={isDerived ? undefined : on}
              title={isDerived ? "Derived column - always added to the output after the selected columns." : undefined}
              onClick={() => data.onToggle?.(id, f.displayName)}
            >
              {f.displayName}{isDerived ? " - derived" : ""}
            </StandardP2Button>
          );
        })}
      </div>

      <NodeStatus problem={data.problem} testId={"select-status-" + id} />
      <Handle type="source" position={Position.Right} id={FLOW_OUT} className="ppiq-port ppiq-port--flow" />
    </div>
  );
}

// ============================================================================
// T-242 Stage 4. ONE NODE FOR EVERY EXECUTABLE FAMILY.
//
// The three blocks above are bespoke because each is genuinely different: they
// are fed from LIVE SCHEMA, so their controls are dropdowns of the real columns
// an upstream dataset happens to expose. Nothing about that generalises.
//
// The executable families are the opposite. Each is fully described by its
// contract - the parameters it requires and the typed ports it exposes - so
// nine bespoke components would be nine copies of one component with a
// different literal in each, and nine places for a signature to drift. One
// renderer reads the contract. A new family is a registry row and a schema,
// not another component here.
// ============================================================================

function portTypeClass(ref: PortTypeRef): string {
  // A type variable has no single concrete colour. The neutral key colour is
  // used until wiring resolves the variable; the typed title still states the
  // accepted semantic type.
  const type = "concrete" in ref ? ref.concrete : "key";
  return "ppiq-port--" + type;
}

function portSlotClass(index: number, total: number): string {
  // Stage 4 families expose at most three inputs. Position belongs in the
  // stylesheet, not in an inline React style object: the authoring shell's
  // conformance ratchet deliberately keeps inline styling at zero.
  return "ppiq-family-port--" + String(total) + "-" + String(index);
}

function FamilyPort({ spec, index, total, side }: {
  spec: PortSpec; index: number; total: number; side: "in" | "out";
}) {
  return (
    <Handle
      type={side === "in" ? "target" : "source"}
      position={side === "in" ? Position.Left : Position.Right}
      id={spec.id}
      className={"ppiq-port " + portTypeClass(spec.type) + " " + portSlotClass(index, total)}
      title={spec.label + ": " + describePortType(spec.type)}
    />
  );
}

function FamilyParameter({ nodeId, spec, value, onChange }: {
  nodeId: string;
  spec: ParameterSpec;
  value: unknown;
  onChange?: (nodeId: string, key: string, value: string) => void;
}) {
  const current = value === undefined || value === null ? "" : String(value);
  if (spec.control.kind === "choice") {
    return (
      <StandardP2Select
        className="blk-node__field"
        aria-label={spec.label}
        value={current}
        onChange={(e) => onChange?.(nodeId, spec.key, e.target.value)}
      >
        {/* The empty option is not a default. Nothing is chosen until the
            author chooses it, and the block stays invalid until then. */}
        <option value="">choose {spec.label.toLowerCase()}</option>
        {spec.control.options.map((o) => <option key={o} value={o}>{o}</option>)}
      </StandardP2Select>
    );
  }
  return (
    <StandardP2Input
      className="blk-node__field"
      aria-label={spec.label}
      value={current}
      onChange={(e) => onChange?.(nodeId, spec.key, e.target.value)}
    />
  );
}

export type FamilyNodeData = BlockCommonData;
type FamilyNodeType = Node<FamilyNodeData, string>;

export function FamilyNode({ id, type, data }: NodeProps<FamilyNodeType>) {
  const kind = (type ?? "") as BoardNodeKind;
  const contract = contractForKind(kind);
  const signature = contract ? contract.ports(data) : null;
  const inputs = signature ? signature.inputs : [];

  return (
    <div className={shellClass(data.problem)} data-testid={"family-node-" + id} data-kind={kind}>
      {inputs.map((spec, i) => (
        <FamilyPort key={spec.id} spec={spec} index={i} total={inputs.length} side="in" />
      ))}

      <div className="blk-node__kind">{titleForKind(kind)}</div>
      <div className="blk-node__title">{data.title}</div>

      {/* DERIVED from the ports rendered above it, so the node cannot describe
          something its contract does not actually do. */}
      {signature ? (
        <div className="blk-node__signature" data-testid={"family-signature-" + id}>
          Takes {describeSignature(signature)}.
        </div>
      ) : null}

      {contract ? contract.parameters.map((spec) => (
        <FamilyParameter
          key={spec.key}
          nodeId={id}
          spec={spec}
          value={data[spec.key]}
          onChange={data.onChange}
        />
      )) : null}

      <NodeStatus problem={data.problem} testId={"family-status-" + id} />

      {signature && signature.output ? (
        <FamilyPort spec={signature.output} index={0} total={1} side="out" />
      ) : null}
    </div>
  );
}

/**
 * The node type map the shell registers with the board. The executable
 * families come FROM THE CATALOGUE, so a family that declares a contract is
 * renderable without anyone remembering to add a line here.
 */
const FAMILY_NODE_TYPES: Record<string, typeof FamilyNode> = Object.fromEntries(
  BLOCK_REGISTRY
    .filter((b) => b.implemented && b.contract !== undefined)
    .map((b) => [b.boardKind as string, FamilyNode]),
);

export const AUTHORING_NODE_TYPES = {
  filter: FilterNode,
  derived: DerivedNode,
  select: SelectNode,
  ...FAMILY_NODE_TYPES,
};