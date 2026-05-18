import { useEffect, useState } from "react";
import { AlertTriangle, CheckCircle2 } from "lucide-react";
import { plantProcessApi } from "@/api/plantProcessApi";
import { MetricCard } from "@/components/MetricCard";
import { StatusBadge } from "@/components/StatusBadge";

export function DataQualityPage() {
  const [dataQuality, setDataQuality] = useState<any>(null);
  const [validation, setValidation] = useState<any>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    Promise.all([
      plantProcessApi.getDataQualityDashboard(),
      plantProcessApi.getValidationReport(),
    ])
      .then(([dq, validationResult]) => {
        setDataQuality(dq);
        setValidation(validationResult);
      })
      .catch((err) => setError(err.message));
  }, []);

  return (
    <section className="page">
      <div className="page-title">
        <div>
          <h1>Data Quality</h1>
          <p>Validation status, detected issues and readiness indicators.</p>
        </div>
        <StatusBadge status={validation?.status || "Loading"} />
      </div>

      {error && <div className="error-box">{error}</div>}

      <div className="metric-grid">
        <MetricCard title="Total DQ Issues" value={dataQuality?.totalIssues ?? "-"} icon={<AlertTriangle size={20} />} />
        <MetricCard title="Errors" value={dataQuality?.errorCount ?? "-"} />
        <MetricCard title="Warnings" value={dataQuality?.warningCount ?? "-"} />
        <MetricCard title="Validation" value={validation?.status ?? "-"} icon={<CheckCircle2 size={20} />} />
      </div>

      <div className="panel">
        <h3>Validation Findings</h3>
        <pre>{JSON.stringify(validation, null, 2)}</pre>
      </div>
    </section>
  );
}
