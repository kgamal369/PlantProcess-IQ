import { useCallback, useEffect, useMemo, useState } from "react";

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

type SystemLogEntry = {
  lineNumber: number;
  timestamp: string;
  severity: string;
  message: string;
};

type LogSource = "job" | "system";

const JOB_TYPES = ["", "Import-Stage1", "Import-Stage2", "Import-FullCycle", "ConnectorTest"];
const SEVERITIES = ["", "Info", "Warning", "Error"];
const SYSTEM_SEVERITIES = ["", "Debug", "Info", "Warning", "Error"];

/** Keystrokes must not become queries. */
function useDebounced<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = window.setTimeout(() => setDebounced(value), delayMs);
    return () => window.clearTimeout(t);
  }, [value, delayMs]);
  return debounced;
}

export function LogPanel() {
  const [open, setOpen] = useState<boolean>(
    new URLSearchParams(window.location.search).get("logs") === "open"
  );
  const [source, setSource] = useState<LogSource>("job");
  const [jobType, setJobType] = useState("");
  const [severity, setSeverity] = useState(
    new URLSearchParams(window.location.search).get("severity") ?? ""
  );
  const [day, setDay] = useState("");
  const [hour, setHour] = useState("");
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebounced(search, 350);

  const [jobEntries, setJobEntries] = useState<JobLogEntry[]>([]);
  const [systemEntries, setSystemEntries] = useState<SystemLogEntry[]>([]);
  const [note, setNote] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    const q = new URLSearchParams();
    if (severity) q.set("severity", severity);
    if (day) q.set("day", day);
    if (debouncedSearch.trim()) q.set("q", debouncedSearch.trim());

    if (source === "job") {
      if (jobType) q.set("jobType", jobType);
      apiClient
        .get<{ entries: JobLogEntry[] }>("/admin/job-logs?" + q.toString())
        .then((r) => {
          setJobEntries(r?.entries ?? []);
          setNote(null);
          setError(null);
        })
        .catch((e: unknown) => setError(e instanceof Error ? e.message : "load failed"));
      return;
    }

    if (hour) q.set("hour", hour);
    apiClient
      .get<{ exists: boolean; entries: SystemLogEntry[]; message?: string }>("/admin/system-logs?" + q.toString())
      .then((r) => {
        setSystemEntries(r?.entries ?? []);
        setNote(r && r.exists === false ? (r.message ?? "No system log for that hour.") : null);
        setError(null);
      })
      .catch((e: unknown) => setError(e instanceof Error ? e.message : "load failed"));
  }, [source, jobType, severity, day, hour, debouncedSearch]);

  useEffect(() => {
    if (!open) return;
    load();
    const t = window.setInterval(load, 10000);
    return () => window.clearInterval(t);
  }, [open, load]);

  const severityOptions = useMemo(() => (source === "job" ? SEVERITIES : SYSTEM_SEVERITIES), [source]);

  return (
    <div className={open ? "piq-log-panel piq-log-panel--open" : "piq-log-panel"}>
      <StandardButton
        type="button"
        className="piq-log-panel__toggle"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
      >
        {source === "job" ? "Job Log" : "System Log"} {open ? "\u25BC" : "\u25B2"}
      </StandardButton>

      {open ? (
        <div className="piq-log-panel__body">
          <div className="piq-log-panel__filters">
            <div className="piq-log-panel__source" role="group" aria-label="Log source">
              <StandardButton
                type="button"
                className={source === "job" ? "piq-log-panel__source--active" : ""}
                onClick={() => setSource("job")}
              >
                Job events
              </StandardButton>
              <StandardButton
                type="button"
                className={source === "system" ? "piq-log-panel__source--active" : ""}
                onClick={() => setSource("system")}
              >
                System log
              </StandardButton>
            </div>

            <input
              type="search"
              className="piq-log-panel__search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Filter by word..."
              aria-label="Filter log entries by word"
            />

            {source === "job" ? (
              <select value={jobType} onChange={(e) => setJobType(e.target.value)} aria-label="Job type">
                {JOB_TYPES.map((t) => (
                  <option key={t} value={t}>
                    {t === "" ? "All job types" : t}
                  </option>
                ))}
              </select>
            ) : null}

            <select value={severity} onChange={(e) => setSeverity(e.target.value)} aria-label="Severity">
              {severityOptions.map((s) => (
                <option key={s} value={s}>
                  {s === "" ? "All severities" : s}
                </option>
              ))}
            </select>

            <input type="date" value={day} onChange={(e) => setDay(e.target.value)} aria-label="Day" />

            {source === "system" ? (
              <input
                type="number"
                min={0}
                max={23}
                className="piq-log-panel__hour"
                value={hour}
                onChange={(e) => setHour(e.target.value)}
                placeholder="Hour"
                aria-label="Hour of day (UTC)"
              />
            ) : null}

            <StandardButton type="button" onClick={load}>Refresh</StandardButton>
          </div>

          {error ? <div className="piq-log-panel__error">{error}</div> : null}
          {note ? <div className="piq-log-panel__empty">{note}</div> : null}

          <div className="piq-log-panel__list" role="log">
            {source === "job" ? (
              jobEntries.length === 0 ? (
                <div className="piq-log-panel__empty">No job events for this filter.</div>
              ) : (
                jobEntries.map((e) => (
                  <div key={e.id} className={"piq-log-line piq-log-row--" + e.severity.toLowerCase()}>
                    <span>{e.occurredAtUtc.replace("T", " ").slice(0, 19)}</span>
                    <span>{e.severity.padEnd(7)}</span>
                    <span>{e.jobName}</span>
                    <span className="piq-log-line__msg">{e.message}</span>
                  </div>
                ))
              )
            ) : systemEntries.length === 0 && !note ? (
              <div className="piq-log-panel__empty">No system log lines for this filter.</div>
            ) : (
              systemEntries.map((e) => (
                <div key={e.lineNumber} className={"piq-log-line piq-log-row--" + e.severity.toLowerCase()}>
                  <span>{e.timestamp.slice(0, 19)}</span>
                  <span>{e.severity.padEnd(7)}</span>
                  <span className="piq-log-line__msg">{e.message}</span>
                </div>
              ))
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}