import { useEffect, useMemo, useState } from "react";

import { P2T08_STANDARD_ROLLOUT_MARKER } from "@/components/standard/StandardP2Controls";
import { StandardButton } from "@/components/standard";
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
        setStatus(`V5 mapper proof unavailable: ${error.message}`);
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
    <main>
      <section
      >
        <p>
          Doctrine v5 · Phase 5 / Phase 6
        </p>
        <h1>No-Code Visual Mapper + Blended Provenance</h1>
        <p>
          This page proves the buyer-facing workflow: discover source columns, build business keys,
          preview joins, suggest canonical targets, dry-run coverage, publish mapping versions, and
          inspect transition/blended genealogy weights.
        </p>

        <div>
          <div>Visual mapper health: {mapperHealth?.status ?? "pending"}</div>
          <div>Blended provenance health: {provenanceHealth?.status ?? "pending"}</div>
          <div>Templates: {templateSummary || "pending"}</div>
          <div>Active session: {sessionId ?? "none"}</div>
          <strong>{status}</strong>
        </div>

        <StandardButton
          type="button"
          onClick={runDemoDiscovery}
        >
          Run read-only demo discovery
        </StandardButton>
      </section>
    </main>
  );
}

export default V5NoCodeMapperPage;