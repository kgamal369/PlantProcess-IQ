import { Handle, Position, type Node, type NodeProps } from "@xyflow/react";
import { PORT_COLORS, inferPortType } from "../ports";

export type DatasetColumn = { name: string; sqlType: string; isKeyCandidate?: boolean };
export type DatasetNodeData = {
  table: string; source: string; columns: DatasetColumn[];
  [key: string]: unknown;
};
type DatasetNodeType = Node<DatasetNodeData, "dataset">;

/** A staged table: every column is a typed source+target port (spec S3/S4). */
export function DatasetNode({ data, selected }: NodeProps<DatasetNodeType>) {
  return (
    <div className={"ds-node" + (selected ? " selected" : "")}>
      <div className="ds-node__head">
        <span className="ds-node__name">{data.table}</span>
        <span className="ds-node__src">{data.source}</span>
      </div>
      {data.columns.map((c) => {
        const pt = c.isKeyCandidate ? "key" : inferPortType(c.sqlType);
        const color = PORT_COLORS[pt];
        return (
          <div className="ds-node__col" key={c.name}>
            <Handle id={"in:" + c.name} type="target" position={Position.Left} style={{ background: color }} />
            <span>{c.name}</span>
            <span className="t">{c.sqlType}</span>
            <Handle id={"out:" + c.name} type="source" position={Position.Right} style={{ background: color }} />
          </div>
        );
      })}
    </div>
  );
}