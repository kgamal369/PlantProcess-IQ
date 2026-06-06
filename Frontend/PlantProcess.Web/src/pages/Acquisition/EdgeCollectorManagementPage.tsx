type CollectorStatus = {
  collectorCode: string;
  siteCode: string;
  sourceSystemCode: string;
  lastPushAtUtc: string;
  lagSeconds: number;
  bufferedBatchCount: number;
  bufferedRowCount: number;
  healthStatus: "Healthy" | "Warning" | "Critical" | "Offline";
  networkProof: string;
  credentialStatus: string;
};

const demoCollectors: CollectorStatus[] = [
  {
    collectorCode: "EDGE-RM-01",
    siteCode: "PLANT-DEMO",
    sourceSystemCode: "HISTORIAN_DEMO",
    lastPushAtUtc: "2026-06-04T08:20:00Z",
    lagSeconds: 45,
    bufferedBatchCount: 0,
    bufferedRowCount: 0,
    healthStatus: "Healthy",
    networkProof: "one-way-push-only / no inbound PPIQ-to-OT / no control path",
    credentialStatus: "active",
  },
  {
    collectorCode: "EDGE-LAB-01",
    siteCode: "PLANT-DEMO",
    sourceSystemCode: "LAB_HISTORY",
    lastPushAtUtc: "2026-06-04T08:12:00Z",
    lagSeconds: 480,
    bufferedBatchCount: 2,
    bufferedRowCount: 184,
    healthStatus: "Warning",
    networkProof: "one-way-push-only / no inbound PPIQ-to-OT / no control path",
    credentialStatus: "rotation-due",
  },
];

export function EdgeCollectorManagementPage() {
  return (
    <main
      style={{
        minHeight: "100vh",
        padding: 24,
        color: "#eaf7ff",
        background: "linear-gradient(135deg,#06101f,#091a33 52%,#071426)",
      }}
    >
      <section
        aria-labelledby="edge-title"
        style={{
          maxWidth: 1120,
          margin: "0 auto",
          border: "1px solid rgba(0,212,255,0.22)",
          borderRadius: 24,
          padding: 24,
          background: "rgba(7,20,38,0.86)",
          display: "grid",
          gap: 18,
        }}
      >
        <div>
          <p style={{ color: "var(--ppiq-color-accent-cyan)", textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12 }}>
            P5 · OT-Safe Acquisition
          </p>
          <h1 id="edge-title">Edge Collector Management</h1>
          <p>
            Register collectors, verify one-way push status, inspect lag/buffer health, and rotate credentials.
            PPIQ never opens inbound connections to OT and never sends control commands.
          </p>
        </div>

        <div style={{ display: "grid", gap: 12 }}>
          {demoCollectors.map((collector) => (
            <article
              key={collector.collectorCode}
              aria-label={`Collector ${collector.collectorCode}`}
              style={{
                border: "1px solid rgba(0,212,255,0.2)",
                borderRadius: 18,
                padding: 16,
                background: "rgba(255,255,255,0.04)",
                display: "grid",
                gridTemplateColumns: "repeat(auto-fit,minmax(210px,1fr))",
                gap: 12,
              }}
            >
              <div>
                <strong>{collector.collectorCode}</strong>
                <div>{collector.siteCode}</div>
                <div>{collector.sourceSystemCode}</div>
              </div>
              <div>
                <strong>Status</strong>
                <div>{collector.healthStatus}</div>
                <div>Lag: {collector.lagSeconds}s</div>
              </div>
              <div>
                <strong>Buffer</strong>
                <div>{collector.bufferedBatchCount} batches</div>
                <div>{collector.bufferedRowCount} rows</div>
              </div>
              <div>
                <strong>Network proof</strong>
                <div>{collector.networkProof}</div>
              </div>
              <div>
                <strong>Credential</strong>
                <div>{collector.credentialStatus}</div>
                <button type="button">Rotate credential</button>
              </div>
            </article>
          ))}
        </div>
      </section>
    </main>
  );
}

export default EdgeCollectorManagementPage;