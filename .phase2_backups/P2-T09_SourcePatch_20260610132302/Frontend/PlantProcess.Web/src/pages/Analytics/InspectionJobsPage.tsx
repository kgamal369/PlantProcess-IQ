// P4-05 On-demand inspection job + generated analysis page.
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { StandardCard, DataFetchBoundary, StandardButton, ppiqTokens } from "../../components/standard";
import { inspectionWorkflowApi, type InspectionJobRow } from "../../api/inspectionWorkflowApi";

import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Input } from "@/components/standard/StandardP2Controls";
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
    inspectionWorkflowApi.getInspectionJobs()
      .then((r) => { if (live) setJobs(r.rows ?? []); })
      .catch((e) => { if (live) setError(e); })
      .finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [reloadKey]);

  const outcomeKey = `defect.${defectType}_rate`;

  async function save(runNow: boolean) {
    setSaving(true);
    try {
      await inspectionWorkflowApi.saveInspectionJobFromCorrelation({
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
    <div>
      <StandardCard eyebrow="ML / Correlation workflow" title="New inspection job" subtitle="Pick a defect + period, name the job, then run or schedule. Running generates the \u00a77.4 analysis page." elevation="raised">
        <div>
          <div><label>Job name</label><StandardP2Input value={name} onChange={(e) => setName(e.target.value)} /></div>
          <div><label>Defect type</label><StandardP2Input value={defectType} onChange={(e) => setDefectType(e.target.value)} /></div>
          <div><label>Parameter code (optional)</label><StandardP2Input value={parameterCode} onChange={(e) => setParameterCode(e.target.value)} placeholder="e.g. casting_speed" /></div>
          <div><label>Window (days)</label><StandardP2Input type="number" value={windowDays} onChange={(e) => setWindowDays(Number(e.target.value))} /></div>
          <div><label>Schedule (cron)</label><StandardP2Input value={schedule} onChange={(e) => setSchedule(e.target.value)} /></div>
        </div>
        <div>
          <StandardButton variant="primary" onClick={() => void save(true)}>Run now</StandardButton>
          <StandardButton variant="ghost" onClick={() => void save(false)}>Save &amp; schedule</StandardButton>
          <span>{saving ? "Saving\u2026" : `outcome: ${outcomeKey}`}</span>
        </div>
      </StandardCard>

      <StandardCard eyebrow="Jobs" title="Inspection jobs" subtitle="Saved jobs appear in the Jobs Monitor. Run, enable/disable, or open the analysis." elevation="flat">
        <DataFetchBoundary title="Inspection jobs" isLoading={isLoading} error={error} isEmpty={jobs.length === 0} onRetry={() => setReloadKey((k) => k + 1)} emptyTitle="No inspection jobs yet" emptyMessage="Create one above to generate a saved analysis.">
          <div>
            {jobs.map((j) => (
              <div key={j.id}>
                <div>
                  <strong>{j.inspectionJobName}</strong>
                  <div>{j.inspectionType} \u00b7 {j.defectType ?? j.parameterCode ?? "\u2014"} \u00b7 {j.scheduleExpression} \u00b7 {j.honestState}{j.lastRunStatus ? ` \u00b7 last: ${j.lastRunStatus}` : ""}</div>
                </div>
                <div>
                  <StandardButton variant="ghost" onClick={() => void inspectionWorkflowApi.runJobNow(j.id).then(() => setReloadKey((k) => k + 1))}>Run</StandardButton>
                  <StandardButton variant="ghost" onClick={() => void (j.isEnabled ? inspectionWorkflowApi.disableJob(j.id) : inspectionWorkflowApi.enableJob(j.id)).then(() => setReloadKey((k) => k + 1))}>{j.isEnabled ? "Disable" : "Enable"}</StandardButton>
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
