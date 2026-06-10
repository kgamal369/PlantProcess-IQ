import { useEffect, useMemo, useState } from "react";
import { phase15AdvisoryApi, type P15BenchmarkDashboardResponse, type P15BenchmarkingContract, type P15BenchmarkingHealth, type P15BenchmarkResponse } from "@/api/phase15Advisory";

import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Table } from "@/components/standard/StandardP2Controls";
import { StandardButton } from "@/components/standard";
const cardStyle = { border: "1px solid rgba(124,220,255,0.18)", borderRadius: 22, padding: 20, background: "linear-gradient(135deg, rgba(7,18,34,0.95), rgba(4,10,22,0.9))", boxShadow: "0 18px 50px rgba(0,0,0,0.22)" } as const;
const buttonStyle = { border: "1px solid rgba(0,212,255,0.34)", borderRadius: 14, padding: "10px 14px", color: "#eaf7ff", background: "rgba(0,132,255,0.24)", cursor: "pointer", fontWeight: 700 } as const;

function asNumber(value?: number | null) {
  if (value === undefined || value === null || Number.isNaN(value)) return "—";
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value);
}

export function BenchmarkingPage() {
  const [health, setHealth] = useState<P15BenchmarkingHealth | null>(null);
  const [contract, setContract] = useState<P15BenchmarkingContract | null>(null);
  const [dashboard, setDashboard] = useState<P15BenchmarkDashboardResponse | null>(null);
  const [suppressed, setSuppressed] = useState<P15BenchmarkResponse | null>(null);
  const [status, setStatus] = useState("Loading Phase 15 benchmarking...");
  const [busy, setBusy] = useState(false);

  async function refresh() {
    setBusy(true);
    try {
      const [nextHealth, nextContract, nextDashboard] = await Promise.all([
        phase15AdvisoryApi.benchmarkingHealth(),
        phase15AdvisoryApi.benchmarkingContract(),
        phase15AdvisoryApi.benchmarkingSummary(),
      ]);
      setHealth(nextHealth);
      setContract(nextContract);
      setDashboard(nextDashboard);
      setStatus("Benchmarking dashboard is ready. Only anonymized aggregate bands are shown.");
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setStatus(`Benchmarking dashboard not reachable: ${message}`);
    } finally {
      setBusy(false);
    }
  }

  useEffect(() => { void refresh(); }, []);

  async function loadSuppressedDemo() {
    setBusy(true);
    try {
      const result = await phase15AdvisoryApi.benchmarkingSuppressedDemo();
      setSuppressed(result);
      setStatus("Suppressed benchmark demo loaded. Below-minimum cohort does not expose aggregate bands.");
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setStatus(`Suppressed demo failed: ${message}`);
    } finally {
      setBusy(false);
    }
  }

  const firstBand = dashboard?.benchmarks.find((item) => item.band)?.band;
  const firstMetric = dashboard?.metricCards[0];

  const summaryCards = useMemo(() => [
    { label: "Mode", value: health?.mode ?? "pending" },
    { label: "Industry", value: dashboard?.industryCode ?? "pending" },
    { label: "Median", value: asNumber(firstBand?.p50) },
    { label: "Cohort", value: String(firstBand?.cohortSize ?? "—") },
    { label: "Plant percentile", value: asNumber(firstMetric?.percentileEstimate) },
    { label: "Suppression", value: suppressed ? String(suppressed.visibility) : "not tested" },
  ], [dashboard?.industryCode, firstBand, firstMetric, health?.mode, suppressed]);

  return (
    <main data-testid="phase15-benchmarking-page">
      <section>
        <p>
          Pack G · T-100 · Cross-plant & industry benchmarking
        </p>
        <h1>Phase 15 Benchmarking</h1>
        <p>
          Privacy-preserving cross-plant and industry benchmarking. The dashboard shows only anonymized aggregate bands and suppresses results below the minimum cohort threshold.
        </p>
        <strong>{status}</strong>
      </section>

      <section>
        {summaryCards.map((card) => (
          <div key={card.label}>
            <span>{card.label}</span>
            <strong>{card.value}</strong>
          </div>
        ))}
      </section>

      <section>
        <div>
          <h2>1. Aggregate benchmark bands</h2>
          {dashboard?.benchmarks.length ? (
            <StandardP2Table>
              <thead><tr><th>Metric</th><th>Visibility</th><th>P25</th><th>P50</th><th>P75</th><th>Message</th></tr></thead>
              <tbody>
                {dashboard.benchmarks.map((benchmark) => (
                  <tr key={`${benchmark.metricCode}-${benchmark.visibility}`}>
                    <td>{benchmark.metricCode}</td>
                    <td>{String(benchmark.visibility)}</td>
                    <td>{asNumber(benchmark.band?.p25)}</td>
                    <td>{asNumber(benchmark.band?.p50)}</td>
                    <td>{asNumber(benchmark.band?.p75)}</td>
                    <td>{benchmark.message}</td>
                  </tr>
                ))}
              </tbody>
            </StandardP2Table>
          ) : <p>Benchmark data is loading...</p>}
          <div>
            <StandardButton type="button" isDisabled={busy} onClick={() => void refresh()}>Refresh benchmark</StandardButton>
            <StandardButton type="button" isDisabled={busy} onClick={() => void loadSuppressedDemo()}>Demo suppressed benchmark</StandardButton>
          </div>
        </div>

        <div>
          <h2>2. Best-practice references</h2>
          <div>
            {(dashboard?.bestPractices ?? []).map((practice) => (
              <article key={practice.practiceId}>
                <strong>{practice.title}</strong>
                <p>{practice.description}</p>
                <p>{practice.safetyCaveat}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section>
        <h2>3. Privacy guards</h2>
        <ul>
          {(dashboard?.privacyGuards ?? contract?.guardrails ?? [
            "No identifiable cross-tenant row exposure.",
            "Only anonymized aggregate bands are returned.",
            "Minimum cohort size is enforced.",
          ]).map((rule) => <li key={rule}>{rule}</li>)}
        </ul>
      </section>
    </main>
  );
}
