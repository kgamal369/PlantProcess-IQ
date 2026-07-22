import { Handle, Position, type Node, type NodeProps } from "@xyflow/react";

export type BlockField = {
  key: string; label: string; options?: string[]; value: string; type?: "select" | "number";
};
export type BlockNodeData = {
  kind: string; title: string;
  fields?: BlockField[];
  onField?: (nodeId: string, key: string, value: string) => void;
  hasIn?: boolean; hasOut?: boolean;
  [key: string]: unknown;
};
type BlockNodeType = Node<BlockNodeData, "block">;

/** Generic toolbox block (spec S7): typed flow ports + inline config. */
export function BlockNode({ id, data }: NodeProps<BlockNodeType>) {
  return (
    <div className="blk-node">
      {data.hasIn !== false && <Handle type="target" position={Position.Left} style={{ background: "#2ce6a2" }} />}
      <div className="blk-node__kind">{data.kind}</div>
      <div className="blk-node__title">{data.title}</div>
      {(data.fields ?? []).map((f) =>
        f.type === "number" ? (
          <input key={f.key} type="number" value={f.value} aria-label={f.label}
            onChange={(e) => data.onField?.(id, f.key, e.target.value)} />
        ) : (
          <select key={f.key} value={f.value} aria-label={f.label}
            onChange={(e) => data.onField?.(id, f.key, e.target.value)}>
            {(f.options ?? []).map((o) => <option key={o} value={o}>{o}</option>)}
          </select>
        )
      )}
      {data.hasOut !== false && <Handle type="source" position={Position.Right} style={{ background: "#2ce6a2" }} />}
    </div>
  );
}