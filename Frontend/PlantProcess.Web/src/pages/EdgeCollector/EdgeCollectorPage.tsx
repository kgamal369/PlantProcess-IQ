import { useEffect, useMemo, useState } from "react";
import {
  edgeCollectorApi,
  type EdgeCollectorContract,
  type EdgeCollectorHealth,
  type EdgeCollectorProfile,
  type EdgeCollectorState,
} from "@/api/edgeCollector";

const cardStyle = {
  border: "1px solid rgba(0, 212, 255, 0.18)",
  borderRadius: 22,
  padding: 20,
  background: "linear-gradient(135deg, rgba(8, 20, 38, 0.94), rgba(5, 11, 24, 0.88))",
  boxShadow: "0 18px 50px rgba(0, 0, 0, 0.22)",
} as const;

const buttonStyle = {
  border: "1px solid rgba(0, 212, 255, 0.34)",
  borderRadius: 14,
  padding: "10px 14px",
  color: "#eaf7ff",
  background: "rgba(0, 132, 255, 0.24)",
  cursor: "pointer",
  fontWeight: 700,
} as const;

const inputStyle = {
  width: "100%",
  border: "1px solid rgba(120, 190, 255, 0.22)",
  borderRadius: 12,
  padding: "10px 12px",
  background: "rgba(2, 7, 18, 0.75)",
  color: "#eaf7ff",
} as const;

function nowIso() {
  return new Date().toISOString();
}

function shortTime(value?: string | null) {
  if (!value) return "—";
  return new Date(value).toLocaleString();
}

function safetyLabel(value: boolean) {
  return value ? "YES" : "NO";
}

