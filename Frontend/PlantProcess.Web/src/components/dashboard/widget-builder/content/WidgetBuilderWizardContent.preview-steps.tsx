import { Eye, Sparkles } from "lucide-react";

import type { DashboardWidgetFilters, DashboardWidgetQueryOptions, DashboardWidgetQueryResult } from "../../../../api/productApiClient";

import { InteractiveBarChart, InteractiveLineChart, InteractivePieChart } from "@/components/charts/InteractiveCharts";
import type { ChartRow } from "@/components/charts/InteractiveCharts";
import { EmptyInsightState } from "@/components/dashboard/EmptyInsightState";
import { WidgetScriptStep } from "../WidgetScriptStep";
import { StandardButton } from "@/components/standard";

import type { WidgetBuilderState } from "./WidgetBuilderWizardContent.types";
import { selectFieldForDimension } from "./WidgetBuilderWizardContent.helpers";

import { StandardP2Table } from "@/components/standard/StandardP2Controls";
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

export function ScriptStep({
  state,
  filters,
  onPatch,
  onExpressionPreviewAccepted,
  onDisableExpression,
}: {
  state: WidgetBuilderState;
  filters: DashboardWidgetFilters;
  onPatch: (patch: Partial<WidgetBuilderState>) => void;
  onExpressionPreviewAccepted: (
    expression: string,
    preview: DashboardWidgetQueryResult
  ) => void;
  onDisableExpression: () => void;
}) {
  const widgetScriptOptions = {
    maxRows: state.maxRows,
    rawRowLimit: state.rawRowLimit,
    sortDirection: "desc",
    includeWarnings: true,
  } satisfies DashboardWidgetQueryOptions;

  return (
    <WizardSection
      icon={<Sparkles size={18} />}
      title="5. Safe transform / expression"
      description="Optional advanced layer for grouping, measure, parameter, row limits and sorting. It is a safe expression, not raw SQL."
    >
      <div className="wizard-script-status">
        <div>
          <strong>
            {state.expressionEnabled
              ? "Expression mode is enabled for this widget."
              : "Expression mode is optional for this widget."}
          </strong>
          <p>
            Use this step when the normal wizard fields are not enough. The
            backend validates allowed tokens and rejects SQL/script-like input.
          </p>
        </div>

        {state.expressionEnabled ? (
          <StandardButton
            type="button"
            className="secondary-button"
            onClick={onDisableExpression}
          >
            Use standard query only
          </StandardButton>
        ) : null}
      </div>

      <WidgetScriptStep
        initialExpression={
          state.queryExpression ||
          "widget=chart;\nchart=bar;\ndimension=defectType;\nmeasure=defectCount;\nmaxRows=20;\nsort=desc;"
        }
        filters={filters}
        options={widgetScriptOptions}
        onExpressionAccepted={(
          expression: string,
          result: DashboardWidgetQueryResult
        ) => {
          onPatch({
            queryExpression: expression,
            expressionEnabled: true,
          });

          onExpressionPreviewAccepted(expression, result);
        }}
      />
    </WizardSection>
  );
}

export function PreviewStep({
  preview,
  previewRows,
  categoryKey,
  valueKey,
  chartType,
  title,
  dimensionCode,
  expressionEnabled,
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
  expressionEnabled: boolean;
  isPreviewing: boolean;
  previewError: unknown;
  onPreview: () => void;
}) {
  return (
    <WizardSection
      icon={<Eye size={18} />}
      title="6. Preview"
      description={
        expressionEnabled
          ? "The preview uses the backend safe expression endpoint. No raw SQL is executed."
          : "The preview calls the backend widget query endpoint with the complete DTO."
      }
    >
      <div className="preview-toolbar">
        <StandardButton
          className="secondary-button"
          onClick={onPreview}
          isDisabled={isPreviewing}
          type="button"
        >
          <Eye size={16} />
          {isPreviewing ? "Previewing..." : "Run preview"}
        </StandardButton>

        {preview ? (
          <span className="muted-text">
            {preview.rows.length} row(s), generated {preview.generatedAtUtc}
          </span>
        ) : null}

        {expressionEnabled ? (
          <span className="muted-text">Safe expression mode</span>
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

export function PreviewTable({ rows }: { rows: ChartRow[] }) {
  if (!rows.length) return <EmptyInsightState />;

  const columns = Object.keys(rows[0] ?? {});

  return (
    <div className="table-shell">
      <StandardP2Table>
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
      </StandardP2Table>
    </div>
  );
}