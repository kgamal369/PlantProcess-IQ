import { P2T08_STANDARD_ROLLOUT_MARKER } from "@/components/standard/StandardP2Controls";
import { StandardButton } from "@/components/standard";
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
    >
      <section
        aria-labelledby="edge-title"
      >
        <div>
          <p>
            P5 · OT-Safe Acquisition
          </p>
          <h1 id="edge-title">Edge Collector Management</h1>
          <p>
            Register collectors, verify one-way push status, inspect lag/buffer health, and rotate credentials.
            PPIQ never opens inbound connections to OT and never sends control commands.
          </p>
        </div>

        <div>
          {demoCollectors.map((collector) => (
            <article
              key={collector.collectorCode}
              aria-label={`Collector ${collector.collectorCode}`}
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
                <StandardButton type="button" isDisabled data-disabled-reason="Credential rotation is not yet available.">Rotate credential</StandardButton>
              </div>
            </article>
          ))}
        </div>
      </section>
    </main>
  );
}

export default EdgeCollectorManagementPage;