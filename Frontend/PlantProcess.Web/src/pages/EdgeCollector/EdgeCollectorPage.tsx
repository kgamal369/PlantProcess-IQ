import { useEffect, useMemo, useState } from "react";
import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Input, StandardP2Table } from "@/components/standard/StandardP2Controls";
import { StandardButton, StandardPageHeader, StandardStatGrid } from "@/components/standard";
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
  const [collectorId, setCollectorId] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [siteName, setSiteName] = useState("Plant");
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
    <main data-testid="edge-collector-page">
      <StandardPageHeader
        title="Edge Collector Management"
        subtitle="Register and monitor OT-safe collectors. Data flows one way, out of the plant network."
        description="Edge collectors read from OT sources and push outbound to PlantProcess IQ. There is no inbound listener and no inbound firewall rule is required. The contract is read-only toward OT at all times."
        status={status}
      />

      <StandardStatGrid items={summaryCards} emphasize="Queue depth" />

      <section>
        <h2>1. Register edge collector</h2>
        <div>
          <label><span>Collector ID</span><StandardP2Input value={collectorId} onChange={(event) => setCollectorId(event.target.value)} /></label>
          <label><span>Display name</span><StandardP2Input value={displayName} onChange={(event) => setDisplayName(event.target.value)} /></label>
          <label><span>Site name</span><StandardP2Input value={siteName} onChange={(event) => setSiteName(event.target.value)} /></label>
          <label><span>Network zone</span><StandardP2Input value={networkZone} onChange={(event) => setNetworkZone(event.target.value)} /></label>
        </div>
        <div>
          <StandardButton type="button" isDisabled={busy} onClick={registerCollector}>Register collector</StandardButton>
          <StandardButton type="button" isDisabled={busy} onClick={sendHeartbeat}>Send heartbeat</StandardButton>
          <StandardButton type="button" isDisabled={busy} onClick={sendQueueStatus}>Update queue status</StandardButton>
          <StandardButton type="button" isDisabled={busy} onClick={pushSampleBatch}>Push sample batch</StandardButton>
          <StandardButton type="button" isDisabled={busy} onClick={() => void refresh()}>Refresh status</StandardButton>
        </div>
      </section>

      <section>
        <div>
          <h2>2. OT-safety contract</h2>
          <ul>
            {(contract?.safetyRules ?? [
              "Collector reads only from configured source profiles.",
              "Collector never writes to production assets.",
              "Collector pushes outbound batches only.",
            ]).map((rule) => <li key={rule}>{rule}</li>)}
          </ul>
          <h3>Profiles</h3>
          <div>
            {profiles.map((profile) => (
              <div key={profile.profileCode}>
                <strong>{profile.displayName}</strong>
                <p>{profile.direction}</p>
                <small>Writes to source: {safetyLabel(profile.writesToSource)} · Inbound OT firewall: {safetyLabel(profile.requiresInboundOtFirewallRule)}</small>
              </div>
            ))}
          </div>
        </div>

        <div>
          <h2>3. Collector status</h2>
          <div>
            <StandardP2Table>
              <thead><tr><th>Collector</th><th>Heartbeat</th><th>Queue</th><th>Push</th><th>Safety</th></tr></thead>
              <tbody>
                {collectors.map((collector) => (
                  <tr key={collector.collectorId}>
                    <td><strong>{collector.displayName}</strong><br /><small>{collector.collectorId}</small></td>
                    <td>{collector.status}<br /><small>{shortTime(collector.lastHeartbeatUtc)}</small></td>
                    <td>{collector.localQueueDepth}<br /><small>failed: {collector.failedPushCount}</small></td>
                    <td>{collector.acceptedSamples} samples<br /><small>{shortTime(collector.lastPushUtc)}</small></td>
                    <td>RO {safetyLabel(collector.readOnlyCollection)} · OUT {safetyLabel(collector.outboundOnly)} · IN {safetyLabel(collector.opensInboundListener)}</td>
                  </tr>
                ))}
              </tbody>
            </StandardP2Table>
            {!collectors.length ? <p>Register a collector to see heartbeat, queue and push status.</p> : null}
          </div>
        </div>
      </section>

      <section>
        <h2>4. Deployment guidance</h2>
        <p>Use the packaged edge-agent dry-run before enabling outbound push. Never solve connectivity by opening inbound OT firewall access.</p>
        <pre>{`powershell -ExecutionPolicy Bypass -File .\\scripts\\edge-agent\\Run-PPIQ-EdgeAgent-Local.ps1 -ProjectRoot "C:\\Workspace\\PlantProcess-IQ"\n\npowershell -ExecutionPolicy Bypass -File .\\scripts\\edge-agent\\Run-PPIQ-EdgeAgent-Local.ps1 -ProjectRoot "C:\\Workspace\\PlantProcess-IQ" -Push`}</pre>
      </section>
    </main>
  );
}
