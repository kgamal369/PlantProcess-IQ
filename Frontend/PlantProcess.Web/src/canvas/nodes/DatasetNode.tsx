import { Handle, Position, type Node, type NodeProps } from "@xyflow/react";
import { inferPortType } from "../ports";
import { FLOW_OUT } from "@/authoring/graphSemantics";

export type DatasetColumn = { name: string; sqlType: string; isKeyCandidate?: boolean };
export type DatasetNodeData = {
  table: string; source: string; columns: DatasetColumn[];
  /**
   * T-034. The columns the author picked in the schema tree before dragging.
   *
   * AUTHORING SELECTION METADATA, NOT A PROJECTION. His ruling is explicit: a
   * source node carrying a selection does not behave like a Select block, and
   * serialisation does not read this. The projection stays explicit, Source to
   * Select block, which is T-033's grammar and not the tree's business.
   *
   * Absent means the whole table was dragged with nothing picked out.
   */
  selectedColumns?: string[];
  [key: string]: unknown;
};
type DatasetNodeType = Node<DatasetNodeData, "dataset">;

/** A staged table: every column is a typed source+target port (spec S3/S4).
 * Port colours come from CSS classes (no inline styles - UI ratchet D2). */
export function DatasetNode({ data, selected }: NodeProps<DatasetNodeType>) {
  const chosen = Array.isArray(data.selectedColumns) ? data.selectedColumns : [];
  return (
    <div className={"ds-node" + (selected ? " selected" : "")}>
      {/* T-033. THE DATASET PORT, and it is a different wire from the column
          ports below. A column port carries a JOIN - key to key between two
          tables. This one carries the DATASET into a relational block, which
          is what "dataset -> Filter -> dataset" means in ruling 2. The handle
          id is imported rather than spelled, so the board and the semantics
          cannot drift apart. */}
      <Handle id={FLOW_OUT} type="source" position={Position.Right}
              className="ppiq-port ppiq-port--flow ds-node__flow" />
      <div className="ds-node__head">
        <span className="ds-node__name">{data.table}</span>
        <span className="ds-node__src">{data.source}</span>
        {chosen.length > 0 && (
          <span className="ds-node__chosen" title="Chosen in the schema tree. Not a projection - add a Select block for that.">
            {chosen.length} picked
          </span>
        )}
      </div>
      {data.columns.map((c) => {
        const pt = c.isKeyCandidate ? "key" : inferPortType(c.sqlType);
        // EVERY column keeps its ports whether or not it was picked. The mark
        // records what the author chose in the tree; it does not remove a
        // column from the node, because that would be a projection.
        const picked = chosen.indexOf(c.name) >= 0;
        return (
          <div className={"ds-node__col" + (picked ? " ds-node__col--chosen" : "")} key={c.name}>
            <Handle id={"in:" + c.name} type="target" position={Position.Left}
                    className={"ppiq-port ppiq-port--" + pt} />
            <span>{c.name}</span>
            <span className="t">{c.sqlType}</span>
            <Handle id={"out:" + c.name} type="source" position={Position.Right}
                    className={"ppiq-port ppiq-port--" + pt} />
          </div>
        );
      })}
    </div>
  );
}