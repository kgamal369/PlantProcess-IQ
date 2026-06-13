/* PPIQ-PHASE5 BidirectionalThread - generic genealogy viz. UP = source,
 * DOWN = affected. Nodes are StandardButtons; clicking highlights the path. */
import { useMemo, useState } from "react";
import { StandardButton } from "../standard";
import type { ThreadNode, ThreadEdge } from "../../types/analyticsContracts";

const T = { navy: "#050B18", panel: "#0B1730", line: "#16243D", cyan: "#00D4FF", ok: "#2CE6A2", warn: "#FFB020", white: "#EAF6FF", steel: "#8EA7C1" };

function pathFrom(start: string, edges: ThreadEdge[]): Set<string> {
  const out = new Set<string>([start]);
  let frontier = [start];
  while (frontier.length) {
    const next: string[] = [];
    for (const id of frontier) {
      for (const e of edges) {
        if (e.from === id && !out.has(e.to)) { out.add(e.to); next.push(e.to); }
        if (e.to === id && !out.has(e.from)) { out.add(e.from); next.push(e.from); }
      }
    }
    frontier = next;
  }
  return out;
}

export function BidirectionalThread({
  nodes,
  edges,
  onNodeClick,
}: {
  nodes: ThreadNode[];
  edges: ThreadEdge[];
  onNodeClick?: (node: ThreadNode) => void;
}) {
  const [active, setActive] = useState<string | null>(null);
  const highlighted = useMemo(() => (active ? pathFrom(active, edges) : null), [active, edges]);

  const upIds = new Set(edges.filter((e) => e.direction === "up").map((e) => e.to));
  const downIds = new Set(edges.filter((e) => e.direction === "down").map((e) => e.to));
  const cols: Record<string, ThreadNode[]> = { up: [], focus: [], down: [] };
  for (const n of nodes) {
    if (upIds.has(n.id) && !downIds.has(n.id)) cols.up.push(n);
    else if (downIds.has(n.id) && !upIds.has(n.id)) cols.down.push(n);
    else cols.focus.push(n);
  }

  const Column = ({ title, items, color }: { title: string; items: ThreadNode[]; color: string }) => (
    <div style={{ flex: 1, minWidth: 180 }}>
      <div style={{ color, fontSize: 11, fontFamily: "'JetBrains Mono', monospace", letterSpacing: "0.12em", marginBottom: 8 }}>{title}</div>
      <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
        {items.map((n) => {
          const on = !highlighted || highlighted.has(n.id);
          return (
            <StandardButton
              key={n.id}
              variant="ghost"
              data-testid="thread-node"
              data-node-id={n.id}
              data-kind={n.kind}
              data-active={active === n.id ? "true" : "false"}
              onMouseEnter={() => setActive(n.id)}
              onMouseLeave={() => setActive(null)}
              onClick={() => { setActive(n.id); onNodeClick?.(n); }}
              style={{
                textAlign: "left", justifyContent: "flex-start", opacity: on ? 1 : 0.35,
                background: active === n.id ? T.navy : T.panel,
                border: `1px solid ${active === n.id ? color : T.line}`,
                borderRadius: 8, padding: "8px 10px", color: T.white, width: "100%",
              }}
            >
              <span style={{ display: "block", textAlign: "left" }}>
                <span style={{ display: "block", fontSize: 11, color }}>{n.kind}</span>
                <span style={{ display: "block", fontSize: 13, fontWeight: 600 }}>{n.label}</span>
              </span>
            </StandardButton>
          );
        })}
        {items.length === 0 ? <div style={{ color: T.steel, fontSize: 12 }}>none</div> : null}
      </div>
    </div>
  );

  return (
    <div data-testid="bidirectional-thread" style={{ display: "flex", gap: 16, alignItems: "flex-start", background: T.navy, padding: 12, borderRadius: 12, border: `1px solid ${T.line}` }}>
      <Column title="UP - SOURCE" items={cols.up} color={T.cyan} />
      <div style={{ alignSelf: "center", color: T.steel }}>&harr;</div>
      <Column title="FOCUS" items={cols.focus} color={T.warn} />
      <div style={{ alignSelf: "center", color: T.steel }}>&harr;</div>
      <Column title="DOWN - AFFECTED" items={cols.down} color={T.ok} />
    </div>
  );
}
export default BidirectionalThread;