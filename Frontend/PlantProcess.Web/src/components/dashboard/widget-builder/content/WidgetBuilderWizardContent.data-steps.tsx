import { BarChart3, CheckCircle2, Sparkles } from "lucide-react";

import type { DashboardChartTypeMetadata, DashboardDimensionMetadata, DashboardMeasureMetadata, DashboardMetadata, DashboardReferenceData } from "../../../../api/productApiClient";

import { StandardButton } from "@/components/standard";

import type { WidgetBuilderState } from "./WidgetBuilderWizardContent.types";

export function WizardSection({
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

export function PurposeStep({
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
          <StandardButton
            key={purpose.code}
            className={`wizard-choice-card ${
              selectedPurposeCode === purpose.code ? "selected" : ""
            }`}
            onClick={() => onSelect(purpose.code)}
            type="button"
          >
            <strong>{purpose.label}</strong>
            <span>{purpose.description}</span>
          </StandardButton>
        ))}
      </div>
    </WizardSection>
  );
}

export function ChartTypeStep({
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
          <StandardButton
            key={chartType.code}
            className={`wizard-choice-card ${
              selectedChartTypeCode === chartType.code ? "selected" : ""
            }`}
            onClick={() => onSelect(chartType.code)}
            type="button"
          >
            <strong>{chartType.label}</strong>
            <span>{chartType.description ?? chartType.category}</span>
          </StandardButton>
        ))}
      </div>
    </WizardSection>
  );
}

export function DataStep({
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
                {dimension.label} — {dimension.category}
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
                {measure.label} — {measure.aggregation}
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
                  {item.name} — {item.code}
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