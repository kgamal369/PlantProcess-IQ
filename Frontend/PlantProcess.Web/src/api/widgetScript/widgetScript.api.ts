import { apiClient } from "../http";
import type {
  DashboardWidgetFilters,
  DashboardWidgetQueryOptions,
  DashboardWidgetQueryResult,
} from "../plantProcessApi";

export type WidgetScriptValidationRequest = {
  expression: string;
  filters?: DashboardWidgetFilters | null;
  options?: DashboardWidgetQueryOptions | null;
};

export type WidgetScriptTemplate = {
  title: string;
  description: string;
  expression: string;
};

export const widgetScriptTemplates: WidgetScriptTemplate[] = [
  {
    title: "Defects by defect type",
    description: "Bar chart grouped by defect family.",
    expression:
      "widget=chart;\nchart=bar;\ndimension=defectType;\nmeasure=defectCount;\nmaxRows=20;\nsort=desc;",
  },
  {
    title: "Defect rate by day",
    description: "Line chart showing defect rate by production day.",
    expression:
      "widget=chart;\nchart=line;\ndimension=day;\nmeasure=defectRate;\nmaxRows=30;\nsort=asc;",
  },
  {
    title: "Average parameter by equipment",
    description: "Parameter-based chart. Replace parameter value with real metadata code.",
    expression:
      "widget=chart;\nchart=bar;\ndimension=equipment;\nmeasure=avgParameterValue;\nparameter=ACTUAL_FDT_C;\nmaxRows=20;\nsort=desc;",
  },
  {
    title: "Risk by risk class",
    description: "Risk score grouped by risk class.",
    expression:
      "widget=chart;\nchart=donut;\ndimension=riskClass;\nmeasure=riskScore;\nmaxRows=10;\nsort=desc;",
  },
  {
    title: "Data quality issues by source",
    description: "Data quality issue count grouped by source system.",
    expression:
      "widget=chart;\nchart=bar;\ndimension=sourceSystem;\nmeasure=dataQualityIssueCount;\nmaxRows=20;\nsort=desc;",
  },
];

export const widgetScriptApi = {
  executeExpression: (request: WidgetScriptValidationRequest) =>
    apiClient.post<DashboardWidgetQueryResult>(
      "/analytics/dashboard/widgets/execute",
      request
    ),
};