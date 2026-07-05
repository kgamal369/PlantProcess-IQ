import { useCallback, useEffect, useState } from "react";

import { apiClient } from "../../api/http";

import { StandardButton } from "@/components/standard";

import "./log-panel.css";

type JobLogEntry = {
  id: string;
  occurredAtUtc: string;
  jobType: string;
  jobName: string;
  severity: string;
  message: string;
};

const JOB_TYPES = ["", "Import-Stage1", "Import-Stage2", "Import-FullCycle", "ConnectorTest"];
const SEVERITIES = ["", "Info", "Warning", "Error"];

export function LogPanel() {
  const [open, setOpen] = useState<boolean>(
    new URLSearchParams(window.location.search).get("logs") === "open"
  );
  const [jobType, setJobType] = useState("");
  const [severity, setSeverity] = useState(
    new URLSearchParams(window.location.search).get("severity") ?? ""
  );
  const [day, setDay] = useState("");
  const [entries, setEntries] = useState<JobLogEntry[]>([]);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    const q = new URLSearchParams();
    if (jobType) q.set("jobType", jobType);
    if (severity) q.set("severity", severity);
    if (day) q.set("day", day);
    apiClient
      .get<{ entries: JobLogEntry[] }>("/admin/job-logs?" + q.toString())
      .then((r) => {
        setEntries(r?.entries ?? []);
        setError(null);
      })
      .catch((e: unknown) => setError(e instanceof Error ? e.message : "load failed"));
  }, [jobType, severity, day]);

  useEffect(() => {
    if (!open) return;
    load();
    const t = window.setInterval(load, 10000);
    return () => window.clearInterval(t);
  }, [open, load]);

  return (
    <div className={open ? "piq-log-panel piq-log-panel--open" : "piq-log-panel"}>
      <StandardButton
        type="button"
        className="piq-log-panel__toggle"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
      >
        Job Log {open ? "\u25BC" : "\u25B2"}
      </StandardButton>

      {open ? (
        <div className="piq-log-panel__body">
          <div className="piq-log-panel__filters">
            <select value={jobType} onChange={(e) => setJobType(e.target.value)} aria-label="Job type">
              {JOB_TYPES.map((t) => (
                <option key={t} value={t}>
                  {t === "" ? "All job types" : t}
                </option>
              ))}
            </select>
            <select value={severity} onChange={(e) => setSeverity(e.target.value)} aria-label="Severity">
              {SEVERITIES.map((s) => (
                <option key={s} value={s}>
                  {s === "" ? "All severities" : s}
                </option>
              ))}
            </select>
            <input type="date" value={day} onChange={(e) => setDay(e.target.value)} aria-label="Day" />
            <StandardButton type="button" onClick={load}>Refresh</StandardButton>
          </div>

          {error ? <div className="piq-log-panel__error">{error}</div> : null}

          <div className="piq-log-panel__list" role="log">
            {entries.length === 0 ? (
              <div className="piq-log-panel__empty">No job events for this filter.</div>
            ) : (
              entries.map((e) => (
                <div key={e.id} className={"piq-log-line piq-log-row--" + e.severity.toLowerCase()}>
                  <span>{e.occurredAtUtc.replace("T", " ").slice(0, 19)}</span>
                  <span>{e.severity.padEnd(7)}</span>
                  <span>{e.jobName}</span>
                  <span className="piq-log-line__msg">{e.message}</span>
                </div>
              ))
            )}
          </div>       </div>
      ) : null}
    </div>
  );
}
