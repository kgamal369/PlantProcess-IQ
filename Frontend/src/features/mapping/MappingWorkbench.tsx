import React, { useCallback, useEffect, useState } from "react";
import { Badge, Button, Card, EmptyState, Spinner, Tabs, tokens } from "../_ui/standard";

type TypedError = { code: string; field: string; message: string };
type ProfileColumn = { name: string; inferredType: string; nullFraction: number; sample: string[] };
type ProfileResult = { sourceTable: string; rowCount: number; columns: ProfileColumn[] };

const TABS = [
  { id: "sources", label: "Source Catalog" },
  { id: "keys", label: "Business-Key Dictionary" },
  { id: "mapper", label: "Canonical Entity Mapper" },
  { id: "sql", label: "SQL View Builder" },
  { id: "genealogy", label: "Genealogy Builder" },
  { id: "publish", label: "Validation & Publish" },
];

async function api<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, { headers: { "Content-Type": "application/json" }, ...init });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return (await res.json()) as T;
}

export default function MappingWorkbench({ sourceTable = "staging.qa_inspection" }: { sourceTable?: string }) {
  const [active, setActive] = useState("mapper");
  const [dirty, setDirty] = useState(false);
  const [profile, setProfile] = useState<ProfileResult | null>(null);
  const [errors, setErrors] = useState<TypedError[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [validating, setValidating] = useState(false);
  const [pendingTab, setPendingTab] = useState<string | null>(null);

  // unsaved-change guard for full-page unload
  useEffect(() => {
    const h = (e: BeforeUnloadEvent) => { if (dirty) { e.preventDefault(); e.returnValue = ""; } };
    window.addEventListener("beforeunload", h);
    return () => window.removeEventListener("beforeunload", h);
  }, [dirty]);

  const loadProfile = useCallback(async () => {
    setLoading(true);
    try { setProfile(await api<ProfileResult>(`/api/mapping/profile?sourceTable=${encodeURIComponent(sourceTable)}&sample=50`)); }
    catch { setProfile(null); }
    finally { setLoading(false); }
  }, [sourceTable]);

  useEffect(() => { void loadProfile(); }, [loadProfile]);

  const validate = useCallback(async () => {
    setValidating(true);
    try {
      const r = await api<{ errors: TypedError[] }>(`/api/mapping/validate`, {
        method: "POST",
        body: JSON.stringify({
          canonicalEntityName: "quality_event",
          sourceTable,
          fields: [
            { canonicalField: "event_id", sourceColumn: "insp_id" },
            { canonicalField: "coil_id", sourceColumn: "coil_no" },
            { canonicalField: "defect_code", sourceColumn: "defect" },
            { canonicalField: "detected_at", sourceColumn: "ts" },
          ],
        }),
      });
      setErrors(r.errors);
    } catch (e) {
      setErrors([{ code: "RequestFailed", field: "-", message: String(e) }]);
    } finally {
      setValidating(false);
    }
  }, [sourceTable]);

  const requestTab = (id: string) => { if (dirty && id !== active) setPendingTab(id); else setActive(id); };
  const resolveLeave = (keep: boolean) => {
    if (keep) { setPendingTab(null); return; }
    setDirty(false);
    if (pendingTab) setActive(pendingTab);
    setPendingTab(null);
  };

  return (
    <div style={{ display: "grid", gridTemplateColumns: "1fr 360px", gap: 16, padding: 16, background: tokens.bg, minHeight: "100%" }}>
      <div style={{ gridColumn: "1 / span 2" }}>
        <Tabs tabs={TABS.map((t) => ({ ...t, dirty: dirty && t.id === active }))} active={active} onSelect={requestTab} />
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
        <Card title={`Editor - ${TABS.find((t) => t.id === active)?.label}`}
              right={<Badge tone={dirty ? "warn" : "ok"}>{dirty ? "Unsaved changes" : "Saved"}</Badge>}>
          {/* INTEGRATE: render the real config form for the active tab here. */}
          <p style={{ color: tokens.sub, font: "13px Inter" }}>
            Standard config surface for <b style={{ color: tokens.ink }}>{active}</b>. Edits mark the workbench dirty.
          </p>
          <Button kind="ghost" onClick={() => setDirty(true)}>Simulate an edit</Button>
        </Card>

        <Card title="Preview" right={<Button kind="ghost" onClick={() => void loadProfile()}>Refresh</Button>}>
          {loading ? <Spinner label="Profiling source..." /> :
            !profile ? <EmptyState title="No preview" hint="Could not profile this source." /> : (
              <table style={{ width: "100%", borderCollapse: "collapse", font: "12px Inter", color: tokens.ink }}>
                <thead>
                  <tr style={{ color: tokens.sub, textAlign: "left" }}>
                    <th style={{ padding: 6 }}>Column</th><th>Type</th><th>Null%</th><th>Sample</th>
                  </tr>
                </thead>
                <tbody>
                  {profile.columns.map((c) => (
                    <tr key={c.name} style={{ borderTop: `1px solid ${tokens.line}` }}>
                      <td style={{ padding: 6 }}>{c.name}</td>
                      <td>{c.inferredType}</td>
                      <td>{(c.nullFraction * 100).toFixed(0)}%</td>
                      <td style={{ color: tokens.sub }}>{c.sample.slice(0, 3).join(", ")}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
        </Card>
      </div>

      <div>
        <Card title="Validation" right={<Button onClick={() => void validate()}>Validate</Button>}>
          {validating ? <Spinner label="Validating mapping..." /> :
            errors === null ? <EmptyState title="Not validated yet" hint="Run validation to see typed errors." /> :
            errors.length === 0 ? <EmptyState title="All checks passed" hint="Required fields, types and units are valid." /> : (
              <ul style={{ listStyle: "none", margin: 0, padding: 0, display: "flex", flexDirection: "column", gap: 8 }}>
                {errors.map((e, i) => (
                  <li key={i} style={{ border: `1px solid ${tokens.bad}`, borderRadius: 8, padding: 10 }}>
                    <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                      <Badge tone="bad">{e.code}</Badge>
                      <b style={{ color: tokens.ink, font: "600 12px Inter" }}>{e.field}</b>
                    </div>
                    <div style={{ color: tokens.sub, font: "12px Inter", marginTop: 4 }}>{e.message}</div>
                  </li>
                ))}
              </ul>
            )}
        </Card>
      </div>

      {pendingTab && (
        <div role="dialog" aria-modal="true"
             style={{ position: "fixed", inset: 0, background: "rgba(2,8,20,.6)", display: "grid", placeItems: "center", zIndex: 50 }}>
          <div style={{ width: 380 }}>
            <Card title="Unsaved changes">
              <p style={{ color: tokens.sub, font: "13px Inter" }}>You have unsaved edits on this tab. Leave without saving?</p>
              <div style={{ display: "flex", gap: 8, justifyContent: "flex-end", marginTop: 8 }}>
                <Button kind="ghost" onClick={() => resolveLeave(true)}>Keep editing</Button>
                <Button kind="danger" onClick={() => resolveLeave(false)}>Discard</Button>
              </div>
            </Card>
          </div>
        </div>
      )}
    </div>
  );
}