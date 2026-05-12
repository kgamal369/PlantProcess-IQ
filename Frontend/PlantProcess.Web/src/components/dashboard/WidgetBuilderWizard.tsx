import {
  ArrowLeft,
  ArrowRight,
  BarChart3,
  CheckCircle2,
  Eye,
  Save,
  Sparkles,
  X,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { plantProcessApi } from "../../api/plantProcessApi";
import type {
  DashboardChartTypeMetadata,
  DashboardDimensionMetadata,
  DashboardMeasureMetadata,
  DashboardMetadata,
  DashboardPurposeMetadata,
  DashboardReferenceData,
  DashboardWidgetFilters,
  DashboardWidgetQuery,
  DashboardWidgetQueryResult,
} from "../../api/plantProcessApi";

interface WidgetBuilderWizardProps {
  isOpen: boolean;
  dashboardDefinitionId?: string | null;
  onClose: () => void;
  onWidgetSaved?: (widgetId: string) => void;
}

type WizardStep = "purpose" | "chartType" | "data" | "filters" | "preview";

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
}

const stepOrder: WizardStep[] = ["purpose", "chartType", "data", "filters", "preview"];

function generateWidgetCode(title: string) {
  const slug = title
    .trim()
    .replace(/[^a-zA-Z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "")
    .toUpperCase();

  return `${slug || "WIDGET"}_${Date.now()}`;
}

export function WidgetBuilderWizard({
  isOpen,
  dashboardDefinitionId,
  onClose,
  onWidgetSaved,
}: WidgetBuilderWizardProps) {
  const [metadata, setMetadata] = useState<DashboardMetadata | null>(null);
  const [referenceData, setReferenceData] = useState<DashboardReferenceData | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isPreviewing, setIsPreviewing] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [loadError, setLoadError] = useState<unknown>(null);
  const [previewError, setPreviewError] = useState<unknown>(null);
  const [preview, setPreview] = useState<DashboardWidgetQueryResult | null>(null);
  const [step, setStep] = useState<WizardStep>("purpose");
  const [builderState, setBuilderState] = useState<WidgetBuilderState>({
    widgetTitle: "",
    widgetType: "chart",
    filters: {},
    maxRows: 100,
    rawRowLimit: 500,
  });

  useEffect(() => {
    if (!isOpen) return;

    let ignore = false;

    async function loadMetadata() {
      setIsLoading(true);
      setLoadError(null);

      try {
        const [metadataResult, referenceResult] = await Promise.all([
          plantProcessApi.getDashboardMetadata(),
          plantProcessApi.getDashboardReferenceData(),
        ]);

        if (!ignore) {
          setMetadata(metadataResult);
          setReferenceData(referenceResult);
        }
      } catch (error) {
        if (!ignore) setLoadError(error);
      } finally {
        if (!ignore) setIsLoading(false);
      }
    }

    loadMetadata();

    return () => {
      ignore = true;
    };
  }, [isOpen]);

  const compatibleDimensions = useMemo(() => {
    if (!metadata || !builderState.chartTypeCode) return [];
    return metadata.dimensions.filter((dimension) =>
      dimension.compatibleChartTypes.includes(builderState.chartTypeCode!)
    );
  }, [metadata, builderState.chartTypeCode]);

  const compatibleMeasures = useMemo(() => {
    if (!metadata || !builderState.chartTypeCode) return [];
    return metadata.measures.filter((measure) =>
      measure.compatibleChartTypes.includes(builderState.chartTypeCode!)
    );
  }, [metadata, builderState.chartTypeCode]);

  const selectedMeasure = useMemo(
    () => metadata?.measures.find((x) => x.code === builderState.measureCode),
    [metadata, builderState.measureCode]
  );

  if (!isOpen) return null;

  function patchState(patch: Partial<WidgetBuilderState>) {
    setBuilderState((current) => ({
      ...current,
      ...patch,
      filters: patch.filters ? { ...current.filters, ...patch.filters } : current.filters,
    }));
  }

  function selectPurpose(purpose: DashboardPurposeMetadata) {
    const nextChart =
      purpose.recommendedChartTypes[0] ??
      metadata?.chartTypes.find((x) => x.code !== "kpi")?.code;

    const nextDimension = purpose.recommendedDimensions[0];
    const nextMeasure = purpose.recommendedMeasures[0];

    patchState({
      purposeCode: purpose.code,
      chartTypeCode: nextChart,
      dimensionCode: nextDimension,
      measureCode: nextMeasure,
      widgetTitle: builderState.widgetTitle || purpose.label,
    });
  }

  function selectChart(chart: DashboardChartTypeMetadata) {
    const nextDimension = compatibleDimensions.some((x) => x.code === builderState.dimensionCode)
      ? builderState.dimensionCode
      : metadata?.dimensions.find((x) => x.compatibleChartTypes.includes(chart.code))?.code;

    const nextMeasure = compatibleMeasures.some((x) => x.code === builderState.measureCode)
      ? builderState.measureCode
      : metadata?.measures.find((x) => x.compatibleChartTypes.includes(chart.code))?.code;

    patchState({
      chartTypeCode: chart.code,
      widgetType: chart.code === "table" ? "table" : "chart",
      dimensionCode: nextDimension,
      measureCode: nextMeasure,
    });
  }

  function buildQuery(): DashboardWidgetQuery {
    return {
      widgetType: builderState.widgetType || "chart",
      chartType: builderState.chartTypeCode || "bar",
      dimensionCode: builderState.dimensionCode || null,
      measureCode: builderState.measureCode || null,
      parameterCode: builderState.parameterCode || builderState.filters.parameterCode || null,
      filters: {
        ...builderState.filters,
        parameterCode: builderState.parameterCode || builderState.filters.parameterCode || null,
      },
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
    setPreview(null);

    try {
      const result = await plantProcessApi.queryDashboardWidget(buildQuery());
      setPreview(result);
      setStep("preview");
    } catch (error) {
      setPreviewError(error);
      setStep("preview");
    } finally {
      setIsPreviewing(false);
    }
  }

  async function saveWidget() {
    if (!dashboardDefinitionId) {
      setPreviewError(new Error("No dashboard definition is selected. Create or load a dashboard before saving widgets."));
      return;
    }

    setIsSaving(true);
    setPreviewError(null);

    try {
      const widgetTitle =
        builderState.widgetTitle.trim() ||
        `${selectedMeasure?.label ?? "Measure"} by ${
          metadata?.dimensions.find((x) => x.code === builderState.dimensionCode)?.label ?? "Dimension"
        }`;

      const saved = await plantProcessApi.createDashboardWidgetDefinition(
        dashboardDefinitionId,
        {
          widgetCode: generateWidgetCode(widgetTitle),
          widgetTitle,
          widgetType: builderState.widgetType,
          chartType: builderState.chartTypeCode || "bar",
          dimensionCode: builderState.dimensionCode || "site",
          measureCode: builderState.measureCode || "materialCount",
          parameterCode: builderState.parameterCode || null,
          filterJson: JSON.stringify(builderState.filters || {}),
          layoutJson: "{}",
          displayOptionsJson: JSON.stringify({
            purposeCode: builderState.purposeCode,
            createdFrom: "WidgetBuilderWizard",
          }),
          sortOrder: 1000,
          isSynthetic: true,
          sourceSystem: "PlantProcessIQ.ReactDashboard",
          sourceRecordId: null,
        }
      );

      onWidgetSaved?.(saved.id);
      onClose();
    } catch (error) {
      setPreviewError(error);
    } finally {
      setIsSaving(false);
    }
  }

  const currentStepIndex = stepOrder.indexOf(step);
  const canGoBack = currentStepIndex > 0;
  const canGoNext =
    (step === "purpose" && !!builderState.purposeCode) ||
    (step === "chartType" && !!builderState.chartTypeCode) ||
    (step === "data" && !!builderState.dimensionCode && !!builderState.measureCode) ||
    step === "filters";

  return (
    <div className="wizard-overlay" role="dialog" aria-modal="true">
      <div className="wizard-shell">
        <header className="wizard-header">
          <div>
            <span className="eyebrow">Dashboard Builder</span>
            <h2>Create Custom Widget</h2>
            <p>
              Guided builder using backend metadata, compatibility rules and safe widget query API.
            </p>
          </div>

          <button className="icon-button" onClick={onClose} type="button" aria-label="Close wizard">
            <X size={18} />
          </button>
        </header>

        <div className="wizard-steps">
          {stepOrder.map((item, index) => (
            <button
              key={item}
              className={`wizard-step ${step === item ? "wizard-step--active" : ""} ${
                index < currentStepIndex ? "wizard-step--done" : ""
              }`}
              onClick={() => setStep(item)}
              type="button"
            >
              {index < currentStepIndex ? <CheckCircle2 size={15} /> : <span>{index + 1}</span>}
              {item}
            </button>
          ))}
        </div>

        {isLoading ? (
          <div className="wizard-panel">Loading metadata...</div>
        ) : loadError ? (
          <div className="wizard-panel wizard-error">{String(loadError)}</div>
        ) : metadata ? (
          <div className="wizard-body">
            {step === "purpose" ? (
              <WizardSection
                title="1. Choose analysis purpose"
                subtitle="This controls the recommended chart, dimension and measure."
              >
                <div className="wizard-card-grid">
                  {metadata.purposes.map((purpose) => (
                    <button
                      key={purpose.code}
                      className={`wizard-choice ${
                        builderState.purposeCode === purpose.code ? "wizard-choice--selected" : ""
                      }`}
                      onClick={() => selectPurpose(purpose)}
                      type="button"
                    >
                      <Sparkles size={18} />
                      <strong>{purpose.label}</strong>
                      <span>{purpose.description}</span>
                    </button>
                  ))}
                </div>
              </WizardSection>
            ) : null}

            {step === "chartType" ? (
              <WizardSection
                title="2. Choose visualization"
                subtitle="Only chart types supported by the backend safety registry should be used."
              >
                <div className="wizard-card-grid">
                  {metadata.chartTypes.map((chart) => (
                    <button
                      key={chart.code}
                      className={`wizard-choice ${
                        builderState.chartTypeCode === chart.code ? "wizard-choice--selected" : ""
                      }`}
                      onClick={() => selectChart(chart)}
                      type="button"
                    >
                      <BarChart3 size={18} />
                      <strong>{chart.label}</strong>
                      <span>{chart.description}</span>
                    </button>
                  ))}
                </div>
              </WizardSection>
            ) : null}

            {step === "data" ? (
              <WizardSection
                title="3. Select dimension and measure"
                subtitle="This is where the wizard becomes real: the selected pair is sent to the backend query engine."
              >
                <div className="wizard-form-grid">
                  <label>
                    Widget title
                    <input
                      value={builderState.widgetTitle}
                      onChange={(event) => patchState({ widgetTitle: event.target.value })}
                      placeholder="e.g. Defect Rate by Equipment"
                    />
                  </label>

                  <label>
                    Dimension
                    <select
                      value={builderState.dimensionCode ?? ""}
                      onChange={(event) => patchState({ dimensionCode: event.target.value })}
                    >
                      <option value="">Select dimension...</option>
                      {compatibleDimensions.map((dimension: DashboardDimensionMetadata) => (
                        <option key={dimension.code} value={dimension.code}>
                          {dimension.label} ({dimension.category})
                        </option>
                      ))}
                    </select>
                  </label>

                  <label>
                    Measure
                    <select
                      value={builderState.measureCode ?? ""}
                      onChange={(event) => patchState({ measureCode: event.target.value })}
                    >
                      <option value="">Select measure...</option>
                      {compatibleMeasures.map((measure: DashboardMeasureMetadata) => (
                        <option key={measure.code} value={measure.code}>
                          {measure.label} ({measure.aggregation})
                        </option>
                      ))}
                    </select>
                  </label>

                  {selectedMeasure?.requiresParameterCode ? (
                    <label>
                      Parameter
                      <select
                        value={builderState.parameterCode ?? ""}
                        onChange={(event) =>
                          patchState({
                            parameterCode: event.target.value,
                            filters: { parameterCode: event.target.value },
                          })
                        }
                      >
                        <option value="">Select parameter...</option>
                        {(referenceData?.parameters ?? []).map((parameter) => (
                          <option key={parameter.code} value={parameter.code}>
                            {parameter.name} ({parameter.code})
                          </option>
                        ))}
                      </select>
                    </label>
                  ) : null}
                </div>
              </WizardSection>
            ) : null}

            {step === "filters" ? (
              <WizardSection
                title="4. Add filters"
                subtitle="Optional filters are stored with the widget and sent to the preview/save API."
              >
                <div className="wizard-form-grid">
                  <label>
                    Site
                    <select
                      value={builderState.filters.siteId ?? ""}
                      onChange={(event) =>
                        patchState({ filters: { siteId: event.target.value || null } })
                      }
                    >
                      <option value="">All sites</option>
                      {(referenceData?.sites ?? []).map((item) => (
                        <option key={item.id} value={item.id}>{item.name}</option>
                      ))}
                    </select>
                  </label>

                  <label>
                    Area
                    <select
                      value={builderState.filters.areaId ?? ""}
                      onChange={(event) =>
                        patchState({ filters: { areaId: event.target.value || null } })
                      }
                    >
                      <option value="">All areas</option>
                      {(referenceData?.areas ?? []).map((item) => (
                        <option key={item.id} value={item.id}>{item.name}</option>
                      ))}
                    </select>
                  </label>

                  <label>
                    Equipment
                    <select
                      value={builderState.filters.equipmentId ?? ""}
                      onChange={(event) =>
                        patchState({ filters: { equipmentId: event.target.value || null } })
                      }
                    >
                      <option value="">All equipment</option>
                      {(referenceData?.equipment ?? []).map((item) => (
                        <option key={item.id} value={item.id}>{item.name}</option>
                      ))}
                    </select>
                  </label>

                  <label>
                    Defect
                    <select
                      value={builderState.filters.defectType ?? ""}
                      onChange={(event) =>
                        patchState({ filters: { defectType: event.target.value || null } })
                      }
                    >
                      <option value="">All defects</option>
                      {(referenceData?.defects ?? []).map((item) => (
                        <option key={item.code} value={item.code}>{item.name}</option>
                      ))}
                    </select>
                  </label>

                  <label>
                    Risk class
                    <select
                      value={builderState.filters.riskClass ?? ""}
                      onChange={(event) =>
                        patchState({ filters: { riskClass: event.target.value || null } })
                      }
                    >
                      <option value="">All risk classes</option>
                      {(referenceData?.riskClasses ?? []).map((item) => (
                        <option key={item.code} value={item.code}>{item.name}</option>
                      ))}
                    </select>
                  </label>

                  <label>
                    From UTC
                    <input
                      type="datetime-local"
                      onChange={(event) =>
                        patchState({
                          filters: {
                            fromUtc: event.target.value
                              ? new Date(event.target.value).toISOString()
                              : null,
                          },
                        })
                      }
                    />
                  </label>

                  <label>
                    To UTC
                    <input
                      type="datetime-local"
                      onChange={(event) =>
                        patchState({
                          filters: {
                            toUtc: event.target.value
                              ? new Date(event.target.value).toISOString()
                              : null,
                          },
                        })
                      }
                    />
                  </label>
                </div>
              </WizardSection>
            ) : null}

            {step === "preview" ? (
              <WizardSection
                title="5. Preview and save"
                subtitle="The preview uses the backend widget query endpoint before saving."
              >
                <div className="wizard-preview-actions">
                  <button className="secondary-button" onClick={runPreview} disabled={isPreviewing} type="button">
                    <Eye size={16} />
                    {isPreviewing ? "Previewing..." : "Run Preview"}
                  </button>

                  <button
                    className="primary-button"
                    onClick={saveWidget}
                    disabled={isSaving || !preview || !dashboardDefinitionId}
                    type="button"
                  >
                    <Save size={16} />
                    {isSaving ? "Saving..." : "Save Widget"}
                  </button>
                </div>

                {previewError ? <div className="wizard-error">{String(previewError)}</div> : null}

                {preview ? (
                  <div className="wizard-preview-table">
                    <strong>
                      {preview.rows.length} rows · {preview.columns.length} columns
                    </strong>
                    {preview.warnings.length ? (
                      <ul>
                        {preview.warnings.map((warning) => (
                          <li key={warning}>{warning}</li>
                        ))}
                      </ul>
                    ) : null}

                    <div className="table-wrap">
                      <table>
                        <thead>
                          <tr>
                            {preview.columns.map((column) => (
                              <th key={column.code}>{column.label}</th>
                            ))}
                          </tr>
                        </thead>
                        <tbody>
                          {preview.rows.slice(0, 10).map((row, index) => (
                            <tr key={index}>
                              {preview.columns.map((column) => (
                                <td key={column.code}>{formatValue(row[column.code])}</td>
                              ))}
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                ) : (
                  <div className="empty-insight">
                    <strong>No preview yet</strong>
                    <p>Run preview to validate the widget against backend metadata and query rules.</p>
                  </div>
                )}
              </WizardSection>
            ) : null}
          </div>
        ) : null}

        <footer className="wizard-footer">
          <button
            className="secondary-button"
            disabled={!canGoBack}
            onClick={() => setStep(stepOrder[currentStepIndex - 1])}
            type="button"
          >
            <ArrowLeft size={16} />
            Back
          </button>

          {step !== "preview" ? (
            <button
              className="primary-button"
              disabled={!canGoNext}
              onClick={() =>
                step === "filters"
                  ? runPreview()
                  : setStep(stepOrder[currentStepIndex + 1])
              }
              type="button"
            >
              {step === "filters" ? "Preview" : "Next"}
              <ArrowRight size={16} />
            </button>
          ) : null}
        </footer>
      </div>
    </div>
  );
}

function WizardSection({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle: string;
  children: React.ReactNode;
}) {
  return (
    <section className="wizard-section">
      <div className="wizard-section__header">
        <h3>{title}</h3>
        <p>{subtitle}</p>
      </div>
      {children}
    </section>
  );
}

function formatValue(value: unknown) {
  if (value === null || value === undefined) return "-";
  if (typeof value === "number") {
    return new Intl.NumberFormat(undefined, { maximumFractionDigits: 3 }).format(value);
  }
  return String(value);
}