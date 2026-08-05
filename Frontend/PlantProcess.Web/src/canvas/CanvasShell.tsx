import type { DragEventHandler, ReactNode } from "react";
import { ReactFlow, Background, BackgroundVariant, Controls, MiniMap, type ReactFlowProps } from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import "./canvas.css";

export interface CanvasShellProps extends ReactFlowProps {
  /**
   * T-033. Board-editing controls rendered INSIDE the canvas toolbar, which is
   * where Chapter 4 section 5.2.6 places Arrange - beside zoom, zoom fit and
   * the minimap, not in the lifecycle action bar.
   *
   * OPTIONAL ON PURPOSE. Every other surface that uses this canvas gets exactly
   * the toolbar it had before; only a surface that owns board editing passes
   * anything here.
   */
  boardActions?: ReactNode;
  /**
   * T-034. A schema drag has to land somewhere, and the canvas wrapper is
   * already the right shape and the right size. These go on the wrapper rather
   * than on a new element around it, so the board's layout does not change to
   * accept a drop. Optional: no other surface takes drops.
   */
  onBoardDragOver?: DragEventHandler<HTMLDivElement>;
  onBoardDrop?: DragEventHandler<HTMLDivElement>;
}

/** Shared dark-industrial canvas: grid, minimap, controls, snap. */
export function CanvasShell({ boardActions, onBoardDragOver, onBoardDrop, ...props }: CanvasShellProps) {
  return (
    <div className="ppiq-canvas" onDragOver={onBoardDragOver} onDrop={onBoardDrop}>
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
        <Controls className="ppiq-controls">{boardActions}</Controls>
      </ReactFlow>
    </div>
  );
}