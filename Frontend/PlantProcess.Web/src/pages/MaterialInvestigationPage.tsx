import { MaterialAnalyticsMaterialInvestigationPage } from "./MaterialAnalytics/MaterialAnalyticsPages";
import { GenealogyThreadPanel } from "@/components/materials/GenealogyThreadPanel";
import { WidgetScriptBuilderPanel } from "@/components/dashboard/widget-builder/WidgetScriptBuilderPanel";

import { P2T08_STANDARD_ROLLOUT_MARKER } from "@/components/standard/StandardP2Controls";
export function MaterialInvestigationPage() {
  return (
    <div>
      <GenealogyThreadPanel />
      <WidgetScriptBuilderPanel />
      <MaterialAnalyticsMaterialInvestigationPage />
    </div>
  );
}