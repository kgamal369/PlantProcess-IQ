// @ts-nocheck
import {
  ArrowLeft,
    ArrowRight,
    BarChart3,
    CheckCircle2,
    Eye,
    Filter,
    Save,
    Sparkles,
    X,
} from "lucide-react";
import {
  useEffect, useMemo, useState
} from "react";
import {
  productApi
} from "../../../../api/productApiClient";
import {
  widgetScriptApi
} from "@/api/widgetScript";
import {
  useOptimisticSave
} from "@/hooks/useOptimisticSave";
import type {
  DashboardChartTypeMetadata,
    DashboardDefinitionRecord,
    DashboardDimensionMetadata,
    DashboardMeasureMetadata,
    DashboardMetadata,
    DashboardReferenceData,
    DashboardWidgetDefinitionRecord,
    DashboardWidgetFilters,
    DashboardWidgetQuery,
    DashboardWidgetQueryOptions,
    DashboardWidgetQueryResult,
} from "../../../../api/productApiClient";

import {
  InteractiveBarChart,
    InteractiveLineChart,
    InteractivePieChart,
} from "@/components/charts/InteractiveCharts";
import type {
  ChartRow
} from "@/components/charts/InteractiveCharts";
import {
  EmptyInsightState
} from "@/components/dashboard/EmptyInsightState";
import {
  WidgetScriptStep
} from "../WidgetScriptStep";
import {
  StandardButton
} from "@/components/standard";

import type {
  ValidationIssue, WidgetBuilderState, WizardStep,
    RelativeDateUnit,
} from "./WidgetBuilderWizardContent.types";

export const stepOrder: WizardStep[] = [
  "purpose",
  "chartType",
  "data",
  "filters",
  "script",
  "preview",
];

export const stepLabels: Record<WizardStep, string> = {
  purpose: "Purpose",
  chartType: "Chart",
  data: "Data",
  filters: "Filters",
  script: "Transform",
  preview: "Preview",
};

export const defaultState: WidgetBuilderState = {
  widgetTitle: "",
  widgetType: "chart",
  filters: {},
  maxRows: 100,
  rawRowLimit: 500,
  dateMode: "relative",
  relativeDateValue: 30,
  relativeDateUnit: "days",
  queryExpression: "",
  expressionEnabled: false,
};

export function generateWidgetCode(title: string) {
  const slug = title
    .trim()
    .replace(/[^a-zA-Z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "")
    .toUpperCase();

  return `${slug || "WIDGET"}_${Date.now()}`;
}

function parseJson<T>(value: string | null | undefined, fallback: T): T {
  if (!value) return fallback;

  try {
    return JSON.parse(value) as T;
  } catch {
    return fallback;
  }
}

export function toInputDateTime(value?: string | null) {
  if (!value) return "";

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "";

  return parsed.toISOString().slice(0, 16);
}

export function fromInputDateTime(value: string) {
  if (!value) return null;

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return null;

  return parsed.toISOString();
}

export function relativeFromUtc(value: number, unit: RelativeDateUnit) {
  const date = new Date();

  if (unit === "days") {
    date.setUTCDate(date.getUTCDate() - value);
  }

  if (unit === "weeks") {
    date.setUTCDate(date.getUTCDate() - value * 7);
  }

  if (unit === "months") {
    date.setUTCMonth(date.getUTCMonth() - value);
  }

  return date.toISOString();
}

export function formatError(error: unknown) {
  if (error instanceof Error) return error.message;
  return String(error);
}

export function mapValidationIssues(error: unknown): ValidationIssue[] {
  const raw = formatError(error);

  try {
    const parsed = JSON.parse(raw) as {
      errors?: Record<string, string[]>;
      title?: string;
      detail?: string;
    };

    if (parsed.errors) {
      return Object.entries(parsed.errors).flatMap(([field, messages]) =>
        messages.map((message) => ({
          field,
          message,
        }))
      );
    }

    if (parsed.detail || parsed.title) {
      return [
        {
          field: "Backend validation",
          message: parsed.detail ?? parsed.title ?? raw,
        },
      ];
    }
  } catch {
    // Existing API client may already return a flattened string.
  }

  return [
    {
      field: "Request",
      message: raw,
    },
  ];
}

export function isCompatible(
  chartTypeCode: string | undefined,
  dimension: DashboardDimensionMetadata | undefined,
  measure: DashboardMeasureMetadata | undefined,
  metadata: DashboardMetadata | null
) {
  if (!chartTypeCode || !dimension || !measure || !metadata) return true;

  if (!dimension.compatibleChartTypes.includes(chartTypeCode)) return false;
  if (!measure.compatibleChartTypes.includes(chartTypeCode)) return false;

  const exactRule = metadata.compatibilityRules.find(
    (rule) =>
      rule.dimensionCode === dimension.code &&
      rule.measureCode === measure.code &&
      rule.allowedChartTypes.includes(chartTypeCode)
  );

  return Boolean(exactRule);
}

export function inferCategoryKey(result: DashboardWidgetQueryResult | null) {
  if (!result) return "dimensionLabel";

  const dimensionCode = result.widget.dimensionCode ?? "dimensionLabel";

  if (result.columns.some((column) => column.code === dimensionCode)) {
    return dimensionCode;
  }

  return (
    result.columns.find((column) => column.code !== "value")?.code ??
    "dimensionLabel"
  );
}

export function inferValueKey(result: DashboardWidgetQueryResult | null) {
  if (!result) return "value";

  if (result.columns.some((column) => column.code === "value")) {
    return "value";
  }

  return (
    result.columns.find((column) => column.dataType === "number")?.code ??
    "value"
  );
}

export function selectFieldForDimension(
  dimensionCode?: string | null
):
  | "siteId"
  | "areaId"
  | "equipmentId"
  | "materialCode"
  | "materialUnitType"
  | "sourceSystem"
  | "defectType"
  | "riskClass"
  | "shiftCode"
  | "parameterCode" {
  switch (dimensionCode) {
    case "site":
      return "siteId";
    case "area":
      return "areaId";
    case "equipment":
      return "equipmentId";
    case "sourceSystem":
      return "sourceSystem";
    case "defectType":
      return "defectType";
    case "riskClass":
      return "riskClass";
    case "shiftCode":
      return "shiftCode";
    case "parameterCode":
      return "parameterCode";
    case "materialUnitType":
      return "materialUnitType";
    default:
      return "materialCode";
  }
}
