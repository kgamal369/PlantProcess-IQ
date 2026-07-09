
import { useMemo, useState } from "react";
import { phase7ValueApi, type Phase7RealizationRequest } from "../../api/value";
import {
  formatMoney, normalizeImpact, normalizeRealization, workedCaseLocalProjection, type ScenarioImpactView, type ScenarioRealizationView, } from "./phase7ValueScenarioMath";
import "./phase7-value-scenario.css";

import { StandardP2Button } from "@/components/standard/StandardP2Controls";
import { StandardButton } from "@/components/standard";
const T040_MARKER = "PPIQ_REALIZATION_T040_VALUE_SCENARIO_PAGE";

const demoAssumptions = {
  Currency: "EUR",
  CostPerTon: null,
  DowngradeDeltaPerTon: { Low: 140, Mid: 210, High: 280 },
  ScrapCostPerTon: { Low: 300, Mid: 400, High: 500 },
  DowntimeCostPerMin: { Low: 50, Mid: 75, High: 100 },
  GradePremiumPerTon: { Low: 100, Mid: 150, High: 200 },
  EnergyPricePerMwh: null,
};

const impactRequest = {
  FindingRef: "finding:edge-crack-demo-28k-56k",
  CoilId: "DEMO-COIL-EDGE-CRACK-001",
  DefectCode: "EDGE_CRACK",
  DefectRateDelta: 0.02,
  MonthlyVolumeTons: 10_000,
  ProductionStopMinutes: 0,
  YieldLossTons: 0,
  UseScrapCost: false,
};

const realizationRequest: Phase7RealizationRequest = {
  TrackingCode: "T040-EDGE-CRACK-SCENARIO-REALIZATION",
  SourceRecommendationId: "rec-edge-crack-001",
  SourceValueImpactId: "11111111-1111-1111-1111-111111111111",
  BaselineWindow: {
    MetricCode: "edge_crack_count",
    StartUtc: "2026-05-01T00:00:00Z",
    EndUtc: "2026-05-31T00:00:00Z",
    Value: 100,
    Unit: "defects",
  },
  ActualWindow: {
    MetricCode: "edge_crack_count",
    StartUtc: "2026-06-01T00:00:00Z",
    EndUtc: "2026-06-30T00:00:00Z",
    Value: 80,
    Unit: "defects",
  },
  Direction: 1,
  ValuePerUnit: { Low: 100, Mid: 150, High: 200 },
  PotentialValue: { Low: 28_000, Mid: 42_000, High: 56_000 },
  InvestmentCost: 1_000,
  Currency: "EUR",
};

