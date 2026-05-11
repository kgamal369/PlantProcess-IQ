import { useEffect, useState } from "react";
import { Activity, AlertTriangle, Database, Factory, Gauge, ShieldCheck } from "lucide-react";
import { plantProcessApi } from "../api/plantProcessApi";
import { MetricCard } from "../components/MetricCard";
import { StatusBadge } from "../components/StatusBadge";

export function DashboardPage() {
  const [health, setHealth] = useState<any>(null);
  const [dbHealth, setDbHealth] = useState<any>(null);
  const [summary, setSummary] = useState<any>(null);
  const [validation, setValidation] = useState<any>(null);
  const [error, setError] = useState<string>("");

  useEffect(() => {
    async function load() {
      try {
        const [healthResult, dbResult, summaryResult, validationResult] =
          await Promise.all([
            plantProcessApi.getHealth(),
            plantProcessApi.getDbHealth(),
            plantProcessApi.getDatabaseSummary(),
            plantProcessApi.getValidationReport(),
          ]);

        setHealth(healthResult);
        setDbHealth(dbResult);
        setSummary(summaryResult);
        setValidation(validationResult);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load dashboard.");
      }
    }

    load();
  }, []);

  return (
    <section className="page">
      <div className="page-title">
        <div>
          <h1>Executive Dashboard</h1>
          <p>Current platform health, data volume and validation readiness.</p>
        </div>
        <StatusBadge status={health?.status || "Loading"} />
      </div>

      {error && <div className="error-box">{error}</div>}

      <div className="metric-grid">
        <MetricCard title="Sites" value={summary?.sites ?? "-"} icon={<Factory size={20} />} />
        <MetricCard title="Materials" value={summary?.materialUnits ?? "-"} icon={<Database size={20} />} />
        <MetricCard title="Process Steps" value={summary?.processStepExecutions ?? "-"} icon={<Activity size={20} />} />
        <MetricCard title="Quality Events" value={summary?.qualityEvents ?? "-"} icon={<AlertTriangle size={20} />} />
        <MetricCard title="Risk Scores" value={summary?.riskScores ?? "-"} icon={<ShieldCheck size={20} />} />
        <MetricCard title="DB Connected" value={dbHealth?.canConnect ? "Yes" : "No"} icon={<Gauge size={20} />} />
      </div>

      <div className="panel">
        <h3>Validation Summary</h3>
        <div className="panel-row">
          <span>Status</span>
          <StatusBadge status={validation?.status || "Loading"} />
        </div>
        <pre>{JSON.stringify(validation?.issueCounts ?? {}, null, 2)}</pre>
      </div>
    </section>
  );
}