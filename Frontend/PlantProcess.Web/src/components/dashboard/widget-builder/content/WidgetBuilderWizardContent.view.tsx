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

import {
  PurposeStep, ChartTypeStep, DataStep
} from "./WidgetBuilderWizardContent.data-steps";
import {
  FilterStep
} from "./WidgetBuilderWizardContent.filter-step";
import {
  ScriptStep, PreviewStep
} from "./WidgetBuilderWizardContent.preview-steps";
import {
  mapValidationIssues, stepLabels, stepOrder,
    formatError,
} from "./WidgetBuilderWizardContent.helpers";

export function WidgetBuilderWizardContentView({ vm }: { vm: Record<string, any> }) {
  const {
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
    save,
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
  } = vm;

  const runPreview = vm.runPreview;
  const saveWidget = vm.saveWidget;


  if (!shouldRender) return null;

  return (
    <div className="wizard-backdrop" role="presentation">
      <div className="widget-builder-modal" role="dialog" aria-modal="true">
        <header className="widget-builder-header">
          <div>
            <p className="eyebrow">Dashboard Builder</p>
            <h2>
              {existingWidget
                ? "Edit dashboard widget"
                : "Create dashboard widget"}
            </h2>
            <p>
              Metadata-driven widget configuration using the backend dashboard
              query engine.
            </p>
          </div>

          <StandardButton className="icon-button" onClick={onClose} type="button">
            <X size={18} />
          </StandardButton>
        </header>

        <div className="wizard-progress">
          {stepOrder.map((item, index) => (
            <StandardButton
              key={item}
              type="button"
              className={`wizard-progress-step ${
                item === step ? "active" : index < currentStepIndex ? "done" : ""
              }`}
              onClick={() => setStep(item)}
              isDisabled={index > currentStepIndex + 1}
            >
              <span>{index + 1}</span>
              {stepLabels[item]}
            </StandardButton>
          ))}
        </div>

        {isLoading ? (
          <div className="empty-insight">
            <strong>Loading widget metadata...</strong>
          </div>
        ) : null}

        {loadError ? (
          <div className="error-panel">
            <strong>Data refresh did not complete wizard metadata</strong>
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
                  const purpose = metadata?.purposes.find(
                    (x) => x.code === purposeCode
                  );

                  patchState({
                    purposeCode,
                    dimensionCode:
                      builderState.dimensionCode ||
                      purpose?.recommendedDimensions[0],
                    measureCode:
                      builderState.measureCode ||
                      purpose?.recommendedMeasures[0],
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

            {step === "script" ? (
              <ScriptStep
                state={builderState}
                filters={cleanFilters()}
                onPatch={patchState}
                onExpressionPreviewAccepted={(expression, result) => {
                  patchState({
                    queryExpression: expression,
                    expressionEnabled: true,
                  });
                  setPreview(result);
                  setPreviewError(null);
                }}
                onDisableExpression={() => {
                  patchState({
                    expressionEnabled: false,
                    queryExpression: "",
                  });
                }}
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
                expressionEnabled={builderState.expressionEnabled}
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
          <StandardButton
            className="secondary-button"
            onClick={goBack}
            isDisabled={!canGoBack}
            type="button"
          >
            <ArrowLeft size={16} />
            Back
          </StandardButton>

          <div className="wizard-footer-actions">
            {step === "preview" ? (
              <>
                <StandardButton
                  className="secondary-button"
                  onClick={runPreview}
                  isDisabled={isPreviewing}
                  type="button"
                >
                  <Eye size={16} />
                  {isPreviewing ? "Previewing..." : "Refresh preview"}
                </StandardButton>

                <StandardButton
                  className="primary-button"
                  onClick={saveWidget}
                  isDisabled={isSaving || validationIssues.length > 0}
                  type="button"
                >
                  <Save size={16} />
                  {isSaving
                    ? "Saving..."
                    : existingWidget
                      ? "Update widget"
                      : "Save widget"}
                </StandardButton>
              </>
            ) : (
              <StandardButton
                className="primary-button"
                onClick={goNext}
                isDisabled={!canGoNext}
                type="button"
              >
                Next
                <ArrowRight size={16} />
              </StandardButton>
            )}
          </div>
        </footer>
      </div>
    </div>
  );
}
