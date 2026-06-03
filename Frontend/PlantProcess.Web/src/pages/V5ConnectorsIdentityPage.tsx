import { useEffect, useState } from "react";

type Health = {
  status: string;
  component: string;
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

export function V5ConnectorsIdentityPage() {
  const [connectorHealth, setConnectorHealth] = useState<Health | null>(null);
  const [identityHealth, setIdentityHealth] = useState<Health | null>(null);
  const [status, setStatus] = useState("Loading P07/P08 proof...");
  const [truth, setTruth] = useState<unknown[]>([]);

  useEffect(() => {
    let active = true;

    Promise.all([
      api<Health>("/api/v5/plant-connectors/health"),
      api<Health>("/api/v5/enterprise-identity/health"),
      api<unknown[]>("/api/v5/plant-connectors/truth"),
    ])
      .then(([connector, identity, truthRows]) => {
        if (!active) return;
        setConnectorHealth(connector);
        setIdentityHealth(identity);
        setTruth(truthRows);
        setStatus("P07/P08 endpoints are reachable.");
      })
      .catch((error) => {
        if (!active) return;
        setStatus(`P07/P08 proof could not load: ${error.message}`);
      });

    return () => {
      active = false;
    };
  }, []);

  async function registerMockConnector() {
    setStatus("Registering read-only mock historian...");

    await api("/api/v5/plant-connectors/mock-historian/register", {
      method: "POST",
      body: JSON.stringify({
        connectorCode: "mock-historian-v5",
        displayName: "Mock Historian V5",
        tagPaths: [
          "MILL.HSM.F1.ROLL_FORCE",
          "MILL.HSM.F2.TEMP_EXIT",
          "QUALITY.SURFACE.EDGE_CRACK_SCORE",
        ],
      }),
    });

    const backfill = await api<{ rowsWritten: number }>("/api/v5/plant-connectors/backfill", {
      method: "POST",
      body: JSON.stringify({
        connectorCode: "mock-historian-v5",
        tagPaths: [
          "MILL.HSM.F1.ROLL_FORCE",
          "MILL.HSM.F2.TEMP_EXIT",
        ],
        fromUtc: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
        toUtc: new Date().toISOString(),
        maxPointsPerTag: 10,
      }),
    });

    const truthRows = await api<unknown[]>("/api/v5/plant-connectors/truth");
    setTruth(truthRows);
    setStatus(`Mock historian registered; backfill wrote ${backfill.rowsWritten} points with checkpoints.`);
  }

  async function enrollMfa() {
    setStatus("Creating TOTP enrollment...");
    const result = await api<{ otpauthUri: string; recoveryCodes: string[] }>("/api/v5/enterprise-identity/mfa/enroll", {
      method: "POST",
      body: JSON.stringify({
        accountName: "admin@plantprocessiq.local",
      }),
    });

    setStatus(`TOTP enrollment created. Recovery codes generated: ${result.recoveryCodes.length}. URI prefix: ${result.otpauthUri.slice(0, 24)}...`);
  }

  return (
    <main style={{ padding: 32, color: "#eaf7ff", background: "#050b18", minHeight: "100vh" }}>
      <section
        style={{
          border: "1px solid rgba(0, 212, 255, 0.22)",
          borderRadius: 24,
          padding: 24,
          background: "rgba(11, 23, 48, 0.82)",
        }}
      >
        <p style={{ color: "#00d4ff", textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12 }}>
          Doctrine v5 · Phase 7 / Phase 8
        </p>
        <h1>Plant Connector Breadth + Enterprise Identity</h1>
        <p>
          This page proves the next buyer-risk layer: read-only historian connector truth,
          backfill/checkpointing, TOTP MFA, recovery codes, session controls, and account protection.
        </p>

        <div style={{ display: "grid", gap: 12, marginTop: 20 }}>
          <div>Connector health: {connectorHealth?.status ?? "pending"}</div>
          <div>Identity health: {identityHealth?.status ?? "pending"}</div>
          <div>Connector truth rows: {truth.length}</div>
          <strong>{status}</strong>
        </div>

        <div style={{ display: "flex", gap: 12, flexWrap: "wrap", marginTop: 22 }}>
          <button
            type="button"
            onClick={registerMockConnector}
            style={{
              border: "1px solid rgba(0, 212, 255, 0.35)",
              borderRadius: 14,
              padding: "12px 18px",
              color: "#eaf7ff",
              background: "rgba(0, 132, 255, 0.24)",
              cursor: "pointer",
            }}
          >
            Register mock historian + backfill
          </button>

          <button
            type="button"
            onClick={enrollMfa}
            style={{
              border: "1px solid rgba(44, 230, 162, 0.35)",
              borderRadius: 14,
              padding: "12px 18px",
              color: "#eaf7ff",
              background: "rgba(44, 230, 162, 0.18)",
              cursor: "pointer",
            }}
          >
            Enroll TOTP MFA
          </button>
        </div>
      </section>
    </main>
  );
}

export default V5ConnectorsIdentityPage;