export function Phase7ValueScenarioPage() {
  const [impact, setImpact] = useState<ScenarioImpactView | null>(null);
  const [realization, setRealization] = useState<ScenarioRealizationView | null>(null);
  const [ledgerStatus, setLedgerStatus] = useState<string>("Not recorded");
  const [isRunning, setIsRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const localProjection = useMemo(() => workedCaseLocalProjection(), []);

  async function runScenario() {
    setIsRunning(true);
    setError(null);
    setLedgerStatus("Preparing assumptions...");

    try {
      await phase7ValueApi.putCostAssumptions(demoAssumptions);

      setLedgerStatus("Calculating projected value impact...");
      const impactResponse = await phase7ValueApi.calculateImpact(impactRequest);
      const normalizedImpact = normalizeImpact(impactResponse);
      setImpact(normalizedImpact);

      setLedgerStatus("Calculating tracked baseline-vs-actual value...");
      const realizationResponse = await phase7ValueApi.calculateRealization(realizationRequest);
      const normalizedRealization = normalizeRealization(realizationResponse);
      setRealization(normalizedRealization);

      setLedgerStatus("Ready. Use Record tracked value to persist the ledger row.");
    } catch (scenarioError) {
      const message =
        scenarioError instanceof Error
          ? scenarioError.message
          : "Could not run value scenario.";
      setError(message);
      setLedgerStatus("Scenario failed safely.");
    } finally {
      setIsRunning(false);
    }
  }

  async function recordTrackedValue() {
    setIsRunning(true);
    setError(null);
    setLedgerStatus("Recording tracked value...");

    try {
      const response = await phase7ValueApi.recordRealization(realizationRequest);
      const recorded = Boolean(response.recorded ?? response.Recorded);
      setLedgerStatus(recorded ? "Tracked value recorded in realization ledger." : "Backend abstained from recording; check returned reason.");
    } catch (recordError) {
      const message =
        recordError instanceof Error
          ? recordError.message
          : "Could not record tracked value.";
      setError(message);
      setLedgerStatus("Record failed safely.");
    } finally {
      setIsRunning(false);
    }
  }

  return (
    <main className="page-shell phase7-value-scenario" data-testid="phase7-value-scenario-page">
      <section className="dashboard-hero">
        <div>
          <div className="eyebrow">{T040_MARKER}</div>
          <h1>Value Scenario Workbench</h1>
          <p>
            Run the EUR 28k-56k edge-crack worked case against the real value APIs,
            then compare projected value with tracked baseline-vs-actual value.
          </p>
          <div className="dashboard-subtitle-row">
            <span>Scenario: EDGE_CRACK downgrade</span>
            <span className="status-chip">Not MES / not guaranteed savings</span>
          </div>
        </div>

        <div className="phase7-value-actions">
          <StandardButton className="phase7-value-button" type="button" onClick={runScenario} isDisabled={isRunning}>
            {isRunning ? "Running..." : "Run value scenario"}
          </StandardButton>
          <StandardButton className="phase7-value-button" type="button" onClick={recordTrackedValue} isDisabled={isRunning || !realization}>
            Record tracked value
          </StandardButton>
        </div>
      </section>

      <section className="phase7-value-card phase7-value-formula">
        <span>Formula</span>
        <strong>0.02 × 10,000 tons = 200 affected tons</strong>
        <p>
          200 affected tons × EUR 140 / 210 / 280 downgrade delta per ton =
          EUR 28k / 42k / 56k per month.
        </p>
      </section>

      <section className="phase7-value-grid">
        <RangeCard title="Local expected fixture" range={localProjection} currency="EUR" />
        <RangeCard title="Projected API impact" range={impact} currency={impact?.currency ?? "EUR"} />
        <RangeCard title="Tracked realized value" range={realization} currency={realization?.currency ?? "EUR"} />
      </section>

      <section className="phase7-value-grid">
        <div className="phase7-value-card">
          <span>Realization status</span>
          <strong className="phase7-value-status">{realization?.status ?? "Not calculated"}</strong>
          <p>Improvement units: {realization?.improvementUnits ?? 0}</p>
          <p>Capture rate: {realization?.captureRateMid == null ? "n/a" : (realization.captureRateMid * 100).toFixed(2) + "%"}</p>
          <p>ROI ratio: {realization?.roiMid == null ? "n/a" : realization.roiMid.toFixed(2) + "x"}</p>
        </div>

        <div className="phase7-value-card">
          <span>Ledger status</span>
          <strong>{ledgerStatus}</strong>
          <p>
            Recording is intentionally separate from projection. The page never claims automatic causal savings.
          </p>
        </div>
      </section>

      <section className="phase7-value-caveat">
        <strong>Honesty caveat</strong>
        <p>
          {realization?.caveat ??
            impact?.caveat ??
            "Projected value range only; not a guaranteed saving. Baseline-vs-actual tracked value is not automatic causal attribution."}
        </p>
      </section>

      {error ? (
        <section className="phase7-value-error">
          <strong>Controlled error</strong>
          <p>{error}</p>
        </section>
      ) : null}
    </main>
  );
}

function RangeCard({
  title,
  range,
  currency,
}: {
  title: string;
  range: { low: number; expected: number; high: number } | null;
  currency: string;
}) {
  return (
    <div className="phase7-value-card">
      <span>{title}</span>
      <strong>{range ? formatMoney(range.expected, currency) : "Not calculated"}</strong>
      <p>
        Low: {range ? formatMoney(range.low, currency) : "-"} · Expected:{" "}
        {range ? formatMoney(range.expected, currency) : "-"} · High:{" "}
        {range ? formatMoney(range.high, currency) : "-"}
      </p>
    </div>
  );
}
