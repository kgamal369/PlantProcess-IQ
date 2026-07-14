// M1-04 AuthorMappingPage - design-system compliant (PPIQ-T09/T11).
import { useEffect, useMemo, useState } from "react";
import {
  StandardPageHeader,
  StandardButton,
  StandardTable,
  DataFetchBoundary,
  type StandardTableColumn,
} from "@/components/standard";
import {
  createMappingDefinition,
  executeMapping,
  listImportBatches,
  type ExecuteResult,
  type ImportBatch,
} from "@/api/integration/mappingAuthor.api";
import "./AuthorMappingPage.css";

type FieldRow = { idx: number; target: string; source: string };

const TARGET_ENTITIES = [
  "DefectCatalog", "ParameterDefinition", "MaterialUnit", "MaterialAlias",
  "ProcessStepExecution", "ParameterObservation", "QualityEvent", "GenealogyEdge",
];

const SUGGESTED: Record<string, string[]> = {
  DefectCatalog: ["DefectCode", "DefectName", "DefectCategory"],
  ParameterDefinition: ["ParameterCode", "ParameterName", "ValueType", "UnitOfMeasure"],
  MaterialUnit: ["MaterialCode", "MaterialType", "ProducedAtUtc"],
  MaterialAlias: ["MaterialCode", "AliasCode", "AliasType"],
  ProcessStepExecution: ["MaterialCode", "StepCode", "StartedAtUtc"],
  ParameterObservation: ["MaterialCode", "ParameterCode", "ObservedAtUtc", "NumericValue"],
  QualityEvent: ["MaterialCode", "DefectCode", "EventType", "EventAtUtc"],
  GenealogyEdge: ["ParentMaterialCode", "ChildMaterialCode", "RelationshipType"],
};

function readNum(r: ExecuteResult, keys: string[]): number | null {
  for (const k of keys) { const v = r[k]; if (typeof v === "number") return v; }
  return null;
}

