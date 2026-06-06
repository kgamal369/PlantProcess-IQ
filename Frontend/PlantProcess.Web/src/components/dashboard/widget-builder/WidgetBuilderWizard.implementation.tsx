/*
  PlantProcess IQ Pack B-3 compatibility wrapper.
  The old WidgetBuilderWizard shell duplicated WidgetBuilderWizardContent.
  WidgetBuilderWizardContent is now the canonical split implementation.
*/

export {
  WidgetBuilderWizard,
  WidgetBuilderWizardContent,
} from "./WidgetBuilderWizardContent";

export { default } from "./WidgetBuilderWizardContent";
