import { DemoReadinessPanel } from "@/components/demo/DemoReadinessPanel";
import { DemoAnalyticsDemoLifecyclePage } from "./DemoAnalyticsPages";

export function DemoLifecyclePage() {
  return (
    <>
      <DemoAnalyticsDemoLifecyclePage />
      <DemoReadinessPanel />
    </>
  );
}