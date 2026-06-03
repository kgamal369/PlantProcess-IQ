// P4-05 On-demand inspection job + generated analysis page.
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { StandardCard, DataFetchBoundary, StandardButton, ppiqTokens } from "../../components/standard";
import { phase2WorkflowApi, type InspectionJobRow } from "../../api/phase2WorkflowApi";

const c = ppiqTokens.color;

export function InspectionJobsPage() {
  const navigate = useNavigate();
  const [jobs, setJobs] = useState<InspectionJobRow[]>([]);
  const [isLoading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const [reloadKey, setReloadKey] = useState(0);
  const [saving, setSaving] = useState(false);

  const [name, setName] = useState("Edge-crack inspection");
  const [defectType, setDefectType] = useState("edge_crack");
  const [parameterCode, setParameterCode] = useState("");
  const [windowDays, setWindowDays] = useState(30);
  const [schedule, setSchedule] = useState("0 6 * * *");

  useEffect(() => {
    let live = true; setLoading(true); setError(null);
    phase2WorkflowApi.getInspectionJobs()
      .then((r) => { if (live) setJobs(r.rows ?? []); })
      .catch((e) => { if (live) setError(e); })
      .finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [reloadKey]);

  const outcomeKey = `defect.${defectType}_rate`;

  async function save(runNow: boolean) {
    setSaving(true);
    try {
      await phase2WorkflowApi.saveInspectionJobFromCorrelation({
        inspectionJobName: name, inspectionType: "correlation", defectType,
        parameterCode: parameterCode || null, windowDays, scheduleExpression: schedule, runNow,
      });
      setReloadKey((k) => k + 1);
      navigate(`/investigate/advanced?outcomeKey=${encodeURIComponent(outcomeKey)}&windowDays=${windowDays}`);
    } finally { setSaving(false); }
  }

  const field = { background: c.surface2, color: c.text, border: `1px solid ${c.borderSubtle}`, borderRadius: ppiqTokens.radius.sm, padding: "8px 10px", width: "100%" } as const;
  const label = { display: "block", color: c.textMuted, fontSize: 12, marginBottom: 4 } as const;

  return (
    <div style={{ display: "grid", gap: ppiqTokens.spacing.lg, padding: ppiqTokens.spacing.lg }}>
      <StandardCard eyebrow="ML / Correlation workflow" title="New inspection job" subtitle="Pick a defect + period, name the job, then run or schedule. Running generates the \u00a77.4 analysis page." elevation="raised">
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: ppiqTokens.spacing.md }}>
          <div><label style={label}>Job name</label><input style={field} value={name} onChange={(e) => setName(e.target.value)} /></div>
          <div><label style={label}>Defect type</label><input style={field} value={defectType} onChange={(e) => setDefectType(e.target.value)} /></div>
          <div><label style={label}>Parameter code (optional)</label><input style={field} value={parameterCode} onChange={(e) => setParameterCode(e.target.value)} placeholder="e.g. casting_speed" /></div>
          <div><label style={label}>Window (days)</label><input style={field} type="number" value={windowDays} onChange={(e) => setWindowDays(Number(e.target.value))} /></div>
          <div><label style={label}>Schedule (cron)</label><input style={field} value={schedule} onChange={(e) => setSchedule(e.target.value)} /></div>
        </div>
        <div style={{ display: "flex", gap: 10, marginTop: ppiqTokens.spacing.md, alignItems: "center" }}>
          <StandardButton variant="primary" onClick={() => void save(true)}>Run now</StandardButton>
          <StandardButton variant="ghost" onClick={() => void save(false)}>Save &amp; schedule</StandardButton>
          <span style={{ color: c.textSoft, fontSize: 12 }}>{saving ? "Saving\u2026" : `outcome: ${outcomeKey}`}</span>
        </div>
      </StandardCard>

      <StandardCard eyebrow="Jobs" title="Inspection jobs" subtitle="Saved jobs appear in the Jobs Monitor. Run, enable/disable, or open the analysis." elevation="flat">
        <DataFetchBoundary title="Inspection jobs" isLoading={isLoading} error={error} isEmpty={jobs.length === 0} onRetry={() => setReloadKey((k) => k + 1)} emptyTitle="No inspection jobs yet" emptyMessage="Create one above to generate a saved analysis.">
          <div style={{ display: "grid", gap: 8 }}>
            {jobs.map((j) => (
              <div key={j.id} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", border: `1px solid ${c.borderSubtle}`, borderRadius: ppiqTokens.radius.md, padding: ppiqTokens.spacing.md, background: c.surface1 }}>
                <div>
                  <strong style={{ color: c.text }}>{j.inspectionJobName}</strong>
                  <div style={{ color: c.textMuted, fontSize: 12, fontFamily: "monospace" }}>{j.inspectionType} \u00b7 {j.defectType ?? j.parameterCode ?? "\u2014"} \u00b7 {j.scheduleExpression} \u00b7 {j.honestState}{j.lastRunStatus ? ` \u00b7 last: ${j.lastRunStatus}` : ""}</div>
                </div>
                <div style={{ display: "flex", gap: 8 }}>
                  <StandardButton variant="ghost" onClick={() => void phase2WorkflowApi.runJobNow(j.id).then(() => setReloadKey((k) => k + 1))}>Run</StandardButton>
                  <StandardButton variant="ghost" onClick={() => void (j.isEnabled ? phase2WorkflowApi.disableJob(j.id) : phase2WorkflowApi.enableJob(j.id)).then(() => setReloadKey((k) => k + 1))}>{j.isEnabled ? "Disable" : "Enable"}</StandardButton>
                  <StandardButton variant="ghost" onClick={() => navigate(`/investigate/advanced?outcomeKey=${encodeURIComponent(`defect.${j.defectType ?? "edge_crack"}_rate`)}`)}>Open analysis</StandardButton>
                </div>
              </div>
            ))}
          </div>
        </DataFetchBoundary>
      </StandardCard>
    </div>
  );
}
