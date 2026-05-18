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
import { useEffect, useMemo, useState } from "react";

import { plantProcessApi } from "@/api/plantProcessApi";
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
  DashboardWidgetQueryResult,
} from "@/api/plantProcessApi";

import {
  InteractiveBarChart,
  InteractiveLineChart,
  InteractivePieChart,
} from "@/components/charts/InteractiveCharts";
import type { ChartRow } from "@/components/charts/InteractiveCharts";
import { EmptyInsightState } from "@/components/dashboard/EmptyInsightState";

interface WidgetBuilderWizardProps {
  isOpen: boolean;
  dashboardDefinitionId?: string | null;
  existingWidget?: DashboardWidgetDefinitionRecord | null;
  onClose: () => void;
  onWidgetSaved?: (widgetId: string) => void | Promise<void>;
}

type WizardStep = "purpose" | "chartType" | "data" | "filters" | "preview";
type RelativeDateUnit = "days" | "weeks" | "months";

interface WidgetBuilderState {
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
}

interface ValidationIssue {
  field: string;
  message: string;
}

const stepOrder: WizardStep[] = [
  "purpose",
  "chartType",
  "data",
  "filters",
  "preview",
];

const defaultState: WidgetBuilderState = {
  widgetTitle: "",
  widgetType: "chart",
  filters: {},
  maxRows: 100,
  rawRowLimit: 500,
  dateMode: "relative",
  relativeDateValue: 30,
  relativeDateUnit: "days",
};

