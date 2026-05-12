import { useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { Responsive } from "react-grid-layout";
import { useDashboardGridLayout } from "../../state/DashboardGridLayoutContext";
import type { DashboardGridLayouts } from "../../state/DashboardGridLayoutContext";

const ResponsiveGridLayout = Responsive as any;

export function DashboardGridLayout({ children }: { children: ReactNode }) {
  const { layouts, setLayouts } = useDashboardGridLayout();
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [width, setWidth] = useState(1200);

  useEffect(() => {
    const element = containerRef.current;
    if (!element) return;

    const updateWidth = () => {
      const nextWidth = element.getBoundingClientRect().width;
      if (nextWidth > 0) {
        setWidth(nextWidth);
      }
    };

    updateWidth();

    const observer = new ResizeObserver(() => {
      updateWidth();
    });

    observer.observe(element);

    return () => {
      observer.disconnect();
    };
  }, []);

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
        onLayoutChange={(
          _currentLayout: unknown,
          allLayouts: unknown
        ) => {
          setLayouts(allLayouts as DashboardGridLayouts);
        }}
      >
        {children}
      </ResponsiveGridLayout>
    </div>
  );
}