import { Phase56MaterialInvestigationPage } from "./Phase56/Phase56Pages";
import { GenealogyThreadPanel } from "@/components/materials/GenealogyThreadPanel";
import { WidgetScriptBuilderPanel } from "@/components/dashboard/widget-builder/WidgetScriptBuilderPanel";

export function MaterialInvestigationPage() {
  return (
    <div style={{ display: "grid", gap: 24 }}>
      <GenealogyThreadPanel />
      <WidgetScriptBuilderPanel />
      <Phase56MaterialInvestigationPage />
    </div>
  );
}