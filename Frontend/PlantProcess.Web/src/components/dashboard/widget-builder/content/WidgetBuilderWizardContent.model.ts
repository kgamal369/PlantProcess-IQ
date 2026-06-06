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
  WidgetBuilderWizardProps, WidgetBuilderState,
    WizardStep,
    ValidationIssue,
} from "./WidgetBuilderWizardContent.types";
import {
  defaultState,
    formatError,
    fromInputDateTime,
    generateWidgetCode,
    inferCategoryKey,
    inferValueKey,
    isCompatible,
    mapValidationIssues,
    relativeFromUtc,
    stepOrder,
    toInputDateTime,
    parseJson,
} from "./WidgetBuilderWizardContent.helpers";

export function useWidgetBuilderWizardContentModel(props: WidgetBuilderWizardProps) {
  const { isOpen, dashboardDefinitionId, existingWidget, onClose, onWidgetSaved } = props;
  const [metadata, setMetadata] = useState<DashboardMetadata | null>(null);
  const [referenceData, setReferenceData] =
    useState<DashboardReferenceData | null>(null);
  const [dashboards, setDashboards] = useState<DashboardDefinitionRecord[]>([]);

  const [isLoading, setIsLoading] = useState(false);
  const [isPreviewing, setIsPreviewing] = useState(false);

  const [loadError, setLoadError] = useState<unknown>(null);
  const [previewError, setPreviewError] = useState<unknown>(null);
  const [preview, setPreview] = useState<DashboardWidgetQueryResult | null>(
    null
  );

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
            productApi.getDashboardMetadata(),
            productApi.getDashboardReferenceData(),
            productApi.getDashboardDefinitions(false, true),
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
            queryExpression?: string;
            expressionEnabled?: boolean;
            expressionVersion?: string;
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
            queryExpression: displayOptions.queryExpression ?? "",
            expressionEnabled: Boolean(
              displayOptions.expressionEnabled && displayOptions.queryExpression
            ),
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
    () =>
      metadata?.chartTypes.find(
        (item) => item.code === builderState.chartTypeCode
      ),
    [metadata, builderState.chartTypeCode]
  );

  const selectedDimension = useMemo(
    () =>
      metadata?.dimensions.find(
        (item) => item.code === builderState.dimensionCode
      ),
    [metadata, builderState.dimensionCode]
  );

  const selectedMeasure = useMemo(
    () =>
      metadata?.measures.find((item) => item.code === builderState.measureCode),
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
        new Date(builderState.filters.fromUtc) >
          new Date(builderState.filters.toUtc)
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

    if (
      step === "script" &&
      builderState.expressionEnabled &&
      !builderState.queryExpression.trim()
    ) {
      issues.push({
        field: "Safe expression",
        message:
          "Expression mode is enabled, but the expression is empty. Run a valid expression preview or disable expression mode.",
      });
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
  const canGoNext =
    validationIssues.length === 0 && currentStepIndex < stepOrder.length - 1;

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
      parameterCode:
        builderState.parameterCode || builderState.filters.parameterCode || null,
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
    const options: DashboardWidgetQueryOptions = {
      maxRows: builderState.maxRows,
      rawRowLimit: builderState.rawRowLimit,
      sortDirection: "desc",
      includeWarnings: true,
    };

    const result =
      builderState.expressionEnabled && builderState.queryExpression.trim()
        ? await widgetScriptApi.executeExpression({
            expression: builderState.queryExpression,
            filters: cleanFilters(),
            options,
          })
        : await productApi.queryDashboardWidget(buildQuery());

    setPreview(result);
  } catch (error) {
    setPreview(null);
    setPreviewError(error);
  } finally {
    setIsPreviewing(false);
  }
}
  const { isSaving, save: saveWidget } = useOptimisticSave({
    successMessage: existingWidget ? "Widget updated" : "Widget saved",
    toastId: `save-widget-${existingWidget?.id ?? "new"}`,
    onSave: async () => {
      if (!effectiveDashboardDefinitionId) {
        throw new Error("No dashboard definition is selected.");
      }

      if (validationIssues.length > 0) {
        throw new Error("Fix validation issues before saving.");
      }

      const filterJson = JSON.stringify(cleanFilters());
      const displayOptionsJson = JSON.stringify({
        maxRows: builderState.maxRows,
        rawRowLimit: builderState.rawRowLimit,
        queryExpression: builderState.expressionEnabled
          ? builderState.queryExpression.trim()
          : null,
        expressionEnabled:
          builderState.expressionEnabled &&
          builderState.queryExpression.trim().length > 0,
        expressionVersion: "workflow-foundation.v1",
      });

      if (existingWidget) {
        await productApi.updateDashboardWidgetDefinition(
          effectiveDashboardDefinitionId,
          existingWidget.id,
          {
            widgetTitle: builderState.widgetTitle.trim(),
            widgetType: builderState.widgetType,
            chartType: builderState.chartTypeCode!,
            dimensionCode: builderState.dimensionCode!,
            measureCode: builderState.measureCode!,
            parameterCode:
              builderState.parameterCode ||
              builderState.filters.parameterCode ||
              null,
            filterJson,
            displayOptionsJson,
            isActive: true,
          }
        );

        await onWidgetSaved?.(existingWidget.id);
      } else {
        const saved = await productApi.createDashboardWidgetDefinition(
          effectiveDashboardDefinitionId,
          {
            widgetCode: generateWidgetCode(builderState.widgetTitle),
            widgetTitle: builderState.widgetTitle.trim(),
            widgetType: builderState.widgetType,
            chartType: builderState.chartTypeCode!,
            dimensionCode: builderState.dimensionCode!,
            measureCode: builderState.measureCode!,
            parameterCode:
              builderState.parameterCode ||
              builderState.filters.parameterCode ||
              null,
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
    },
    onSuccess: () => {
      onClose();
    },
    onError: (err) => {
      setPreviewError(err);
    },
  });

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
  const shouldRender = isOpen;
  const previewRows = (preview?.rows ?? []) as ChartRow[];
  const categoryKey = inferCategoryKey(preview);
  const valueKey = inferValueKey(preview);

  return {
    saveWidget,
    runPreview,
    buildQuery,
    builderState,
    canGoBack,
    canGoNext,
    categoryKey,
    cleanFilters,
    compatibleDimensions,
    compatibleMeasures,
    currentStepIndex,
    dashboardDefinitionId,
    dashboards,
    effectiveDashboardDefinitionId,
    existingWidget,
    goBack,
    goNext,
    isLoading,
    isOpen,
    isPreviewing,
    isSaving,
    loadError,
    metadata,
    onClose,
    onWidgetSaved,
    patchFilters,
    patchState,
    preview,
    previewError,
    previewRows,
    referenceData,

    selectedChartType,
    selectedDimension,
    selectedMeasure,
    setBuilderState,
    setDashboards,
    setIsLoading,
    setIsPreviewing,
    setLoadError,
    setMetadata,
    setPreview,
    setPreviewError,
    setReferenceData,
    setStep,
    shouldRender,
    step,
    validationIssues,
    valueKey,
  };
}

