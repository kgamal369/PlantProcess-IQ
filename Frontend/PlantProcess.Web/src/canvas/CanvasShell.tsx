import { ReactFlow, Background, BackgroundVariant, Controls, MiniMap, type ReactFlowProps } from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import "./canvas.css";

/** Shared dark-industrial canvas: grid, minimap, controls, snap. */
export function CanvasShell(props: ReactFlowProps) {
  return (
    <div className="ppiq-canvas">
      <ReactFlow
        fitView
        snapToGrid
        snapGrid={[14, 14]}
        deleteKeyCode={["Backspace", "Delete"]}
        proOptions={{ hideAttribution: false }}
        {...props}
      >
        <Background variant={BackgroundVariant.Dots} gap={22} size={1.4} color="#16294a" />
        <MiniMap pannable zoomable className="ppiq-minimap" />
        <Controls className="ppiq-controls" />
      </ReactFlow>
    </div>
  );
}