
import { useMemo, useState } from "react";
import {
  buildWidgetHeatmapCells, buildWidgetQueryFromDefinition, filterSortHeatmapCells, heatmapSeriesSignature, normalizeDashboardWidgetDefinition, p3t15DemoBackendWidget, p3t15DemoHeatmapRows, schemaDriftSummary, validateWidgetDefinitionSchema, type HeatmapFilterSortState, } from "../../api/p3T15WidgetSchemaContract";
import "./p3t15-widget-schema-drift.css";

import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Input, StandardP2Select } from "@/components/standard/StandardP2Controls";
import { StandardButton } from "@/components/standard";
export const P3_T15_WIDGET_SCHEMA_DRIFT_ROOT_CAUSE_FIXED =
  "P3_T15_WIDGET_SCHEMA_DRIFT_ROOT_CAUSE_FIXED";

export function P3T15WidgetSchemaDriftPage() {
  const [created, setCreated] = useState(false);
  const [search, setSearch] = useState("");
  const [minValue, setMinValue] = useState(0);
  const [sortBy, setSortBy] = useState<HeatmapFilterSortState["sortBy"]>("value");
  const [direction, setDirection] = useState<HeatmapFilterSortState["direction"]>("desc");

  const validation = useMemo(
    () => validateWidgetDefinitionSchema(p3t15DemoBackendWidget),
    [],
  );

  const normalized = useMemo(
    () => normalizeDashboardWidgetDefinition(p3t15DemoBackendWidget),
    [],
  );

  const query = useMemo(
    () => buildWidgetQueryFromDefinition(normalized),
    [normalized],
  );

  const allCells = useMemo(
    () => buildWidgetHeatmapCells(p3t15DemoHeatmapRows, "equipment", "day", "defectRate"),
    [],
  );

  const cells = useMemo(
    () =>
      filterSortHeatmapCells(allCells, {
        search,
        minValue,
        sortBy,
        direction,
      }),
    [allCells, search, minValue, sortBy, direction],
  );

  const signature = useMemo(() => heatmapSeriesSignature(cells), [cells]);
  const summary = useMemo(() => schemaDriftSummary(p3t15DemoBackendWidget), []);

  return (
    <main
      className="p3-t15-page"
      data-testid="p3-t15-widget-schema-page"
      data-p3-task="P3-T15"
    >
      <section className="p3-t15-hero">
        <div className="p3-t15-kicker">P3-T15 · Widget schema-drift root-cause fix</div>
        <h1>Widget Contract + Heatmap Interaction Proof</h1>
        <p>
          This page proves that backend dashboard-widget definitions are normalized into one frontend
          contract before rendering. It also proves the heatmap widget can be filtered and sorted without
          reloading the dashboard shell.
        </p>

        <div className="p3-t15-actions">
          <StandardButton type="button" onClick={() => setCreated(true)}>
            Create heatmap widget from builder contract
          </StandardButton>
          <StandardButton type="button" onClick={() => setCreated(false)}>
            Reset widget preview
          </StandardButton>
        </div>
      </section>

      <section className="p3-t15-grid">
        <article className="p3-t15-card">
          <span>Contract status</span>
          <strong data-testid="p3-t15-contract-status">
            {validation.isValid ? "VALID" : "INVALID"}
          </strong>
          <p>{validation.isValid ? "No FE/BE schema drift detected." : validation.errors.join(", ")}</p>
        </article>

        <article className="p3-t15-card">
          <span>Chart type</span>
          <strong>{normalized.chartType}</strong>
          <p>Heatmap is normalized from backend PascalCase or camelCase fields.</p>
        </article>

        <article className="p3-t15-card">
          <span>Query sort</span>
          <strong>{query.options.sortDirection.toUpperCase()}</strong>
          <p>Sort direction is carried from displayOptionsJson into the widget query contract.</p>
        </article>
      </section>

      <section className="p3-t15-panel">
        <h2>Schema drift diagnostics</h2>
        <pre data-testid="p3-t15-schema-summary">{JSON.stringify(summary, null, 2)}</pre>
      </section>

      <section className="p3-t15-panel">
        <h2>Interactive heatmap widget</h2>
        {!created ? (
          <div className="p3-t15-empty" data-testid="p3-t15-empty">
            Widget definition is valid. Click “Create heatmap widget from builder contract” to render it.
          </div>
        ) : (
          <>
            <div className="p3-t15-controls">
              <label>
                Search
                <StandardP2Input
                  data-testid="p3-t15-filter-search"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Caster, Mill, Mon..."
                />
              </label>

              <label>
                Minimum value
                <StandardP2Input
                  data-testid="p3-t15-filter-min"
                  type="number"
                  min="0"
                  max="1"
                  step="0.01"
                  value={minValue}
                  onChange={(event) => setMinValue(Number(event.target.value))}
                />
              </label>

              <label>
                Sort by
                <StandardP2Select
                  data-testid="p3-t15-sort-by"
                  value={sortBy}
                  onChange={(event) => setSortBy(event.target.value as HeatmapFilterSortState["sortBy"])}
                >
                  <option value="value">Value</option>
                  <option value="x">Equipment</option>
                  <option value="y">Day</option>
                </StandardP2Select>
              </label>

              <label>
                Direction
                <StandardP2Select
                  data-testid="p3-t15-sort-direction"
                  value={direction}
                  onChange={(event) => setDirection(event.target.value as HeatmapFilterSortState["direction"])}
                >
                  <option value="desc">Desc</option>
                  <option value="asc">Asc</option>
                </StandardP2Select>
              </label>
            </div>

            <div
              className="p3-t15-heatmap"
              data-testid="p3-t15-heatmap"
              data-series-signature={signature}
            >
              {cells.map((cell) => (
                <div
                  key={cell.id}
                  className="p3-t15-heatmap-cell"
                  data-testid="p3-t15-heatmap-cell"
                  title={cell.label + " = " + cell.value.toFixed(2)}
                >
                  <span>{cell.x}</span>
                  <em>{cell.y}</em>
                  <strong>{Math.round(cell.value * 100)}%</strong>
                </div>
              ))}
            </div>

            <p className="p3-t15-note">
              Visible cells: <strong data-testid="p3-t15-cell-count">{cells.length}</strong>.
              Series signature changes when filter or sort changes, without page reload.
            </p>
          </>
        )}
      </section>
    </main>
  );
}

export default P3T15WidgetSchemaDriftPage;
