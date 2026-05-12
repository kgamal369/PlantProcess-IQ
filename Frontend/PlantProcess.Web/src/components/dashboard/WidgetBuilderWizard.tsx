import { ArrowLeft, ArrowRight, BarChart3, CheckCircle2, Sparkles, X } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { plantProcessApi } from "../../api/plantProcessApi";
import type {
  DashboardChartTypeMetadata,
  DashboardMetadata,
  DashboardPurposeMetadata,
} from "../../api/plantProcessApi";

interface WidgetBuilderWizardProps {
  isOpen: boolean;
  onClose: () => void;
}

type WizardStep = "purpose" | "chartType";

interface WidgetBuilderState {
  purposeCode?: string;
  chartTypeCode?: string;
}

const stepOrder: WizardStep[] = ["purpose", "chartType"];

export function WidgetBuilderWizard({
  isOpen,
  onClose,
}: WidgetBuilderWizardProps) {
  const [metadata, setMetadata] = useState<DashboardMetadata | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [loadError, setLoadError] = useState<unknown>(null);
  const [step, setStep] = useState<WizardStep>("purpose");
  const [builderState, setBuilderState] = useState<WidgetBuilderState>({});

  useEffect(() => {
    if (!isOpen) return;

    let cancelled = false;

    async function loadMetadata() {
      try {
        setIsLoading(true);
        setLoadError(null);

        const result = await plantProcessApi.getDashboardMetadata();

        if (!cancelled) {
          setMetadata(result);
        }
      } catch (error) {
        if (!cancelled) {
          setLoadError(error);
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    loadMetadata();

    return () => {
      cancelled = true;
    };
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) {
      setStep("purpose");
      setBuilderState({});
      setLoadError(null);
    }
  }, [isOpen]);

  const selectedPurpose = useMemo(
    () =>
      metadata?.purposes.find(
        (purpose) => purpose.code === builderState.purposeCode
      ),
    [metadata, builderState.purposeCode]
  );

  const availableChartTypes = useMemo(() => {
    if (!metadata) return [];

    if (!selectedPurpose) {
      return metadata.chartTypes;
    }

    const allowed = new Set(
      selectedPurpose.recommendedChartTypes.map((item) => item.toLowerCase())
    );

    return metadata.chartTypes.filter((chartType) =>
      allowed.has(chartType.code.toLowerCase())
    );
  }, [metadata, selectedPurpose]);

  if (!isOpen) {
    return null;
  }

  const currentStepIndex = stepOrder.indexOf(step);
  const canGoBack = currentStepIndex > 0;
  const canGoNext =
    step === "purpose"
      ? Boolean(builderState.purposeCode)
      : Boolean(builderState.chartTypeCode);

  function goBack() {
    if (!canGoBack) return;

    setStep(stepOrder[currentStepIndex - 1]);
  }

  function goNext() {
    if (!canGoNext) return;

    if (step === "purpose") {
      setStep("chartType");
      return;
    }

    // Tasks 20-32 stop at chart-type selection.
    // Dimension, measure, filters, preview and save come in tasks 33-36.
    onClose();
  }

  function selectPurpose(purpose: DashboardPurposeMetadata) {
    setBuilderState((current) => ({
      ...current,
      purposeCode: purpose.code,
      chartTypeCode: current.chartTypeCode,
    }));
  }

  function selectChartType(chartType: DashboardChartTypeMetadata) {
    setBuilderState((current) => ({
      ...current,
      chartTypeCode: chartType.code,
    }));
  }

  return (
    <div className="wizard-backdrop" role="presentation">
      <aside className="widget-builder-wizard" role="dialog" aria-modal="true">
        <header className="widget-builder-wizard__header">
          <div>
            <div className="eyebrow">
              <Sparkles size={14} />
              Dynamic dashboard builder
            </div>
            <h2>Add analysis widget</h2>
            <p>
              Build a metadata-driven widget. Frontend selects fields; backend
              executes safe aggregated queries only.
            </p>
          </div>

          <button
            className="wizard-icon-button"
            onClick={onClose}
            type="button"
            aria-label="Close widget builder"
          >
            <X size={18} />
          </button>
        </header>

        <div className="wizard-progress">
          <div
            className={`wizard-progress__item ${
              step === "purpose" ? "wizard-progress__item--active" : ""
            } ${builderState.purposeCode ? "wizard-progress__item--done" : ""}`}
          >
            <span>1</span>
            Purpose
          </div>

          <div
            className={`wizard-progress__item ${
              step === "chartType" ? "wizard-progress__item--active" : ""
            } ${
              builderState.chartTypeCode ? "wizard-progress__item--done" : ""
            }`}
          >
            <span>2</span>
            Chart type
          </div>

          <div className="wizard-progress__item wizard-progress__item--locked">
            <span>3</span>
            Dimension & measure
          </div>

          <div className="wizard-progress__item wizard-progress__item--locked">
            <span>4</span>
            Preview
          </div>
        </div>

        {isLoading ? (
          <div className="wizard-state-panel">
            <strong>Loading metadata...</strong>
            <p>Reading dashboard dimensions, measures and chart rules.</p>
          </div>
        ) : null}

        {loadError ? (
          <div className="wizard-state-panel wizard-state-panel--error">
            <strong>Could not load dashboard metadata</strong>
            <p>
              {loadError instanceof Error
                ? loadError.message
                : "Unknown metadata loading error."}
            </p>
          </div>
        ) : null}

        {!isLoading && !loadError && metadata ? (
          <div className="widget-builder-wizard__body">
            {step === "purpose" ? (
              <PurposeStep
                purposes={metadata.purposes}
                selectedPurposeCode={builderState.purposeCode}
                onSelect={selectPurpose}
              />
            ) : null}

            {step === "chartType" ? (
              <ChartTypeStep
                chartTypes={availableChartTypes}
                selectedChartTypeCode={builderState.chartTypeCode}
                selectedPurpose={selectedPurpose}
                onSelect={selectChartType}
              />
            ) : null}
          </div>
        ) : null}

        <footer className="widget-builder-wizard__footer">
          <button
            className="secondary-button"
            type="button"
            onClick={goBack}
            disabled={!canGoBack}
          >
            <ArrowLeft size={16} />
            Back
          </button>

          <div className="wizard-selection-summary">
            {selectedPurpose ? <span>{selectedPurpose.label}</span> : null}
            {builderState.chartTypeCode ? (
              <span>{builderState.chartTypeCode}</span>
            ) : null}
          </div>

          <button
            className="primary-button"
            type="button"
            onClick={goNext}
            disabled={!canGoNext}
          >
            {step === "chartType" ? "Finish step 32" : "Next"}
            <ArrowRight size={16} />
          </button>
        </footer>
      </aside>
    </div>
  );
}

function PurposeStep({
  purposes,
  selectedPurposeCode,
  onSelect,
}: {
  purposes: DashboardPurposeMetadata[];
  selectedPurposeCode?: string;
  onSelect: (purpose: DashboardPurposeMetadata) => void;
}) {
  return (
    <section className="wizard-step">
      <div className="wizard-step__heading">
        <h3>What do you want to analyze?</h3>
        <p>
          Choose the investigation purpose. This will later narrow the available
          dimensions, measures and recommended charts.
        </p>
      </div>

      <div className="wizard-option-grid">
        {purposes.map((purpose) => {
          const selected = selectedPurposeCode === purpose.code;

          return (
            <button
              key={purpose.code}
              className={`wizard-option-card ${
                selected ? "wizard-option-card--selected" : ""
              }`}
              onClick={() => onSelect(purpose)}
              type="button"
            >
              <div className="wizard-option-card__top">
                <span className="wizard-option-card__icon">
                  <Sparkles size={18} />
                </span>

                {selected ? (
                  <CheckCircle2 className="wizard-option-card__check" size={18} />
                ) : null}
              </div>

              <strong>{purpose.label}</strong>
              <p>{purpose.description}</p>

              <small>
                Recommended: {purpose.recommendedChartTypes.join(", ")}
              </small>
            </button>
          );
        })}
      </div>
    </section>
  );
}

function ChartTypeStep({
  chartTypes,
  selectedChartTypeCode,
  selectedPurpose,
  onSelect,
}: {
  chartTypes: DashboardChartTypeMetadata[];
  selectedChartTypeCode?: string;
  selectedPurpose?: DashboardPurposeMetadata;
  onSelect: (chartType: DashboardChartTypeMetadata) => void;
}) {
  return (
    <section className="wizard-step">
      <div className="wizard-step__heading">
        <h3>Select visualization type</h3>
        <p>
          {selectedPurpose
            ? `Recommended chart types for ${selectedPurpose.label}.`
            : "Choose how the widget should present the analysis result."}
        </p>
      </div>

      <div className="wizard-option-grid wizard-option-grid--compact">
        {chartTypes.map((chartType) => {
          const selected = selectedChartTypeCode === chartType.code;

          return (
            <button
              key={chartType.code}
              className={`wizard-option-card ${
                selected ? "wizard-option-card--selected" : ""
              }`}
              onClick={() => onSelect(chartType)}
              type="button"
            >
              <div className="wizard-option-card__top">
                <span className="wizard-option-card__icon">
                  <BarChart3 size={18} />
                </span>

                {selected ? (
                  <CheckCircle2 className="wizard-option-card__check" size={18} />
                ) : null}
              </div>

              <strong>{chartType.label}</strong>
              <p>{chartType.description}</p>

              <small>
                {chartType.supportsDimension ? "Dimension" : "No dimension"} ·{" "}
                {chartType.supportsParameterSelection
                  ? "Parameter aware"
                  : "Standard"}
              </small>
            </button>
          );
        })}
      </div>
    </section>
  );
}