export function EdgeCollectorPage() {
  const [health, setHealth] = useState<EdgeCollectorHealth | null>(null);
  const [contract, setContract] = useState<EdgeCollectorContract | null>(null);
  const [profiles, setProfiles] = useState<EdgeCollectorProfile[]>([]);
  const [collectors, setCollectors] = useState<EdgeCollectorState[]>([]);
  const [collectorId, setCollectorId] = useState("edge-demo-collector-01");
  const [displayName, setDisplayName] = useState("Demo OT-safe Edge Collector 01");
  const [siteName, setSiteName] = useState("Demo Plant");
  const [networkZone, setNetworkZone] = useState("DMZ/Edge");
  const [status, setStatus] = useState("Loading edge collector backend...");
  const [busy, setBusy] = useState(false);

  async function refresh() {
    const [nextHealth, nextContract, nextProfiles, nextStatus] = await Promise.all([
      edgeCollectorApi.health(),
      edgeCollectorApi.contract(),
      edgeCollectorApi.profiles(),
      edgeCollectorApi.status(),
    ]);

    setHealth(nextHealth);
    setContract(nextContract);
    setProfiles(nextProfiles.profiles ?? []);
    setCollectors(nextStatus.collectors ?? []);
    setStatus("Edge collector backend is reachable.");
  }

  useEffect(() => {
    let active = true;
    refresh().catch((error: Error) => {
      if (active) setStatus(`Edge backend not reachable: ${error.message}`);
    });

    return () => {
      active = false;
    };
  }, []);

  const selectedProfiles = useMemo(
    () => (profiles.length ? profiles.map((profile) => profile.profileCode) : ["historian-readonly", "file-drop-readonly"]),
    [profiles],
  );

  const currentCollector = collectors.find((collector) => collector.collectorId === collectorId);

  const summaryCards = useMemo(
    () => [
      { label: "Mode", value: health?.mode ?? "pending" },
      { label: "No inbound OT", value: health ? safetyLabel(health.noInboundOtAccessRequired) : "pending" },
      { label: "Inbound listener", value: health ? safetyLabel(health.opensInboundListener) : "pending" },
      { label: "Collectors", value: String(collectors.length) },
      { label: "Queue depth", value: String(currentCollector?.localQueueDepth ?? 0) },
      { label: "Accepted samples", value: String(currentCollector?.acceptedSamples ?? 0) },
    ],
    [collectors.length, currentCollector?.acceptedSamples, currentCollector?.localQueueDepth, health],
  );

  async function runAction(label: string, action: () => Promise<unknown>) {
    setBusy(true);
    setStatus(label);
    try {
      await action();
      await refresh();
      setStatus(`${label} — done`);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setStatus(`${label} — failed: ${message}`);
    } finally {
      setBusy(false);
    }
  }

  async function registerCollector() {
    await runAction("Registering OT-safe edge collector", () =>
      edgeCollectorApi.register({
        collectorId,
        displayName,
        siteName,
        networkZone,
        agentVersion: "0.1.0-pack-f4-ui",
        pushEndpointUrl: "/api/v5/edge-collector/push-batch",
        readOnlyCollection: true,
        outboundOnly: true,
        opensInboundListener: false,
        sourceProfiles: selectedProfiles,
      }),
    );
  }

  async function sendHeartbeat() {
    await runAction("Sending edge collector heartbeat", () =>
      edgeCollectorApi.heartbeat({
        collectorId,
        agentVersion: "0.1.0-pack-f4-ui",
        observedAtUtc: nowIso(),
        status: "healthy",
        localQueueDepth: 7,
        failedPushCount: 0,
        lastSuccessfulPushUtc: null,
        lastError: null,
      }),
    );
  }

  async function sendQueueStatus() {
    await runAction("Updating edge queue/spool status", () =>
      edgeCollectorApi.queueStatus({
        collectorId,
        queueDepth: 4,
        oldestItemAgeSeconds: 90,
        failedPushCount: 0,
        lastBatchSize: 3,
        lastError: null,
      }),
    );
  }

  async function pushSampleBatch() {
    await runAction("Pushing outbound sample batch", () =>
      edgeCollectorApi.pushBatch({
        collectorId,
        batchId: `ui-batch-${Date.now()}`,
        createdAtUtc: nowIso(),
        readOnlyCollection: true,
        outboundOnly: true,
        sequenceNumber: 1,
        samples: [
          { sourceProfile: "historian-readonly", tagPath: "plant.line1.furnace.temperature.actual", timestampUtc: nowIso(), numericValue: 742.4, textValue: null, unit: "degC", quality: "Good" },
          { sourceProfile: "historian-readonly", tagPath: "plant.line1.mill.force.actual", timestampUtc: nowIso(), numericValue: 1290.2, textValue: null, unit: "kN", quality: "Good" },
          { sourceProfile: "file-drop-readonly", tagPath: "filedrop.quality.score", timestampUtc: nowIso(), numericValue: 97.2, textValue: null, unit: "score", quality: "Good" },
        ],
      }),
    );
  }

  return (
    <main style={{ display: "grid", gap: 18, color: "#eaf7ff" }} data-testid="edge-collector-page">
      <section style={{ ...cardStyle, borderColor: "rgba(19, 216, 255, 0.28)" }}>
        <p style={{ color: "#13d8ff", textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12, margin: 0 }}>
          Pack F · T-068 · Edge collector management UX
        </p>
        <h1 style={{ margin: "8px 0 8px", fontSize: 34 }}>Edge Collector Management</h1>
        <p style={{ margin: 0, color: "#9ab8d7", maxWidth: 980, lineHeight: 1.65 }}>
          Register and monitor OT-safe edge collectors. The contract is read-only toward OT sources, outbound-only toward PlantProcess IQ, and does not require inbound OT firewall access.
        </p>
        <strong style={{ display: "block", marginTop: 16, color: status.includes("failed") ? "#ffb86b" : "#64ffda" }}>{status}</strong>
      </section>

      <section style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: 12 }}>
        {summaryCards.map((card) => (
          <div key={card.label} style={cardStyle}>
            <span style={{ color: "#7e9bb8", fontSize: 12, textTransform: "uppercase" }}>{card.label}</span>
            <strong style={{ display: "block", marginTop: 6, fontSize: 18 }}>{card.value}</strong>
          </div>
        ))}
      </section>

      <section style={{ ...cardStyle, display: "grid", gap: 14 }}>
        <h2 style={{ margin: 0 }}>1. Register edge collector</h2>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 12 }}>
          <label><span style={{ display: "block", marginBottom: 6, color: "#9ab8d7" }}>Collector ID</span><input value={collectorId} onChange={(event) => setCollectorId(event.target.value)} style={inputStyle} /></label>
          <label><span style={{ display: "block", marginBottom: 6, color: "#9ab8d7" }}>Display name</span><input value={displayName} onChange={(event) => setDisplayName(event.target.value)} style={inputStyle} /></label>
          <label><span style={{ display: "block", marginBottom: 6, color: "#9ab8d7" }}>Site name</span><input value={siteName} onChange={(event) => setSiteName(event.target.value)} style={inputStyle} /></label>
          <label><span style={{ display: "block", marginBottom: 6, color: "#9ab8d7" }}>Network zone</span><input value={networkZone} onChange={(event) => setNetworkZone(event.target.value)} style={inputStyle} /></label>
        </div>
        <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
          <button type="button" disabled={busy} onClick={registerCollector} style={buttonStyle}>Register collector</button>
          <button type="button" disabled={busy} onClick={sendHeartbeat} style={buttonStyle}>Send heartbeat</button>
          <button type="button" disabled={busy} onClick={sendQueueStatus} style={buttonStyle}>Update queue status</button>
          <button type="button" disabled={busy} onClick={pushSampleBatch} style={buttonStyle}>Push sample batch</button>
          <button type="button" disabled={busy} onClick={() => void refresh()} style={buttonStyle}>Refresh status</button>
        </div>
      </section>

      <section style={{ display: "grid", gridTemplateColumns: "minmax(280px, 0.8fr) minmax(360px, 1.2fr)", gap: 14 }}>
        <div style={cardStyle}>
          <h2 style={{ marginTop: 0 }}>2. OT-safety contract</h2>
          <ul style={{ color: "#9ab8d7", lineHeight: 1.75, paddingLeft: 18 }}>
            {(contract?.safetyRules ?? [
              "Collector reads only from configured source profiles.",
              "Collector never writes to production assets.",
              "Collector pushes outbound batches only.",
            ]).map((rule) => <li key={rule}>{rule}</li>)}
          </ul>
          <h3>Profiles</h3>
          <div style={{ display: "grid", gap: 8 }}>
            {profiles.map((profile) => (
              <div key={profile.profileCode} style={{ border: "1px solid rgba(120, 190, 255, 0.14)", borderRadius: 14, padding: 10, background: "rgba(2, 7, 18, 0.42)" }}>
                <strong>{profile.displayName}</strong>
                <p style={{ margin: "6px 0", color: "#9ab8d7" }}>{profile.direction}</p>
                <small>Writes to source: {safetyLabel(profile.writesToSource)} · Inbound OT firewall: {safetyLabel(profile.requiresInboundOtFirewallRule)}</small>
              </div>
            ))}
          </div>
        </div>

        <div style={cardStyle}>
          <h2 style={{ marginTop: 0 }}>3. Collector status</h2>
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse" }}>
              <thead><tr style={{ color: "#9ab8d7", textAlign: "left" }}><th style={{ padding: 8 }}>Collector</th><th style={{ padding: 8 }}>Heartbeat</th><th style={{ padding: 8 }}>Queue</th><th style={{ padding: 8 }}>Push</th><th style={{ padding: 8 }}>Safety</th></tr></thead>
              <tbody>
                {collectors.map((collector) => (
                  <tr key={collector.collectorId} style={{ borderTop: "1px solid rgba(120, 190, 255, 0.12)" }}>
                    <td style={{ padding: 8 }}><strong>{collector.displayName}</strong><br /><small>{collector.collectorId}</small></td>
                    <td style={{ padding: 8 }}>{collector.status}<br /><small>{shortTime(collector.lastHeartbeatUtc)}</small></td>
                    <td style={{ padding: 8 }}>{collector.localQueueDepth}<br /><small>failed: {collector.failedPushCount}</small></td>
                    <td style={{ padding: 8 }}>{collector.acceptedSamples} samples<br /><small>{shortTime(collector.lastPushUtc)}</small></td>
                    <td style={{ padding: 8 }}>RO {safetyLabel(collector.readOnlyCollection)} · OUT {safetyLabel(collector.outboundOnly)} · IN {safetyLabel(collector.opensInboundListener)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            {!collectors.length ? <p style={{ color: "#9ab8d7" }}>Register a collector to see heartbeat, queue and push status.</p> : null}
          </div>
        </div>
      </section>

      <section style={cardStyle}>
        <h2 style={{ marginTop: 0 }}>4. Deployment guidance</h2>
        <p style={{ color: "#9ab8d7" }}>Use the packaged edge-agent dry-run before enabling outbound push. Never solve connectivity by opening inbound OT firewall access.</p>
        <pre style={{ whiteSpace: "pre-wrap", color: "#64ffda", background: "rgba(2,7,18,0.75)", padding: 14, borderRadius: 14, overflowX: "auto" }}>{`powershell -ExecutionPolicy Bypass -File .\\scripts\\edge-agent\\Run-PPIQ-EdgeAgent-Local.ps1 -ProjectRoot "C:\\Workspace\\PlantProcess-IQ"\n\npowershell -ExecutionPolicy Bypass -File .\\scripts\\edge-agent\\Run-PPIQ-EdgeAgent-Local.ps1 -ProjectRoot "C:\\Workspace\\PlantProcess-IQ" -Push`}</pre>
      </section>
    </main>
  );
}
