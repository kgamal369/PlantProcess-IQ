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
import { FLOW_IN, FLOW_OUT, type BoardField } from "./graphSemantics";

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

/** The node type map the shell registers with the board. */
export const AUTHORING_NODE_TYPES = {
  filter: FilterNode,
  derived: DerivedNode,
  select: SelectNode,
};