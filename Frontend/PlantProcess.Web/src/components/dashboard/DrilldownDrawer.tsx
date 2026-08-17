import { X } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { useDashboardSelections } from "../../state/DashboardSelectionContext";
import { StandardButton } from "@/components/standard";
import { populationForRow } from "@/state/drilldownRowIdentity";
import { describePopulationCount, resolveExecutionEvidence, type EvidenceLookup } from "@/state/drilldownEvidence";
import { executeWithEvidence, type WidgetExecutionSnapshot } from "@/state/drilldownExecutionSnapshot";
import type { DashboardWidgetRowPopulation } from "@/api/product-core/dashboard-widget-types";
import { productApi } from "../../api/productApiClient";
import { dashboardingApi } from "../../api/dashboarding/dashboarding.api";
import { assistantApi } from "@/api/assistantApi";

export function DrilldownDrawer() {
  const { drilldown, closeDrilldown } = useDashboardSelections();

  if (!drilldown.isOpen) {
    return null;
  }

  const snapshot = drilldown.executionSnapshot ?? null;
  const population = populationForRow(
    snapshot?.rowPopulations ?? null,
    drilldown.sourceRowIndex ?? null,
  );

  return (
    <aside className="drilldown-drawer">
      <div className="drilldown-drawer__header">
        <div>
          <span>{drilldown.type}</span>
          <h3>{drilldown.title}</h3>
          {drilldown.subtitle ? <p>{drilldown.subtitle}</p> : null}
        </div>

        <StandardButton className="icon-button" onClick={closeDrilldown} type="button">
          <X size={18} />
        </StandardButton>
      </div>

      <div className="drilldown-drawer__body">
        <PopulationSection population={population} />
        <EvidenceSection snapshot={snapshot} />

        <section className="drilldown-section" data-testid="drilldown-point">
          <h4>The point you clicked</h4>
          <PrettyObject value={drilldown.payload} />
        </section>
      </div>
    </aside>
  );
}

/** WHAT this point represents. Straight from the backend descriptor of the
 *  render that drew it - never recomputed here, never inferred from a position. */
function PopulationSection({ population }: { population: DashboardWidgetRowPopulation | null }) {
  if (population === null) {
    return (
      <section className="drilldown-section" data-testid="drilldown-population" data-population="unavailable">
        <h4>Population</h4>
        <p>
          This point carries no population descriptor, so the rows behind it cannot be
          named. Nothing is inferred from its position on the chart.
        </p>
      </section>
    );
  }

  const bindings = Object.entries(population.dimensionBindings ?? {});

  return (
    <section className="drilldown-section" data-testid="drilldown-population" data-population="described">
      <h4>Population</h4>

      <div className="detail-list">
        <div className="detail-row">
          <span>Rows behind this point</span>
          {/* Null is a real answer. It is never replaced by the row count, the
              series length or the number of visible points. */}
          <strong data-testid="population-count">{describePopulationCount(population.populationCount)}</strong>
        </div>
        <div className="detail-row">
          <span>Measure</span>
          <strong>{population.measureCode}</strong>
        </div>
        {population.parameterCode ? (
          <div className="detail-row"><span>Parameter</span><strong>{population.parameterCode}</strong></div>
        ) : null}
        {bindings.map(([key, value]) => (
          <div key={key} className="detail-row">
            <span>{formatKey(key)}</span>
            <strong>{value ?? "-"}</strong>
          </div>
        ))}
      </div>
    </section>
  );
}

/** WHICH EXECUTION produced the values. This is execution evidence, not
 *  physical source-row lineage, and it is never described as such. */
function EvidenceSection({ snapshot }: { snapshot: WidgetExecutionSnapshot | null }) {
  const [lookup, setLookup] = useState<EvidenceLookup<unknown> | null>(null);
  const requested = useRef<WidgetExecutionSnapshot | null>(null);

  const run = useCallback(async (current: WidgetExecutionSnapshot) => {
    const result = await executeWithEvidence(
      current,
      (query) => productApi.queryDashboardWidget(query as never),
      (query) => dashboardingApi.executeWidgetQueryExpression(query as never) as never,
    );

    setLookup(await resolveExecutionEvidence(
      result.executionEvidenceHandle ?? null,
      result.warnings,
      (id) => assistantApi.getWidgetResultEvidence(id) as Promise<unknown | null>,
    ));
  }, []);

  useEffect(() => {
    if (snapshot === null) return;
    // Exactly one execution per opened point. Effect churn must not turn a
    // drill-down into repeated writes against the evidence store.
    if (requested.current === snapshot) return;
    requested.current = snapshot;
    setLookup(null);

    void run(snapshot).catch((error: unknown) => {
      setLookup({ status: "error", message: error instanceof Error ? error.message : String(error) });
    });
  }, [snapshot, run]);

  if (snapshot === null) {
    return (
      <section className="drilldown-section" data-testid="drilldown-evidence" data-evidence="unavailable">
        <h4>Execution evidence</h4>
        <p>This point was not opened from a widget execution, so there is no evidence to fetch.</p>
      </section>
    );
  }

  if (lookup === null) {
    return (
      <section className="drilldown-section" data-testid="drilldown-evidence" data-evidence="loading">
        <h4>Execution evidence</h4>
        <p>Asking the execution that produced this point.</p>
      </section>
    );
  }

  return (
    <section className="drilldown-section" data-testid="drilldown-evidence" data-evidence={lookup.status}>
      <h4>Execution evidence</h4>
      <p className="drilldown-section__note">
        Evidence of the widget execution that produced these values. It is not source-row lineage.
      </p>

      {lookup.status === "resolved" ? <PrettyObject value={lookup.evidence} /> : null}

      {lookup.status === "unavailable" ? (
        <p data-testid="evidence-unavailable">{lookup.reason}</p>
      ) : null}

      {lookup.status === "notFound" ? (
        <p data-testid="evidence-not-found">
          The execution offered a handle, but the evidence behind it cannot be resolved
          now - it may not be available to this tenant, or no longer retained.
        </p>
      ) : null}

      {lookup.status === "error" ? (
        <p data-testid="evidence-error">
          The evidence request itself failed, which is not the same as there being no
          evidence: {lookup.message}
        </p>
      ) : null}
    </section>
  );
}

function PrettyObject({ value }: { value: unknown }) {
  if (value === null || value === undefined) {
    return <p>No drilldown data available.</p>;
  }

  if (typeof value !== "object") {
    return <p>{String(value)}</p>;
  }

  return (
    <div className="detail-list">
      {Object.entries(value as Record<string, unknown>).map(([key, item]) => (
        <div key={key} className="detail-row">
          <span>{formatKey(key)}</span>
          <strong>{formatValue(item)}</strong>
        </div>
      ))}
    </div>
  );
}

function formatKey(key: string) {
  return key
    .replace(/([A-Z])/g, " $1")
    .replace(/^./, (value) => value.toUpperCase());
}

function formatValue(value: unknown) {
  if (value === null || value === undefined) return "-";
  if (typeof value === "object") return JSON.stringify(value);
  return String(value);
}
