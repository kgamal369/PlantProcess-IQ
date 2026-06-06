import type {
  DashboardWidgetDefinitionRecord,
  DashboardWidgetFilters,
} from "../../../../api/productApiClient";

export interface WidgetBuilderWizardProps {
  isOpen: boolean;
  dashboardDefinitionId?: string | null;
  existingWidget?: DashboardWidgetDefinitionRecord | null;
  onClose: () => void;
  onWidgetSaved?: (widgetId: string) => void | Promise<void>;
}

export type WizardStep =
  | "purpose"
  | "chartType"
  | "data"
  | "filters"
  | "script"
  | "preview";

export type RelativeDateUnit = "days" | "weeks" | "months";

export interface WidgetBuilderState {
  purposeCode?: string;
  widgetTitle: string;
  widgetType: string;
  chartTypeCode?: string;
  dimensionCode?: string;
  measureCode?: string;
  parameterCode?: string;
  filters: DashboardWidgetFilters;
  maxRows: number;
  rawRowLimit: number;
  dateMode: "none" | "absolute" | "relative";
  relativeDateValue: number;
  relativeDateUnit: RelativeDateUnit;
  queryExpression: string;
  expressionEnabled: boolean;
}

export interface ValidationIssue {
  field: string;
  message: string;
}
