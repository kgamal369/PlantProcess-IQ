import { Handle, Position, type Node, type NodeProps } from "@xyflow/react";
import { StandardP2Input, StandardP2Select } from "@/components/standard/StandardP2Controls";

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

/** Generic toolbox block (spec S7): typed flow ports + inline config.
 * Uses Standard* primitives and CSS port classes (design-system conformant). */
export function BlockNode({ id, data }: NodeProps<BlockNodeType>) {
  return (
    <div className="blk-node">
      {data.hasIn !== false && (
        <Handle type="target" position={Position.Left} className="ppiq-port ppiq-port--flow" />
      )}
      <div className="blk-node__kind">{data.kind}</div>
      <div className="blk-node__title">{data.title}</div>
      {(data.fields ?? []).map((f) =>
        f.type === "number" ? (
          <StandardP2Input key={f.key} className="blk-node__field" type="number" value={f.value}
            aria-label={f.label}
            onChange={(e) => data.onField?.(id, f.key, e.target.value)} />
        ) : (
          <StandardP2Select key={f.key} className="blk-node__field" value={f.value}
            aria-label={f.label}
            onChange={(e) => data.onField?.(id, f.key, e.target.value)}>
            {(f.options ?? []).map((o) => <option key={o} value={o}>{o}</option>)}
          </StandardP2Select>
        )
      )}
      {data.hasOut !== false && (
        <Handle type="source" position={Position.Right} className="ppiq-port ppiq-port--flow" />
      )}
    </div>
  );
}