// ============================================================
// TASK 6 — Validate drag, resize, and reflow   [FIXED BUILD]
// FILE: Frontend/PlantProcess.Web/src/components/dashboard/DashboardGridLayout.tsx
//
// FIX: Reverted to the project's existing react-grid-layout pattern:
//   • Uses `Responsive as any` — NO WidthProvider (not exported as
//     a named member in this version of react-grid-layout).
//   • Manages width manually via ResizeObserver + useState.
//   • Removed `import type { Layout, Layouts }` — not needed here.
//
// ADDITIONS vs the original:
//   • beginDrag / endDrag wired to onDragStart/Stop and
//     onResizeStart/Stop so localStorage is NOT written mid-drag.
//   • draggableHandle matches original ".dashboard-widget__drag-handle".
// ============================================================

import { useCallback, useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { Responsive } from "react-grid-layout";
import {
  useDashboardGridLayout,
} from "../../state/DashboardGridLayoutContext";
import type { DashboardGridLayouts } from "../../state/DashboardGridLayoutContext";

// eslint-disable-next-line @typescript-eslint/no-explicit-any
const ResponsiveGridLayout = Responsive as any;

export function DashboardGridLayout({ children }: { children: ReactNode }) {
  const { layouts, setLayouts, beginDrag, endDrag } = useDashboardGridLayout();

  const containerRef = useRef<HTMLDivElement | null>(null);
  const [width, setWidth] = useState(1200);

  useEffect(() => {
    const element = containerRef.current;
    if (!element) return;

    const updateWidth = () => {
      const nextWidth = element.getBoundingClientRect().width;
      if (nextWidth > 0) setWidth(nextWidth);
    };

    updateWidth();

    const observer = new ResizeObserver(() => updateWidth());
    observer.observe(element);

    return () => {
      observer.disconnect();
    };
  }, []);

  const handleLayoutChange = useCallback(
    (_currentLayout: unknown, allLayouts: unknown) => {
      setLayouts(allLayouts as DashboardGridLayouts);
    },
    [setLayouts]
  );

  return (
    <div ref={containerRef} className="dashboard-grid-layout-shell">
      <ResponsiveGridLayout
        className="dashboard-grid-layout"
        layouts={layouts}
        width={width}
        breakpoints={{ lg: 1400, md: 1100, sm: 800, xs: 560, xxs: 0 }}
        cols={{ lg: 12, md: 10, sm: 6, xs: 4, xxs: 2 }}
        rowHeight={42}
        margin={[18, 18]}
        containerPadding={[0, 0]}
        compactType="vertical"
        preventCollision={false}
        isDraggable
        isResizable
        draggableHandle=".dashboard-widget__drag-handle"
        onDragStart={beginDrag}
        onDragStop={endDrag}
        onResizeStart={beginDrag}
        onResizeStop={endDrag}
        onLayoutChange={handleLayoutChange}
      >
        {children}
      </ResponsiveGridLayout>
    </div>
  );
}
