import { useEffect, useMemo, useState } from "react";
import { phase15AdvisoryApi, type P15BenchmarkDashboardResponse, type P15BenchmarkingContract, type P15BenchmarkingHealth, type P15BenchmarkResponse } from "@/api/phase15Advisory";

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
    <main style={{ display: "grid", gap: 18, color: "#eaf7ff" }} data-testid="phase15-benchmarking-page">
      <section style={{ ...cardStyle, borderColor: "rgba(19,216,255,0.28)" }}>
        <p style={{ color: "#13d8ff", textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12, margin: 0 }}>
          Pack G · T-100 · Cross-plant & industry benchmarking
        </p>
        <h1 style={{ margin: "8px 0 8px", fontSize: 34 }}>Phase 15 Benchmarking</h1>
        <p style={{ margin: 0, color: "#9ab8d7", maxWidth: 980, lineHeight: 1.65 }}>
          Privacy-preserving cross-plant and industry benchmarking. The dashboard shows only anonymized aggregate bands and suppresses results below the minimum cohort threshold.
        </p>
        <strong style={{ display: "block", marginTop: 16, color: status.includes("failed") ? "#ffb86b" : "#64ffda" }}>{status}</strong>
      </section>

      <section style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(170px, 1fr))", gap: 12 }}>
        {summaryCards.map((card) => (
          <div key={card.label} style={cardStyle}>
            <span style={{ color: "#7e9bb8", fontSize: 12, textTransform: "uppercase" }}>{card.label}</span>
            <strong style={{ display: "block", marginTop: 6, fontSize: 18 }}>{card.value}</strong>
          </div>
        ))}
      </section>

      <section style={{ display: "grid", gridTemplateColumns: "minmax(360px, 1fr) minmax(340px, 0.9fr)", gap: 14 }}>
        <div style={cardStyle}>
          <h2 style={{ marginTop: 0 }}>1. Aggregate benchmark bands</h2>
          {dashboard?.benchmarks.length ? (
            <table style={{ width: "100%", borderCollapse: "collapse" }}>
              <thead><tr style={{ color: "#9ab8d7", textAlign: "left" }}><th style={{ padding: 8 }}>Metric</th><th style={{ padding: 8 }}>Visibility</th><th style={{ padding: 8 }}>P25</th><th style={{ padding: 8 }}>P50</th><th style={{ padding: 8 }}>P75</th><th style={{ padding: 8 }}>Message</th></tr></thead>
              <tbody>
                {dashboard.benchmarks.map((benchmark) => (
                  <tr key={`${benchmark.metricCode}-${benchmark.visibility}`} style={{ borderTop: "1px solid rgba(120,190,255,0.12)" }}>
                    <td style={{ padding: 8 }}>{benchmark.metricCode}</td>
                    <td style={{ padding: 8 }}>{String(benchmark.visibility)}</td>
                    <td style={{ padding: 8 }}>{asNumber(benchmark.band?.p25)}</td>
                    <td style={{ padding: 8 }}>{asNumber(benchmark.band?.p50)}</td>
                    <td style={{ padding: 8 }}>{asNumber(benchmark.band?.p75)}</td>
                    <td style={{ padding: 8, color: "#9ab8d7" }}>{benchmark.message}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : <p style={{ color: "#9ab8d7" }}>Benchmark data is loading...</p>}
          <div style={{ display: "flex", gap: 10, marginTop: 12, flexWrap: "wrap" }}>
            <button type="button" disabled={busy} onClick={() => void refresh()} style={buttonStyle}>Refresh benchmark</button>
            <button type="button" disabled={busy} onClick={() => void loadSuppressedDemo()} style={buttonStyle}>Demo suppressed benchmark</button>
          </div>
        </div>

        <div style={cardStyle}>
          <h2 style={{ marginTop: 0 }}>2. Best-practice references</h2>
          <div style={{ display: "grid", gap: 12 }}>
            {(dashboard?.bestPractices ?? []).map((practice) => (
              <article key={practice.practiceId} style={{ border: "1px solid rgba(100,255,218,0.18)", borderRadius: 14, padding: 12 }}>
                <strong>{practice.title}</strong>
                <p style={{ color: "#9ab8d7", lineHeight: 1.6 }}>{practice.description}</p>
                <p style={{ color: "#ffdd8a", lineHeight: 1.6 }}>{practice.safetyCaveat}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section style={cardStyle}>
        <h2 style={{ marginTop: 0 }}>3. Privacy guards</h2>
        <ul style={{ color: "#9ab8d7", lineHeight: 1.75, paddingLeft: 18 }}>
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
