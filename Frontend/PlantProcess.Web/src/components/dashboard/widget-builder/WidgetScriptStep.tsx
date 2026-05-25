import { useMemo, useState } from "react";
import {
  widgetScriptApi,
  widgetScriptTemplates,
  type WidgetScriptValidationRequest,
} from "@/api/widgetScript";
import type {
  DashboardWidgetFilters,
  DashboardWidgetQueryOptions,
  DashboardWidgetQueryResult,
} from "@/api/plantProcessApi";

type WidgetScriptStepProps = {
  initialExpression?: string;
  filters?: DashboardWidgetFilters | null;
  options?: DashboardWidgetQueryOptions | null;
  onExpressionAccepted?: (
    expression: string,
    preview: DashboardWidgetQueryResult
  ) => void;
};

const defaultWidgetScriptOptions = {
  maxRows: 20,
  rawRowLimit: 10000,
  sortDirection: "desc",
  includeWarnings: true,
} satisfies DashboardWidgetQueryOptions;

export function WidgetScriptStep({
  initialExpression,
  filters,
  options,
  onExpressionAccepted,
}: WidgetScriptStepProps) {
  const [expression, setExpression] = useState(
    initialExpression ??
      "widget=chart;\nchart=bar;\ndimension=defectType;\nmeasure=defectCount;\nmaxRows=20;\nsort=desc;"
  );

  const [isPreviewing, setIsPreviewing] = useState(false);
  const [preview, setPreview] = useState<DashboardWidgetQueryResult | null>(
    null
  );
  const [error, setError] = useState<string | null>(null);

  const request = useMemo<WidgetScriptValidationRequest>(
    () => ({
      expression,
      filters: filters ?? null,
      options: options ?? defaultWidgetScriptOptions,
    }),
    [expression, filters, options]
  );

  async function runPreview() {
    setIsPreviewing(true);
    setError(null);

    try {
      const result = await widgetScriptApi.executeExpression(request);
      setPreview(result);
      onExpressionAccepted?.(expression, result);
    } catch (err) {
      setPreview(null);
      setError(
        err instanceof Error
          ? err.message
          : "Widget expression preview failed."
      );
    } finally {
      setIsPreviewing(false);
    }
  }

  return (
    <section className="widget-script-step" aria-label="Widget script step">
      <div className="widget-script-step__header">
        <div>
          <p className="eyebrow">Advanced transform</p>
          <h3>Safe Widget Expression</h3>
          <p>
            Configure grouping, measure, parameter, filters, row limits, and
            sorting through a safe expression. This is not raw SQL.
          </p>
        </div>

        <button
          type="button"
          className="primary-button"
          disabled={isPreviewing || expression.trim().length === 0}
          onClick={() => void runPreview()}
        >
          {isPreviewing ? "Running preview..." : "Run expression preview"}
        </button>
      </div>

      <div className="widget-script-step__templates">
        {widgetScriptTemplates.map((template) => (
          <button
            key={template.title}
            type="button"
            className="secondary-button"
            onClick={() => {
              setExpression(template.expression);
              setPreview(null);
              setError(null);
            }}
            title={template.description}
          >
            {template.title}
          </button>
        ))}
      </div>

      <textarea
        className="widget-script-step__editor"
        value={expression}
        rows={8}
        spellCheck={false}
        onChange={(event) => {
          setExpression(event.target.value);
          setPreview(null);
          setError(null);
        }}
      />

      <div className="widget-script-step__help">
        <strong>Allowed tokens:</strong>{" "}
        widget, chart, dimension, measure, parameter, materialCode,
        sourceSystem, defectType, riskClass, shiftCode, fromUtc, toUtc,
        maxRows, rawRowLimit, sort.
      </div>

      {error ? (
        <div className="admin-alert danger">
          <strong>Expression rejected:</strong> {error}
        </div>
      ) : null}

      {preview ? (
        <div className="admin-preview-panel">
          <h4>Preview result</h4>
          <p>
            Rows: <strong>{preview.rows?.length ?? 0}</strong> · Columns:{" "}
            <strong>{preview.columns?.length ?? 0}</strong>
          </p>

          {preview.warnings?.length ? (
            <ul>
              {preview.warnings.map((warning) => (
                <li key={warning}>{warning}</li>
              ))}
            </ul>
          ) : null}

          <pre style={{ overflow: "auto", maxHeight: 260 }}>
            {JSON.stringify(
              {
                widget: preview.widget,
                columns: preview.columns,
                sampleRows: preview.rows?.slice(0, 5) ?? [],
              },
              null,
              2
            )}
          </pre>
        </div>
      ) : null}
    </section>
  );
}

export default WidgetScriptStep;