export function AuthorMappingPage() {
  const [batches, setBatches] = useState<ImportBatch[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);

  const [batchId, setBatchId] = useState<string>("");
  const [targetEntity, setTargetEntity] = useState<string>("DefectCatalog");
  const [rows, setRows] = useState<FieldRow[]>([{ idx: 0, target: "", source: "" }]);
  const [nextIdx, setNextIdx] = useState(1);

  const [busy, setBusy] = useState(false);
  const [mappingId, setMappingId] = useState<string | null>(null);
  const [result, setResult] = useState<ExecuteResult | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  async function load() {
    setIsLoading(true);
    setError(null);
    try {
      const b = await listImportBatches();
      setBatches(Array.isArray(b) ? b : []);
      const first = Array.isArray(b) ? b[0] : undefined;
      if (first) setBatchId(first.id);
    } catch (e: unknown) {
      setError(e);
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    let cancelled = false;
    listImportBatches()
      .then((b) => {
        if (cancelled) return;
        setBatches(Array.isArray(b) ? b : []);
        const first = Array.isArray(b) ? b[0] : undefined;
        if (first) setBatchId(first.id);
      })
      .catch((e: unknown) => { if (!cancelled) setError(e); })
      .finally(() => { if (!cancelled) setIsLoading(false); });
    return () => { cancelled = true; };
  }, []);

  const selectedBatch = useMemo(() => batches.find((b) => b.id === batchId) ?? null, [batches, batchId]);

  function seedRows(entity: string) {
    const s = SUGGESTED[entity] ?? [];
    if (s.length > 0) {
      setRows(s.map((t, i) => ({ idx: i, target: t, source: "" })));
      setNextIdx(s.length);
    } else {
      setRows([{ idx: 0, target: "", source: "" }]);
      setNextIdx(1);
    }
  }
  function updateRow(idx: number, patch: Partial<FieldRow>) {
    setRows((prev) => prev.map((r) => (r.idx === idx ? { ...r, ...patch } : r)));
  }
  function addRow() {
    setRows((prev) => [...prev, { idx: nextIdx, target: "", source: "" }]);
    setNextIdx((n) => n + 1);
  }
  function removeRow(idx: number) {
    setRows((prev) => prev.filter((r) => r.idx !== idx));
  }

  const fieldColumns: ReadonlyArray<StandardTableColumn<FieldRow>> = [
    {
      key: "target", header: "Target field",
      cell: (r) => (
        <input className="ppiq-am-input" value={r.target} placeholder="e.g. DefectCode"
          onChange={(e) => updateRow(r.idx, { target: e.target.value })} />
      ),
    },
    {
      key: "source", header: "Source (column or const:VALUE)",
      cell: (r) => (
        <input className="ppiq-am-input" value={r.source} placeholder="e.g. defect_code or const:CRACK_LONG"
          onChange={(e) => updateRow(r.idx, { source: e.target.value })} />
      ),
    },
    {
      key: "actions", header: "", align: "right",
      cell: (r) => (
        <StandardButton variant="secondary" size="sm" onClick={() => removeRow(r.idx)}>
          Remove
        </StandardButton>
      ),
    },
  ];

  async function onSave() {
    if (!selectedBatch) { setFormError("Pick an import batch first."); return; }
    const map: Record<string, string> = {};
    rows.forEach((r) => { if (r.target.trim() && r.source.trim()) map[r.target.trim()] = r.source.trim(); });
    if (Object.keys(map).length === 0) {
      setFormError("Add at least one field map (target field + source column or const:VALUE).");
      return;
    }
    setFormError(null); setNotice(null); setBusy(true);
    try {
      const res = await createMappingDefinition({
        sourceSystemDefinitionId: selectedBatch.sourceSystemDefinitionId,
        mappingCode: `UI-${selectedBatch.sourceObjectName}-${targetEntity}-${Date.now()}`,
        mappingName: `${targetEntity} from ${selectedBatch.sourceObjectName}`,
        sourceObjectName: selectedBatch.sourceObjectName,
        targetEntityName: targetEntity,
        mappingJson: JSON.stringify(map),
        mappingVersion: "v1",
        description: "Authored in HMI (M1-04 step-4)",
        isSynthetic: false,
        sourceSystem: selectedBatch.sourceSystem ?? null,
        sourceRecordId: null,
      });
      setMappingId(res.id);
      setNotice(`Mapping saved (id ${res.id}). Now Execute to project this batch.`);
    } catch (e: unknown) {
      setFormError(e instanceof Error ? e.message : "Could not save the mapping.");
    } finally {
      setBusy(false);
    }
  }

  async function onExecute() {
    if (!mappingId || !selectedBatch) return;
    setFormError(null); setBusy(true);
    try {
      const res = await executeMapping(mappingId, selectedBatch.id);
      setResult(res);
      setNotice("Executed. Staged rows for this batch should now be Mapped and canonical rows grew.");
    } catch (e: unknown) {
      setFormError(e instanceof Error ? e.message : "Could not execute the mapping.");
    } finally {
      setBusy(false);
    }
  }

  const mapped = result ? readNum(result, ["mapped", "mappedCount", "mappedRows"]) : null;
  const failed = result ? readNum(result, ["failed", "failedCount", "errors"]) : null;
  const total = result ? readNum(result, ["total", "processed", "rowCount"]) : null;

  return (
    <div className="ppiq-author-mapping">
      <StandardPageHeader
        title="Load to Plant Data"
        subtitle="Author a mapping for a staged object and project it into the canonical plant schema (journey step 4-6)."
      />
      <DataFetchBoundary
        title="Import batches"
        isLoading={isLoading}
        error={error}
        isEmpty={batches.length === 0}
        emptyMessage="No import batches yet. Connect a source and import data first (steps 1-3), then return here."
        onRetry={() => void load()}
      >
        <div className="ppiq-am-row">
          <label className="ppiq-am-label">
            Import batch
            <select className="ppiq-am-select" value={batchId}
              onChange={(e) => { setBatchId(e.target.value); setMappingId(null); setResult(null); }}>
              {batches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.sourceObjectName} - {b.status ?? "unknown"} - {b.startedAtUtc ?? ""}
                </option>
              ))}
            </select>
          </label>
          <label className="ppiq-am-label">
            Target entity
            <select className="ppiq-am-select" value={targetEntity}
              onChange={(e) => { setTargetEntity(e.target.value); seedRows(e.target.value); setMappingId(null); setResult(null); }}>
              {TARGET_ENTITIES.map((t) => (<option key={t} value={t}>{t}</option>))}
            </select>
          </label>
        </div>

        {selectedBatch && (
          <p className="ppiq-am-muted">
            Source object <strong>{selectedBatch.sourceObjectName}</strong> from system{" "}
            <strong>{selectedBatch.sourceSystem ?? "-"}</strong>. Source can be a column name or{" "}
            <code>const:VALUE</code> for a literal.
          </p>
        )}

        <StandardTable
          columns={fieldColumns}
          data={rows}
          getRowKey={(r) => String(r.idx)}
        />

        <div className="ppiq-am-actions">
          <StandardButton onClick={addRow} isDisabled={busy}>Add field</StandardButton>
          <StandardButton onClick={onSave} isDisabled={busy || !selectedBatch}>Save mapping</StandardButton>
          <StandardButton onClick={onExecute} isDisabled={busy || !mappingId}>Execute (project)</StandardButton>
        </div>

        {notice && <div className="ppiq-am-notice">{notice}</div>}
        {formError && <div className="ppiq-am-error">{formError}</div>}

        {result && (
          <div className="ppiq-am-result">
            <strong>Projection result</strong>
            <div className="ppiq-am-result-nums">
              {mapped !== null && <span>Mapped: {mapped}</span>}
              {failed !== null && <span>Failed: {failed}</span>}
              {total !== null && <span>Total: {total}</span>}
            </div>
            <pre className="ppiq-am-json">{JSON.stringify(result, null, 2)}</pre>
          </div>
        )}
      </DataFetchBoundary>
    </div>
  );
}

export default AuthorMappingPage;