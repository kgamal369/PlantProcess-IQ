import { useEffect, useMemo, useState } from "react";

type Health = {
  status: string;
  component: string;
};

type TemplateRow = {
  templateCode: string;
  displayName: string;
  sourceArchetype: string;
  templateDefinition: string;
};

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5063";

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
  });

  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}`);
  }

  return response.json() as Promise<T>;
}

export function V5NoCodeMapperPage() {
  const [mapperHealth, setMapperHealth] = useState<Health | null>(null);
  const [provenanceHealth, setProvenanceHealth] = useState<Health | null>(null);
  const [templates, setTemplates] = useState<TemplateRow[]>([]);
  const [status, setStatus] = useState<string>("Loading V5 mapper proof...");
  const [sessionId, setSessionId] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    Promise.all([
      api<Health>("/api/v5/visual-mapper/health"),
      api<Health>("/api/v5/blended-provenance/health"),
      api<TemplateRow[]>("/api/v5/visual-mapper/templates"),
    ])
      .then(([mapper, provenance, templateRows]) => {
        if (!active) return;
        setMapperHealth(mapper);
        setProvenanceHealth(provenance);
        setTemplates(templateRows);
        setStatus("V5 no-code mapper and blended provenance endpoints are reachable.");
      })
      .catch((error) => {
        if (!active) return;
        setStatus(`V5 mapper proof could not load: ${error.message}`);
      });

    return () => {
      active = false;
    };
  }, []);

  async function runDemoDiscovery() {
    setStatus("Creating mapper session...");
    const session = await api<{ id: string }>("/api/v5/visual-mapper/sessions", {
      method: "POST",
      body: JSON.stringify({
        sourceCode: "demo-source-v5",
        displayName: "Demo source V5",
        sourceKind: "generic_relational",
        templateCode: "generic_relational",
      }),
    });

    setSessionId(session.id);
    setStatus("Running read-only demo discovery...");

    const discovery = await api<{ discoveredTables: number; discoveredColumns: number }>(
      `/api/v5/visual-mapper/sessions/${session.id}/discover-demo`,
      { method: "POST" },
    );

    setStatus(
      `Discovery complete: ${discovery.discoveredTables} tables / ${discovery.discoveredColumns} columns. Business-key and dry-run steps are ready.`,
    );
  }

  const templateSummary = useMemo(
    () => templates.map((x) => `${x.displayName} (${x.sourceArchetype})`).join(", "),
    [templates],
  );

  return (
    <main style={{ padding: "32px", color: "#eaf7ff", background: "var(--ppiq-color-bg-deep)", minHeight: "100vh" }}>
      <section
        style={{
          border: "1px solid rgba(0, 212, 255, 0.22)",
          borderRadius: 24,
          padding: 24,
          background: "rgba(11, 23, 48, 0.82)",
        }}
      >
        <p style={{ color: "var(--ppiq-color-accent-cyan)", textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12 }}>
          Doctrine v5 · Phase 5 / Phase 6
        </p>
        <h1>No-Code Visual Mapper + Blended Provenance</h1>
        <p>
          This page proves the buyer-facing workflow: discover source columns, build business keys,
          preview joins, suggest canonical targets, dry-run coverage, publish mapping versions, and
          inspect transition/blended genealogy weights.
        </p>

        <div style={{ display: "grid", gap: 12, marginTop: 20 }}>
          <div>Visual mapper health: {mapperHealth?.status ?? "pending"}</div>
          <div>Blended provenance health: {provenanceHealth?.status ?? "pending"}</div>
          <div>Templates: {templateSummary || "pending"}</div>
          <div>Active session: {sessionId ?? "none"}</div>
          <strong>{status}</strong>
        </div>

        <button
          type="button"
          onClick={runDemoDiscovery}
          style={{
            marginTop: 22,
            border: "1px solid rgba(0, 212, 255, 0.35)",
            borderRadius: 14,
            padding: "12px 18px",
            color: "#eaf7ff",
            background: "rgba(0, 132, 255, 0.24)",
            cursor: "pointer",
          }}
        >
          Run read-only demo discovery
        </button>
      </section>
    </main>
  );
}

export default V5NoCodeMapperPage;