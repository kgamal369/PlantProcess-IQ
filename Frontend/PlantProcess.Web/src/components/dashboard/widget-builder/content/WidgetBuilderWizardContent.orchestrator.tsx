import type { WidgetBuilderWizardProps } from "./WidgetBuilderWizardContent.types";
import { useWidgetBuilderWizardContentModel } from "./WidgetBuilderWizardContent.model";
import { WidgetBuilderWizardContentView } from "./WidgetBuilderWizardContent.view";

export function WidgetBuilderWizardContent(props: WidgetBuilderWizardProps) {
  const vm = useWidgetBuilderWizardContentModel(props);
  return <WidgetBuilderWizardContentView vm={vm} />;
}

export { WidgetBuilderWizardContent as WidgetBuilderWizard };
export default WidgetBuilderWizardContent;