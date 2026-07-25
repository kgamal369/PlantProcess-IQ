import { MaterialAnalyticsMaterialInvestigationPage } from "./MaterialAnalytics/MaterialAnalyticsPages";
import { GenealogyThreadPanel } from "@/components/materials/GenealogyThreadPanel";

import { P2T08_STANDARD_ROLLOUT_MARKER } from "@/components/standard/StandardP2Controls";
export function MaterialInvestigationPage() {
  return (
    <div>
      <GenealogyThreadPanel />
      <MaterialAnalyticsMaterialInvestigationPage />
    </div>
  );
}