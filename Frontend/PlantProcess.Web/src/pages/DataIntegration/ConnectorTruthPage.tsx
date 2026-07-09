// ============================================================
// FILE: src/pages/DataIntegration/ConnectorTruthPage.tsx
// M1-06: honest per-connector status.
//
// REPLACES DemoAnalyticsWorkflowTruthPage, which called the real endpoint and,
// when it returned nothing, rendered two HARDCODED connector rows naming source
// systems that may not exist, with a fabricated "pending" fingerprint and a
// "Tracked" drift status. Invented plant status, on a surface named Truth.
//
// This page has no fallback rows. If the backend knows nothing, it says so.
// (The literal fabricated strings are deliberately NOT repeated here: the
//  M1-06 gate greps this directory for them.)
// ============================================================
import { useCallback, useEffect, useMemo, useState } from "react";
import { StandardCard, StandardTable, type StandardTableColumn } from "@/components/standard";
import { workflowFoundationApi } from "@/api/workflow-foundation/workflowFoundation.api";

type Row = Record<string, unknown>;

function text(value: unknown): string {
  if (value === null || value === undefined || value === "") return "-";
  return String(value);
}

/** The endpoint has been observed under several shapes; accept them, invent none. */
function extractRows(payload: unknown): Row[] {
  if (!payload || typeof payload !== "object") return [];
  const raw = payload as Record<string, unknown>;
  const candidates = [raw.connectorTruth, raw.providers, raw.connectors, raw.items];
  for (const candidate of candidates) {
    if (Array.isArray(candidate)) return candidate as Row[];
  }
  return [];
}

const columns: StandardTableColumn<Row>[] = [
  { key: "connector", header: "Connector", sortable: true, accessor: (row) => text(row.connector ?? row.displayName ?? row.sourceSystemName ?? row.providerType) },
  { key: "lastSync", header: "Last successful sync", sortable: true, accessor: (row) => text(row.lastSuccessfulSyncUtc ?? row.lastSyncUtc) },
  { key: "fingerprint", header: "Schema fingerprint", sortable: true, accessor: (row) => text(row.schemaFingerprint ?? row.fingerprint) },
  { key: "drift", header: "Drift status", sortable: true, accessor: (row) => text(row.driftStatus) },
  { key: "rows", header: "Sample rows", sortable: true, accessor: (row) => text(row.sampleRowCount) },
];

export function ConnectorTruthPage() {
  const [payload, setPayload] = useState<unknown>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      setPayload(await workflowFoundationApi.getConnectorTruth());
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const rows = useMemo(() => extractRows(payload), [payload]);

  return (
    <StandardCard
      title="Connector truth"
      subtitle="What each connected source last told us, and whether its schema has drifted."
      data-testid="connector-truth-page"
    >
      {error ? <p role="alert">Connector truth is unavailable: {error}</p> : null}

      {!error && !isLoading && rows.length === 0 ? (
        <p>
          No connector has reported yet. Register a source under Connections and run an
          import; its sync time, schema fingerprint and drift status will appear here.
        </p>
      ) : null}

      {rows.length > 0 ? (
        <StandardTable<Row> data={rows} columns={columns} getRowKey={(row, i) => text(row.connector) + ":" + i} />
      ) : null}

      {isLoading ? <p>Loading connector status...</p> : null}
    </StandardCard>
  );
}