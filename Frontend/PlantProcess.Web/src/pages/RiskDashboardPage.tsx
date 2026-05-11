import { useEffect, useState } from "react";
import { ShieldAlert, ShieldCheck } from "lucide-react";
import { plantProcessApi } from "../api/plantProcessApi";
import { MetricCard } from "../components/MetricCard";

export function RiskDashboardPage() {
  const [risk, setRisk] = useState<any>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    plantProcessApi
      .getRiskDashboard()
      .then(setRisk)
      .catch((err) => setError(err.message));
  }, []);

  return (
    <section className="page">
      <div className="page-title">
        <div>
          <h1>Risk Dashboard</h1>
          <p>Calculated quality-risk overview and high-risk material signals.</p>
        </div>
      </div>

      {error && <div className="error-box">{error}</div>}

      <div className="metric-grid">
        <MetricCard title="Total Risk Scores" value={risk?.totalRiskScores ?? "-"} icon={<ShieldCheck size={20} />} />
        <MetricCard title="High Risk" value={risk?.highRiskCount ?? "-"} icon={<ShieldAlert size={20} />} />
        <MetricCard title="Medium Risk" value={risk?.mediumRiskCount ?? "-"} />
        <MetricCard title="Low Risk" value={risk?.lowRiskCount ?? "-"} />
      </div>

      <div className="panel">
        <h3>Risk API Response</h3>
        <pre>{JSON.stringify(risk, null, 2)}</pre>
      </div>
    </section>
  );
}