function generateWidgetCode(title: string) {
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

function toInputDateTime(value?: string | null) {
  if (!value) return "";

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "";

  return parsed.toISOString().slice(0, 16);
}

function fromInputDateTime(value: string) {
  if (!value) return null;

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return null;

  return parsed.toISOString();
}

function relativeFromUtc(value: number, unit: RelativeDateUnit) {
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

function formatError(error: unknown) {
  if (error instanceof Error) return error.message;
  return String(error);
}

function mapValidationIssues(error: unknown): ValidationIssue[] {
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

function isCompatible(
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

function inferCategoryKey(result: DashboardWidgetQueryResult | null) {
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

function inferValueKey(result: DashboardWidgetQueryResult | null) {
  if (!result) return "value";

  if (result.columns.some((column) => column.code === "value")) {
    return "value";
  }

  return result.columns.find((column) => column.dataType === "number")?.code ?? "value";
}

function selectFieldForDimension(
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

export function WidgetBuilderWizardContent({
  isOpen,
  dashboardDefinitionId,
  existingWidget,
  onClose,
  onWidgetSaved,
}: WidgetBuilderWizardProps) {
  const [metadata, setMetadata] = useState<DashboardMetadata | null>(null);
  const [referenceData, setReferenceData] = useState<DashboardReferenceData | null>(null);
  const [dashboards, setDashboards] = useState<DashboardDefinitionRecord[]>([]);

  const [isLoading, setIsLoading] = useState(false);
  const [isPreviewing, setIsPreviewing] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  const [loadError, setLoadError] = useState<unknown>(null);
  const [previewError, setPreviewError] = useState<unknown>(null);
  const [preview, setPreview] = useState<DashboardWidgetQueryResult | null>(null);

  const [step, setStep] = useState<WizardStep>("purpose");
  const [builderState, setBuilderState] =
    useState<WidgetBuilderState>(defaultState);

  const effectiveDashboardDefinitionId =
    dashboardDefinitionId ??
    dashboards.find((dashboard) => dashboard.isDefault)?.id ??
    dashboards[0]?.id ??
    null;

  useEffect(() => {
    if (!isOpen) return;

    let ignore = false;

    async function load() {
      setIsLoading(true);
      setLoadError(null);
      setPreview(null);
      setPreviewError(null);
      setStep("purpose");

      try {
        const [metadataResult, referenceResult, dashboardResult] =
          await Promise.all([
            plantProcessApi.getDashboardMetadata(),
            plantProcessApi.getDashboardReferenceData(),
            plantProcessApi.getDashboardDefinitions(false, true),
          ]);

        if (ignore) return;

        setMetadata(metadataResult);
        setReferenceData(referenceResult);
        setDashboards(dashboardResult);

        if (existingWidget) {
          const filters = parseJson<DashboardWidgetFilters>(
            existingWidget.filterJson,
            {}
          );

          const displayOptions = parseJson<{
            maxRows?: number;
            rawRowLimit?: number;
          }>(existingWidget.displayOptionsJson, {});

          setBuilderState({
            purposeCode: undefined,
            widgetTitle: existingWidget.widgetTitle,
            widgetType: existingWidget.widgetType,
            chartTypeCode: existingWidget.chartType,
            dimensionCode: existingWidget.dimensionCode,
            measureCode: existingWidget.measureCode,
            parameterCode: existingWidget.parameterCode ?? undefined,
            filters,
            maxRows:
              displayOptions.maxRows ??
              metadataResult.safetyLimits.defaultMaxRows,
            rawRowLimit:
              displayOptions.rawRowLimit ??
              metadataResult.safetyLimits.defaultRawRowLimit,
            dateMode: filters.fromUtc || filters.toUtc ? "absolute" : "relative",
            relativeDateValue: metadataResult.safetyLimits.defaultLookbackDays,
            relativeDateUnit: "days",
          });

          setStep("data");
        } else {
          setBuilderState({
            ...defaultState,
            maxRows: metadataResult.safetyLimits.defaultMaxRows,
            rawRowLimit: metadataResult.safetyLimits.defaultRawRowLimit,
          });
        }
      } catch (error) {
        if (!ignore) setLoadError(error);
      } finally {
        if (!ignore) setIsLoading(false);
      }
    }

    load();

    return () => {
      ignore = true;
    };
  }, [isOpen, existingWidget]);

  const selectedChartType = useMemo(
    () => metadata?.chartTypes.find((item) => item.code === builderState.chartTypeCode),
    [metadata, builderState.chartTypeCode]
  );

  const selectedDimension = useMemo(
    () => metadata?.dimensions.find((item) => item.code === builderState.dimensionCode),
    [metadata, builderState.dimensionCode]
  );

  const selectedMeasure = useMemo(
    () => metadata?.measures.find((item) => item.code === builderState.measureCode),
    [metadata, builderState.measureCode]
  );

  const compatibleDimensions = useMemo(() => {
    if (!metadata) return [];

    if (!builderState.chartTypeCode) return metadata.dimensions;

    return metadata.dimensions.filter((dimension) =>
      dimension.compatibleChartTypes.includes(builderState.chartTypeCode!)
    );
  }, [metadata, builderState.chartTypeCode]);

  const compatibleMeasures = useMemo(() => {
    if (!metadata) return [];

    if (!builderState.chartTypeCode) return metadata.measures;

    return metadata.measures.filter((measure) =>
      measure.compatibleChartTypes.includes(builderState.chartTypeCode!)
    );
  }, [metadata, builderState.chartTypeCode]);

  const validationIssues = useMemo<ValidationIssue[]>(() => {
    const issues: ValidationIssue[] = [];

    if (step === "purpose" && !builderState.purposeCode) {
      issues.push({
        field: "Purpose",
        message: "Select the business purpose for this widget.",
      });
    }

    if (step === "chartType" && !builderState.chartTypeCode) {
      issues.push({
        field: "Chart type",
        message: "Select a chart type.",
      });
    }

    if (step === "data") {
      if (!builderState.widgetTitle.trim()) {
        issues.push({
          field: "Widget title",
          message: "Widget title is required.",
        });
      }

      if (selectedChartType?.supportsDimension && !builderState.dimensionCode) {
        issues.push({
          field: "Dimension",
          message: "Dimension is required for this chart type.",
        });
      }

      if (selectedChartType?.supportsMeasure && !builderState.measureCode) {
        issues.push({
          field: "Measure",
          message: "Measure is required for this chart type.",
        });
      }

      if (
        selectedMeasure?.requiresParameterCode &&
        !builderState.parameterCode &&
        !builderState.filters.parameterCode
      ) {
        issues.push({
          field: "Parameter",
          message: `Measure "${selectedMeasure.label}" requires selecting a process parameter.`,
        });
      }

      if (
        !isCompatible(
          builderState.chartTypeCode,
          selectedDimension,
          selectedMeasure,
          metadata
        )
      ) {
        issues.push({
          field: "Dimension / Measure",
          message: `Dimension "${selectedDimension?.label}" is not compatible with measure "${selectedMeasure?.label}" for chart "${selectedChartType?.label}".`,
        });
      }
    }

    if (step === "filters") {
      if (
        builderState.dateMode === "absolute" &&
        builderState.filters.fromUtc &&
        builderState.filters.toUtc &&
        new Date(builderState.filters.fromUtc) > new Date(builderState.filters.toUtc)
      ) {
        issues.push({
          field: "Date range",
          message: "From date must be before To date.",
        });
      }

      if (
        builderState.dateMode === "relative" &&
        (!builderState.relativeDateValue || builderState.relativeDateValue < 1)
      ) {
        issues.push({
          field: "Relative date",
          message: "Relative date value must be at least 1.",
        });
      }
    }

    return issues;
  }, [
    step,
    builderState,
    selectedChartType,
    selectedMeasure,
    selectedDimension,
    metadata,
  ]);

  const currentStepIndex = stepOrder.indexOf(step);
  const canGoBack = currentStepIndex > 0;
  const canGoNext = validationIssues.length === 0 && currentStepIndex < stepOrder.length - 1;

  function patchState(patch: Partial<WidgetBuilderState>) {
    setBuilderState((current) => ({
      ...current,
      ...patch,
    }));

    setPreview(null);
    setPreviewError(null);
  }

  function patchFilters(patch: Partial<DashboardWidgetFilters>) {
    setBuilderState((current) => ({
      ...current,
      filters: {
        ...current.filters,
        ...patch,
      },
    }));

    setPreview(null);
    setPreviewError(null);
  }

  function cleanFilters(): DashboardWidgetFilters {
    const filters: DashboardWidgetFilters = {
      ...builderState.filters,
    };

    Object.entries(filters).forEach(([key, value]) => {
      if (value === "" || value === undefined) {
        delete (filters as Record<string, unknown>)[key];
      }
    });

    if (builderState.dateMode === "none") {
      delete filters.fromUtc;
      delete filters.toUtc;
    }

    if (builderState.dateMode === "relative") {
      filters.fromUtc = relativeFromUtc(
        builderState.relativeDateValue,
        builderState.relativeDateUnit
      );
      filters.toUtc = new Date().toISOString();
    }

    if (builderState.parameterCode) {
      filters.parameterCode = builderState.parameterCode;
    }

    return filters;
  }

  function buildQuery(): DashboardWidgetQuery {
    return {
      widgetType: builderState.widgetType,
      chartType: builderState.chartTypeCode,
      dimensionCode: builderState.dimensionCode,
      measureCode: builderState.measureCode,
      parameterCode: builderState.parameterCode || builderState.filters.parameterCode || null,
      filters: cleanFilters(),
      options: {
        maxRows: builderState.maxRows,
        rawRowLimit: builderState.rawRowLimit,
        sortDirection: "desc",
        includeWarnings: true,
      },
    };
  }

  async function runPreview() {
    setIsPreviewing(true);
    setPreviewError(null);

    try {
      const result = await plantProcessApi.queryDashboardWidget(buildQuery());
      setPreview(result);
    } catch (error) {
      setPreview(null);
      setPreviewError(error);
    } finally {
      setIsPreviewing(false);
    }
  }

  async function saveWidget() {
    if (!effectiveDashboardDefinitionId) {
      setPreviewError(new Error("No dashboard definition is selected."));
      return;
    }

    if (validationIssues.length > 0) {
      setPreviewError(new Error("Fix validation issues before saving."));
      return;
    }

    setIsSaving(true);
    setPreviewError(null);

    try {
      const filterJson = JSON.stringify(cleanFilters());
      const displayOptionsJson = JSON.stringify({
        maxRows: builderState.maxRows,
        rawRowLimit: builderState.rawRowLimit,
      });

      if (existingWidget) {
        await plantProcessApi.updateDashboardWidgetDefinition(
          effectiveDashboardDefinitionId,
          existingWidget.id,
          {
            widgetTitle: builderState.widgetTitle.trim(),
            widgetType: builderState.widgetType,
            chartType: builderState.chartTypeCode!,
            dimensionCode: builderState.dimensionCode!,
            measureCode: builderState.measureCode!,
            parameterCode:
              builderState.parameterCode || builderState.filters.parameterCode || null,
            filterJson,
            displayOptionsJson,
            isActive: true,
          }
        );

        await onWidgetSaved?.(existingWidget.id);
      } else {
        const saved = await plantProcessApi.createDashboardWidgetDefinition(
          effectiveDashboardDefinitionId,
          {
            widgetCode: generateWidgetCode(builderState.widgetTitle),
            widgetTitle: builderState.widgetTitle.trim(),
            widgetType: builderState.widgetType,
            chartType: builderState.chartTypeCode!,
            dimensionCode: builderState.dimensionCode!,
            measureCode: builderState.measureCode!,
            parameterCode:
              builderState.parameterCode || builderState.filters.parameterCode || null,
            filterJson,
            layoutJson: "{}",
            displayOptionsJson,
            sortOrder: 100,
            isSynthetic: false,
            sourceSystem: "PlantProcessIQ.UserDashboard",
            sourceRecordId: null,
          }
        );

        await onWidgetSaved?.(saved.id);
      }

      onClose();
    } catch (error) {
      setPreviewError(error);
    } finally {
      setIsSaving(false);
    }
  }

  function goNext() {
    if (!canGoNext) return;

    const next = stepOrder[currentStepIndex + 1];
    setStep(next);

    if (next === "preview") {
      void runPreview();
    }
  }

  function goBack() {
    if (!canGoBack) return;
    setStep(stepOrder[currentStepIndex - 1]);
  }

  if (!isOpen) return null;

  const previewRows = (preview?.rows ?? []) as ChartRow[];
  const categoryKey = inferCategoryKey(preview);
  const valueKey = inferValueKey(preview);

  return (
    <div className="wizard-backdrop" role="presentation">
      <div className="widget-builder-modal" role="dialog" aria-modal="true">
        <header className="widget-builder-header">
          <div>
            <p className="eyebrow">Dashboard Builder</p>
            <h2>
              {existingWidget ? "Edit dashboard widget" : "Create dashboard widget"}
            </h2>
            <p>
              Metadata-driven widget configuration using the backend dashboard query
              engine.
            </p>
          </div>

          <button className="icon-button" onClick={onClose} type="button">
            <X size={18} />
          </button>
        </header>

        <div className="wizard-progress">
          {stepOrder.map((item, index) => (
            <button
              key={item}
              type="button"
              className={`wizard-progress-step ${
                item === step ? "active" : index < currentStepIndex ? "done" : ""
              }`}
              onClick={() => setStep(item)}
              disabled={index > currentStepIndex + 1}
            >
              <span>{index + 1}</span>
              {item}
            </button>
          ))}
        </div>

        {isLoading ? (
          <div className="empty-insight">
            <strong>Loading widget metadata...</strong>
          </div>
        ) : null}

        {loadError ? (
          <div className="error-panel">
            <strong>Failed to load wizard metadata</strong>
            <p>{formatError(loadError)}</p>
          </div>
        ) : null}

        {!isLoading && !loadError ? (
          <main className="widget-builder-body">
            {step === "purpose" ? (
              <PurposeStep
                metadata={metadata}
                selectedPurposeCode={builderState.purposeCode}
                onSelect={(purposeCode) => {
                  const purpose = metadata?.purposes.find((x) => x.code === purposeCode);

                  patchState({
                    purposeCode,
                    dimensionCode:
                      builderState.dimensionCode ||
                      purpose?.recommendedDimensions[0],
                    measureCode:
                      builderState.measureCode || purpose?.recommendedMeasures[0],
                    chartTypeCode:
                      builderState.chartTypeCode ||
                      purpose?.recommendedChartTypes[0],
                  });
                }}
              />
            ) : null}

            {step === "chartType" ? (
              <ChartTypeStep
                chartTypes={metadata?.chartTypes ?? []}
                selectedChartTypeCode={builderState.chartTypeCode}
                onSelect={(chartTypeCode) => {
                  const currentDimensionStillCompatible =
                    metadata?.dimensions
                      .find((x) => x.code === builderState.dimensionCode)
                      ?.compatibleChartTypes.includes(chartTypeCode) ?? false;

                  const currentMeasureStillCompatible =
                    metadata?.measures
                      .find((x) => x.code === builderState.measureCode)
                      ?.compatibleChartTypes.includes(chartTypeCode) ?? false;

                  patchState({
                    chartTypeCode,
                    dimensionCode: currentDimensionStillCompatible
                      ? builderState.dimensionCode
                      : undefined,
                    measureCode: currentMeasureStillCompatible
                      ? builderState.measureCode
                      : undefined,
                  });
                }}
              />
            ) : null}

            {step === "data" ? (
              <DataStep
                state={builderState}
                chartType={selectedChartType}
                selectedDimension={selectedDimension}
                selectedMeasure={selectedMeasure}
                dimensions={compatibleDimensions}
                measures={compatibleMeasures}
                referenceData={referenceData}
                onPatch={patchState}
              />
            ) : null}

            {step === "filters" ? (
              <FilterStep
                state={builderState}
                referenceData={referenceData}
                onPatch={patchState}
                onPatchFilters={patchFilters}
              />
            ) : null}

            {step === "preview" ? (
              <PreviewStep
                preview={preview}
                previewRows={previewRows}
                categoryKey={categoryKey}
                valueKey={valueKey}
                chartType={builderState.chartTypeCode}
                title={builderState.widgetTitle}
                dimensionCode={builderState.dimensionCode}
                isPreviewing={isPreviewing}
                previewError={previewError}
                onPreview={runPreview}
              />
            ) : null}

            {validationIssues.length > 0 ? (
              <div className="wizard-validation">
                {validationIssues.map((issue) => (
                  <div key={`${issue.field}-${issue.message}`}>
                    <strong>{issue.field}:</strong> {issue.message}
                  </div>
                ))}
              </div>
            ) : null}

            {previewError ? (
              <div className="wizard-validation danger">
                {mapValidationIssues(previewError).map((issue) => (
                  <div key={`${issue.field}-${issue.message}`}>
                    <strong>{issue.field}:</strong> {issue.message}
                  </div>
                ))}
              </div>
            ) : null}
          </main>
        ) : null}

        <footer className="widget-builder-footer">
          <button
            className="secondary-button"
            onClick={goBack}
            disabled={!canGoBack}
            type="button"
          >
            <ArrowLeft size={16} />
            Back
          </button>

          <div className="wizard-footer-actions">
            {step === "preview" ? (
              <>
                <button
                  className="secondary-button"
                  onClick={runPreview}
                  disabled={isPreviewing}
                  type="button"
                >
                  <Eye size={16} />
                  {isPreviewing ? "Previewing..." : "Refresh preview"}
                </button>

                <button
                  className="primary-button"
                  onClick={saveWidget}
                  disabled={isSaving || validationIssues.length > 0}
                  type="button"
                >
                  <Save size={16} />
                  {isSaving ? "Saving..." : existingWidget ? "Update widget" : "Save widget"}
                </button>
              </>
            ) : (
              <button
                className="primary-button"
                onClick={goNext}
                disabled={!canGoNext}
                type="button"
              >
                Next
                <ArrowRight size={16} />
              </button>
            )}
          </div>
        </footer>
      </div>
    </div>
  );
}

function PurposeStep({
  metadata,
  selectedPurposeCode,
  onSelect,
}: {
  metadata: DashboardMetadata | null;
  selectedPurposeCode?: string;
  onSelect: (purposeCode: string) => void;
}) {
  return (
    <WizardSection
      icon={<Sparkles size={18} />}
      title="1. Business purpose"
      description="Choose the reason this widget exists. This helps preselect compatible dimensions, measures, and chart types."
    >
      <div className="wizard-card-grid">
        {(metadata?.purposes ?? []).map((purpose) => (
          <button
            key={purpose.code}
            className={`wizard-choice-card ${
              selectedPurposeCode === purpose.code ? "selected" : ""
            }`}
            onClick={() => onSelect(purpose.code)}
            type="button"
          >
            <strong>{purpose.label}</strong>
            <span>{purpose.description}</span>
          </button>
        ))}
      </div>
    </WizardSection>
  );
}

function ChartTypeStep({
  chartTypes,
  selectedChartTypeCode,
  onSelect,
}: {
  chartTypes: DashboardChartTypeMetadata[];
  selectedChartTypeCode?: string;
  onSelect: (chartTypeCode: string) => void;
}) {
  return (
    <WizardSection
      icon={<BarChart3 size={18} />}
      title="2. Chart type"
      description="Only chart types supported by the backend metadata engine are shown."
    >
      <div className="wizard-card-grid">
        {chartTypes.map((chartType) => (
          <button
            key={chartType.code}
            className={`wizard-choice-card ${
              selectedChartTypeCode === chartType.code ? "selected" : ""
            }`}
            onClick={() => onSelect(chartType.code)}
            type="button"
          >
            <strong>{chartType.label}</strong>
            <span>{chartType.description ?? chartType.category}</span>
          </button>
        ))}
      </div>
    </WizardSection>
  );
}

function DataStep({
  state,
  chartType,
  selectedDimension,
  selectedMeasure,
  dimensions,
  measures,
  referenceData,
  onPatch,
}: {
  state: WidgetBuilderState;
  chartType?: DashboardChartTypeMetadata;
  selectedDimension?: DashboardDimensionMetadata;
  selectedMeasure?: DashboardMeasureMetadata;
  dimensions: DashboardDimensionMetadata[];
  measures: DashboardMeasureMetadata[];
  referenceData: DashboardReferenceData | null;
  onPatch: (patch: Partial<WidgetBuilderState>) => void;
}) {
  const parameterRequired =
    selectedDimension?.requiresParameterCode ||
    selectedMeasure?.requiresParameterCode ||
    chartType?.supportsParameterSelection;

  return (
    <WizardSection
      icon={<CheckCircle2 size={18} />}
      title="3. Dimension and measure"
      description="Choose backend-approved fields. Incompatible combinations are blocked before preview."
    >
      <div className="form-grid">
        <label>
          Widget title
          <input
            value={state.widgetTitle}
            onChange={(event) => onPatch({ widgetTitle: event.target.value })}
            placeholder="Example: Defect rate by equipment"
          />
        </label>

        <label>
          Widget type
          <select
            value={state.widgetType}
            onChange={(event) => onPatch({ widgetType: event.target.value })}
          >
            <option value="chart">Chart</option>
            <option value="kpi">KPI</option>
            <option value="table">Table</option>
          </select>
        </label>

        <label>
          Dimension
          <select
            value={state.dimensionCode ?? ""}
            onChange={(event) =>
              onPatch({ dimensionCode: event.target.value || undefined })
            }
          >
            <option value="">Select dimension</option>
            {dimensions.map((dimension) => (
              <option key={dimension.code} value={dimension.code}>
                {dimension.label} Â· {dimension.category}
              </option>
            ))}
          </select>
        </label>

        <label>
          Measure
          <select
            value={state.measureCode ?? ""}
            onChange={(event) =>
              onPatch({ measureCode: event.target.value || undefined })
            }
          >
            <option value="">Select measure</option>
            {measures.map((measure) => (
              <option key={measure.code} value={measure.code}>
                {measure.label} Â· {measure.aggregation}
              </option>
            ))}
          </select>
        </label>

        {parameterRequired ? (
          <label>
            Process parameter
            <select
              value={state.parameterCode ?? ""}
              onChange={(event) =>
                onPatch({ parameterCode: event.target.value || undefined })
              }
            >
              <option value="">Select parameter</option>
              {(referenceData?.parameters ?? []).map((item) => (
                <option key={item.code} value={item.code}>
                  {item.name} Â· {item.code}
                </option>
              ))}
            </select>
          </label>
        ) : null}

        <label>
          Max rows
          <input
            type="number"
            min={1}
            max={500}
            value={state.maxRows}
            onChange={(event) => onPatch({ maxRows: Number(event.target.value) })}
          />
        </label>

        <label>
          Raw row limit
          <input
            type="number"
            min={1}
            max={5000}
            value={state.rawRowLimit}
            onChange={(event) =>
              onPatch({ rawRowLimit: Number(event.target.value) })
            }
          />
        </label>
      </div>
    </WizardSection>
  );
}

function FilterStep({
  state,
  referenceData,
  onPatch,
  onPatchFilters,
}: {
  state: WidgetBuilderState;
  referenceData: DashboardReferenceData | null;
  onPatch: (patch: Partial<WidgetBuilderState>) => void;
  onPatchFilters: (patch: Partial<DashboardWidgetFilters>) => void;
}) {
  return (
    <WizardSection
      icon={<Filter size={18} />}
      title="4. Filters"
      description="Filters are stored inside the widget definition and applied every time the backend executes the widget query."
    >
      <div className="form-grid">
        <label>
          Site
          <select
            value={state.filters.siteId ?? ""}
            onChange={(event) => onPatchFilters({ siteId: event.target.value || null })}
          >
            <option value="">All sites</option>
            {(referenceData?.sites ?? []).map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Area
          <select
            value={state.filters.areaId ?? ""}
            onChange={(event) => onPatchFilters({ areaId: event.target.value || null })}
          >
            <option value="">All areas</option>
            {(referenceData?.areas ?? []).map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Equipment
          <select
            value={state.filters.equipmentId ?? ""}
            onChange={(event) =>
              onPatchFilters({ equipmentId: event.target.value || null })
            }
          >
            <option value="">All equipment</option>
            {(referenceData?.equipment ?? []).map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Material type
          <input
            value={state.filters.materialUnitType ?? ""}
            onChange={(event) =>
              onPatchFilters({ materialUnitType: event.target.value || null })
            }
            placeholder="Example: Coil, Batch, Slab, Roll"
          />
        </label>

        <label>
          Defect type
          <select
            value={state.filters.defectType ?? ""}
            onChange={(event) =>
              onPatchFilters({ defectType: event.target.value || null })
            }
          >
            <option value="">All defects</option>
            {(referenceData?.defects ?? []).map((item) => (
              <option key={item.code} value={item.code}>
                {item.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Risk class
          <select
            value={state.filters.riskClass ?? ""}
            onChange={(event) =>
              onPatchFilters({ riskClass: event.target.value || null })
            }
          >
            <option value="">All risk classes</option>
            {(referenceData?.riskClasses ?? []).map((item) => (
              <option key={item.code} value={item.code}>
                {item.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Source system
          <select
            value={state.filters.sourceSystem ?? ""}
            onChange={(event) =>
              onPatchFilters({ sourceSystem: event.target.value || null })
            }
          >
            <option value="">All source systems</option>
            {(referenceData?.sourceSystems ?? []).map((item) => (
              <option key={item.code} value={item.code}>
                {item.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Shift / crew
          <select
            value={state.filters.shiftCode ?? ""}
            onChange={(event) =>
              onPatchFilters({ shiftCode: event.target.value || null })
            }
          >
            <option value="">All shifts</option>
            {(referenceData?.shifts ?? []).map((item) => (
              <option key={item.code} value={item.code}>
                {item.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Date filter mode
          <select
            value={state.dateMode}
            onChange={(event) =>
              onPatch({
                dateMode: event.target.value as WidgetBuilderState["dateMode"],
              })
            }
          >
            <option value="none">No date filter</option>
            <option value="relative">Relative lookback</option>
            <option value="absolute">Absolute date range</option>
          </select>
        </label>

        {state.dateMode === "relative" ? (
          <>
            <label>
              Last
              <input
                type="number"
                min={1}
                value={state.relativeDateValue}
                onChange={(event) =>
                  onPatch({ relativeDateValue: Number(event.target.value) })
                }
              />
            </label>

            <label>
              Unit
              <select
                value={state.relativeDateUnit}
                onChange={(event) =>
                  onPatch({
                    relativeDateUnit: event.target.value as RelativeDateUnit,
                  })
                }
              >
                <option value="days">Days</option>
                <option value="weeks">Weeks</option>
                <option value="months">Months</option>
              </select>
            </label>
          </>
        ) : null}

        {state.dateMode === "absolute" ? (
          <>
            <label>
              From
              <input
                type="datetime-local"
                value={toInputDateTime(state.filters.fromUtc)}
                onChange={(event) =>
                  onPatchFilters({ fromUtc: fromInputDateTime(event.target.value) })
                }
              />
            </label>

            <label>
              To
              <input
                type="datetime-local"
                value={toInputDateTime(state.filters.toUtc)}
                onChange={(event) =>
                  onPatchFilters({ toUtc: fromInputDateTime(event.target.value) })
                }
              />
            </label>
          </>
        ) : null}
      </div>
    </WizardSection>
  );
}

function PreviewStep({
  preview,
  previewRows,
  categoryKey,
  valueKey,
  chartType,
  title,
  dimensionCode,
  isPreviewing,
  previewError,
  onPreview,
}: {
  preview: DashboardWidgetQueryResult | null;
  previewRows: ChartRow[];
  categoryKey: string;
  valueKey: string;
  chartType?: string;
  title: string;
  dimensionCode?: string | null;
  isPreviewing: boolean;
  previewError: unknown;
  onPreview: () => void;
}) {
  return (
    <WizardSection
      icon={<Eye size={18} />}
      title="5. Preview"
      description="The preview calls the backend widget query endpoint with the complete DTO."
    >
      <div className="preview-toolbar">
        <button
          className="secondary-button"
          onClick={onPreview}
          disabled={isPreviewing}
          type="button"
        >
          <Eye size={16} />
          {isPreviewing ? "Previewing..." : "Run preview"}
        </button>

        {preview ? (
          <span className="muted-text">
            {preview.rows.length} row(s), generated {preview.generatedAtUtc}
          </span>
        ) : null}
      </div>

      {previewError ? null : !preview && !isPreviewing ? (
        <EmptyInsightState />
      ) : null}

      {isPreviewing ? (
        <div className="empty-insight">
          <strong>Running backend preview...</strong>
        </div>
      ) : null}

      {preview ? (
        chartType === "line" || chartType === "area" ? (
          <InteractiveLineChart
            data={previewRows}
            categoryKey={categoryKey}
            valueKey={valueKey}
            area={chartType === "area"}
            selection={{
              type: "generic",
              field: selectFieldForDimension(dimensionCode) as any,
              sourceWidget: title,
              valueKey: categoryKey,
              labelKey: "dimensionLabel",
            }}
          />
        ) : chartType === "pie" || chartType === "donut" ? (
          <InteractivePieChart
            data={previewRows}
            categoryKey={categoryKey}
            valueKey={valueKey}
            donut={chartType === "donut"}
            selection={{
              type: "generic",
              field: selectFieldForDimension(dimensionCode) as any,
              sourceWidget: title,
              valueKey: categoryKey,
              labelKey: "dimensionLabel",
            }}
          />
        ) : chartType === "table" ? (
          <PreviewTable rows={previewRows} />
        ) : (
          <InteractiveBarChart
            data={previewRows}
            categoryKey={categoryKey}
            valueKey={valueKey}
            selection={{
              type: "generic",
              field: selectFieldForDimension(dimensionCode) as any,
              sourceWidget: title,
              valueKey: categoryKey,
              labelKey: "dimensionLabel",
            }}
          />
        )
      ) : null}

      {preview?.warnings?.length ? (
        <div className="wizard-warning">
          {preview.warnings.map((warning) => (
            <div key={warning}>{warning}</div>
          ))}
        </div>
      ) : null}
    </WizardSection>
  );
}

function PreviewTable({ rows }: { rows: ChartRow[] }) {
  if (!rows.length) return <EmptyInsightState />;

  const columns = Object.keys(rows[0] ?? {});

  return (
    <div className="table-shell">
      <table>
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column}>{column}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.slice(0, 50).map((row, index) => (
            <tr key={index}>
              {columns.map((column) => (
                <td key={column}>{String(row[column] ?? "")}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function WizardSection({
  icon,
  title,
  description,
  children,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
  children: React.ReactNode;
}) {
  return (
    <section className="wizard-section">
      <div className="wizard-section-header">
        <span>{icon}</span>
        <div>
          <h3>{title}</h3>
          <p>{description}</p>
        </div>
      </div>
      {children}
    </section>
  );
}

