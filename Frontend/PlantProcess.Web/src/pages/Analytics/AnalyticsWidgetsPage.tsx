import {
  DowntimeByAreaWidget, EquipmentStoppageWidget, DailyProductionWidget,
  DefectByProductFamilyWidget, CastingSpeedByGradeWidget, ParameterTrendWidget,
  KpiTargetVsActualWidget,
} from "@/components/analytics/ReadModelWidgets";
import { AdvancedResultsList } from "@/components/analytics/AdvancedResultsList";

const grid: React.CSSProperties = {
  display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(360px, 1fr))", gap: 16,
};

export function AnalyticsWidgetsPage() {
  return (
    <div style={{ padding: "8px 4px 32px" }}>
      <h1 style={{ fontSize: 22, fontWeight: 800, margin: "0 0 4px" }}>Analytics widgets</h1>
      <p style={{ fontSize: 13, color: "#6b7280", margin: "0 0 20px" }}>
        Simple read-model widgets (T-027) and the advanced-analysis result view (T-034).
        Set real KPI / parameter codes below for your data.
      </p>

      <div style={grid}>
        <DowntimeByAreaWidget />
        <EquipmentStoppageWidget />
        <DailyProductionWidget />
        <DefectByProductFamilyWidget />
        <CastingSpeedByGradeWidget parameterCode="CASTING_SPEED" />
        <ParameterTrendWidget parameterCode="LAB_C" title="Lab carbon trend" />
        <KpiTargetVsActualWidget kpiCode="FPSY" />
      </div>

      <h2 style={{ fontSize: 18, fontWeight: 700, margin: "28px 0 12px" }}>Advanced analysis (managed engine)</h2>
      <AdvancedResultsList />
    </div>
  );
}