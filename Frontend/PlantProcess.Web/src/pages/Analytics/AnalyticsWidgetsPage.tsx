import {
  DowntimeByAreaWidget, EquipmentStoppageWidget, DailyProductionWidget,
  DefectByProductFamilyWidget, CastingSpeedByGradeWidget, ParameterTrendWidget,
  KpiTargetVsActualWidget,
} from "@/components/analytics/ReadModelWidgets";
import { AdvancedResultsList } from "@/components/analytics/AdvancedResultsList";

import { P2T08_STANDARD_ROLLOUT_MARKER } from "@/components/standard/StandardP2Controls";
const grid: React.CSSProperties = {
  display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(360px, 1fr))", gap: 16,
};

export function AnalyticsWidgetsPage() {
  return (
    <div>
      <h1>Analytics widgets</h1>
      <p>
        Simple read-model widgets (T-027) and the advanced-analysis result view (T-034).
        Set real KPI / parameter codes below for your data.
      </p>

      <div>
        <DowntimeByAreaWidget />
        <EquipmentStoppageWidget />
        <DailyProductionWidget />
        <DefectByProductFamilyWidget />
        <CastingSpeedByGradeWidget parameterCode="CASTING_SPEED" />
        <ParameterTrendWidget parameterCode="LAB_C" title="Lab carbon trend" />
        <KpiTargetVsActualWidget kpiCode="FPSY" />
      </div>

      <h2>Advanced analysis (managed engine)</h2>
      <AdvancedResultsList />
    </div>
  );
}