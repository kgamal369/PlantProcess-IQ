import { useEffect, useMemo, useState } from "react";
import {
  historianConnectorApi,
  type HistorianConnectionTestResult,
  type HistorianHealth,
  type HistorianMappingHintDto,
  type HistorianPointDto,
  type HistorianProviderInfo,
  type HistorianTagDto,
} from "@/api/historianConnector";

const seedTags = [
  "plant.line1.furnace.temperature.actual",
  "plant.line1.mill.force.actual",
  "plant.line1.speed.actual",
  "plant.line1.quality.surface_score",
  "plant.line1.material.current_id",
];

const pageStyle = {
  display: "grid",
  gap: 18,
  color: "#eaf7ff",
} as const;

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

function shortTime(value?: string) {
  if (!value) return "—";
  return new Date(value).toLocaleString();
}

function toggle(list: string[], item: string) {
  return list.includes(item) ? list.filter((x) => x !== item) : [...list, item];
}

export function HistorianConnectorPage() {
  const [health, setHealth] = useState<HistorianHealth | null>(null);
  const [provider, setProvider] = useState<HistorianProviderInfo | null>(null);
  const [endpointUrl, setEndpointUrl] = useState("opc.tcp://demo-historian-gateway:4840");
  const [namespaceUri, setNamespaceUri] = useState("urn:plantprocessiq:demo:historian");
  const [pathPrefix, setPathPrefix] = useState("plant.line1");
  const [selectedTags, setSelectedTags] = useState<string[]>(seedTags.slice(0, 3));
  const [testResult, setTestResult] = useState<HistorianConnectionTestResult | null>(null);
  const [tags, setTags] = useState<HistorianTagDto[]>([]);
  const [points, setPoints] = useState<HistorianPointDto[]>([]);
  const [hints, setHints] = useState<HistorianMappingHintDto[]>([]);
  const [status, setStatus] = useState("Loading historian connector backend...");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let active = true;

    Promise.all([historianConnectorApi.health(), historianConnectorApi.provider()])
      .then(([healthResult, providerResult]) => {
        if (!active) return;
        setHealth(healthResult);
        setProvider(providerResult);
        setStatus("Historian backend is reachable. Ready to register/test/map.");
      })
      .catch((error: Error) => {
        if (!active) return;
        setStatus(`Historian backend not reachable: ${error.message}`);
      });

    return () => {
      active = false;
    };
  }, []);

  const statusCards = useMemo(
    () => [
      { label: "Provider", value: provider?.providerType ?? "pending" },
      { label: "Mode", value: health?.mode ?? "pending" },
      { label: "Connection", value: testResult?.isSuccess ? "accepted" : "not tested" },
      { label: "Tags", value: String(tags.length) },
      { label: "Points", value: String(points.length) },
      { label: "Mapping hints", value: String(hints.length) },
    ],
    [health?.mode, hints.length, points.length, provider?.providerType, tags.length, testResult?.isSuccess],
  );

  async function runAction<T>(label: string, action: () => Promise<T>) {
    setBusy(true);
    setStatus(label);
    try {
      const result = await action();
      setStatus(`${label} — done`);
      return result;
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      setStatus(`${label} — failed: ${message}`);
      throw error;
    } finally {
      setBusy(false);
    }
  }

  async function testConnection() {
    const result = await runAction("Testing read-only historian gateway configuration", () =>
      historianConnectorApi.testConnection({
        providerType: "OpcUaHistorian",
        endpointUrl,
        namespaceUri,
        readOnly: true,
        requireLiveHandshake: false,
        seedTags,
      }),
    );

    setTestResult(result ?? null);
  }

  async function browseTags() {
    const result = await runAction("Browsing historian tag metadata", () =>
      historianConnectorApi.browseTags({ endpointUrl, namespaceUri, pathPrefix, maxTags: 25 }),
    );

    const nextTags = result?.tags ?? [];
    setTags(nextTags);
    if (nextTags.length && selectedTags.length === 0) setSelectedTags(nextTags.slice(0, 3).map((tag) => tag.tagPath));
  }

  async function readWindow() {
    const toUtc = new Date();
    const fromUtc = new Date(toUtc.getTime() - 45 * 60 * 1000);
    const result = await runAction("Reading bounded historian sample window", () =>
      historianConnectorApi.readWindow({
        tagPaths: selectedTags.length ? selectedTags : seedTags.slice(0, 3),
        fromUtc: fromUtc.toISOString(),
        toUtc: toUtc.toISOString(),
        maxPointsPerTag: 8,
      }),
    );

    setPoints(result?.points ?? []);
  }

  async function createMappingHints() {
    const result = await runAction("Creating historian-to-canonical mapping hints", () =>
      historianConnectorApi.mappingHints({
        tagPaths: selectedTags.length ? selectedTags : seedTags,
        materialKeyTag: "plant.line1.material.current_id",
        timestampTag: "system.timestamp",
        qualityTag: "plant.line1.quality.surface_score",
      }),
    );

    setHints(result?.hints ?? []);
  }

  return (
    <main style={pageStyle} data-testid="historian-connector-page">
      <section style={{ ...cardStyle, borderColor: "rgba(19, 216, 255, 0.28)" }}>
        <p style={{ color: "#13d8ff", textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12, margin: 0 }}>
          Pack E · T-063 · Historian connector UI
        </p>
        <h1 style={{ margin: "8px 0 8px", fontSize: 34 }}>Historian Connector</h1>
        <p style={{ margin: 0, color: "#9ab8d7", maxWidth: 980, lineHeight: 1.65 }}>
          Register a read-only OPC-UA / historian gateway source, test the configuration, browse tag metadata, read a bounded sample window, and hand selected tags into canonical mapping. This page is honest: live vendor handshake remains customer-environment specific.
        </p>
        <strong style={{ display: "block", marginTop: 16, color: testResult?.isSuccess ? "#64ffda" : "#eaf7ff" }}>{status}</strong>
      </section>

      <section style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: 12 }}>
        {statusCards.map((card) => (
          <div key={card.label} style={cardStyle}>
            <span style={{ color: "#7e9bb8", fontSize: 12, textTransform: "uppercase" }}>{card.label}</span>
            <strong style={{ display: "block", marginTop: 6, fontSize: 18 }}>{card.value}</strong>
          </div>
        ))}
      </section>

      <section style={{ ...cardStyle, display: "grid", gap: 14 }}>
        <h2 style={{ margin: 0 }}>1. Register configuration</h2>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))", gap: 12 }}>
          <label>
            <span style={{ display: "block", marginBottom: 6, color: "#9ab8d7" }}>Endpoint URL</span>
            <input value={endpointUrl} onChange={(event) => setEndpointUrl(event.target.value)} style={inputStyle} />
          </label>
          <label>
            <span style={{ display: "block", marginBottom: 6, color: "#9ab8d7" }}>Namespace URI</span>
            <input value={namespaceUri} onChange={(event) => setNamespaceUri(event.target.value)} style={inputStyle} />
          </label>
          <label>
            <span style={{ display: "block", marginBottom: 6, color: "#9ab8d7" }}>Browse prefix</span>
            <input value={pathPrefix} onChange={(event) => setPathPrefix(event.target.value)} style={inputStyle} />
          </label>
        </div>
        <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
          <button type="button" disabled={busy} onClick={testConnection} style={buttonStyle}>Test connection</button>
          <button type="button" disabled={busy} onClick={browseTags} style={buttonStyle}>Browse tags</button>
          <button type="button" disabled={busy} onClick={readWindow} style={buttonStyle}>Read sample window</button>
          <button type="button" disabled={busy} onClick={createMappingHints} style={buttonStyle}>Create mapping hints</button>
        </div>
        {provider ? (
          <p style={{ color: "#9ab8d7", margin: 0 }}>
            Provider scope: <strong>{provider.displayName}</strong> · {provider.description}
          </p>
        ) : null}
      </section>

      <section style={{ display: "grid", gridTemplateColumns: "minmax(260px, 0.9fr) minmax(320px, 1.1fr)", gap: 14 }}>
        <div style={cardStyle}>
          <h2 style={{ marginTop: 0 }}>2. Tag browser</h2>
          <div style={{ display: "grid", gap: 8 }}>
            {(tags.length ? tags : seedTags.map((tagPath) => ({ tagPath, displayName: tagPath.split('.').at(-1) ?? tagPath, unit: "engineering-unit", dataType: "Double", suggestedCanonicalGroup: "process-measurement", isTimestampCandidate: false, isQualityCandidate: tagPath.includes("quality"), isProcessMeasurementCandidate: true }))).map((tag) => (
              <label key={tag.tagPath} style={{ display: "grid", gridTemplateColumns: "24px 1fr", gap: 8, alignItems: "start", padding: 10, border: "1px solid rgba(120, 190, 255, 0.14)", borderRadius: 14, background: "rgba(2, 7, 18, 0.42)" }}>
                <input type="checkbox" checked={selectedTags.includes(tag.tagPath)} onChange={() => setSelectedTags((current) => toggle(current, tag.tagPath))} />
                <span>
                  <strong style={{ display: "block" }}>{tag.tagPath}</strong>
                  <small style={{ color: "#9ab8d7" }}>{tag.suggestedCanonicalGroup} · {tag.dataType} · {tag.unit}</small>
                </span>
              </label>
            ))}
          </div>
        </div>

        <div style={cardStyle}>
          <h2 style={{ marginTop: 0 }}>3. Bounded read sample</h2>
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse" }}>
              <thead>
                <tr style={{ color: "#9ab8d7", textAlign: "left" }}>
                  <th style={{ padding: 8 }}>Tag</th>
                  <th style={{ padding: 8 }}>Time</th>
                  <th style={{ padding: 8 }}>Value</th>
                  <th style={{ padding: 8 }}>Quality</th>
                </tr>
              </thead>
              <tbody>
                {points.slice(0, 16).map((point, index) => (
                  <tr key={`${point.tagPath}-${point.timestampUtc}-${index}`} style={{ borderTop: "1px solid rgba(120, 190, 255, 0.12)" }}>
                    <td style={{ padding: 8 }}>{point.tagPath}</td>
                    <td style={{ padding: 8 }}>{shortTime(point.timestampUtc)}</td>
                    <td style={{ padding: 8 }}>{point.value} {point.unit}</td>
                    <td style={{ padding: 8 }}>{point.quality}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            {!points.length ? <p style={{ color: "#9ab8d7" }}>Run “Read sample window” after selecting tags.</p> : null}
          </div>
        </div>
      </section>

      <section style={cardStyle}>
        <h2 style={{ marginTop: 0 }}>4. Mapping handoff</h2>
        <p style={{ color: "#9ab8d7" }}>Selected historian tags become mapping candidates for generic canonical groups. This keeps PlantProcess IQ generic and avoids hardcoded plant/vendor assumptions.</p>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))", gap: 10 }}>
          {hints.map((hint) => (
            <div key={hint.tagPath} style={{ padding: 12, borderRadius: 16, border: "1px solid rgba(0, 212, 255, 0.16)", background: "rgba(2, 7, 18, 0.55)" }}>
              <strong>{hint.suggestedFieldName}</strong>
              <p style={{ margin: "6px 0", color: "#9ab8d7" }}>{hint.tagPath}</p>
              <small>{hint.suggestedCanonicalGroup} · {hint.sourceDataType}</small>
            </div>
          ))}
        </div>
        {!hints.length ? <p style={{ color: "#9ab8d7" }}>Run “Create mapping hints” to generate canonical mapping candidates.</p> : null}
      </section>
    </main>
  );
}
