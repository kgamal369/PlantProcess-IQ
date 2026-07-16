import { useEffect, useMemo, useState } from "react";
import {
  DataFetchBoundary,
  StandardButton,
  StandardCard,
  StandardInput,
  StandardPageHeader,
  StandardSelect,
  StandardTable,
  type StandardSelectOption,
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
  "DefectCatalog",
  "ParameterDefinition",
  "MaterialUnit",
  "MaterialAlias",
  "ProcessStepExecution",
  "ParameterObservation",
  "QualityEvent",
  "GenealogyEdge",
] as const;

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

const targetOptions: ReadonlyArray<StandardSelectOption> = TARGET_ENTITIES.map((entity) => ({
  value: entity,
  label: entity,
}));

function readNum(result: ExecuteResult, keys: string[]): number | null {
  for (const key of keys) {
    const value = result[key];
    if (typeof value === "number") return value;
  }
  return null;
}

export function AuthorMappingPage() {
  const [batches, setBatches] = useState<ImportBatch[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const [batchId, setBatchId] = useState("");
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
      const response = await listImportBatches();
      const next = Array.isArray(response) ? response : [];
      setBatches(next);
      if (next[0]) setBatchId((current) => current || next[0].id);
    } catch (caught: unknown) {
      setError(caught);
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    let cancelled = false;
    listImportBatches()
      .then((response) => {
        if (cancelled) return;
        const next = Array.isArray(response) ? response : [];
        setBatches(next);
        if (next[0]) setBatchId(next[0].id);
      })
      .catch((caught: unknown) => {
        if (!cancelled) setError(caught);
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const selectedBatch = useMemo(
    () => batches.find((batch) => batch.id === batchId) ?? null,
    [batches, batchId],
  );

  const batchOptions = useMemo<ReadonlyArray<StandardSelectOption>>(
    () =>
      batches.map((batch) => ({
        value: batch.id,
        label: `${batch.sourceObjectName} - ${batch.status ?? "Unknown"} - ${batch.startedAtUtc ?? "No start time"}`,
        searchText: `${batch.sourceObjectName} ${batch.sourceSystem ?? ""} ${batch.status ?? ""}`,
      })),
    [batches],
  );

  function seedRows(entity: string) {
    const suggestions = SUGGESTED[entity] ?? [];
    const next = suggestions.length
      ? suggestions.map((target, index) => ({ idx: index, target, source: "" }))
      : [{ idx: 0, target: "", source: "" }];
    setRows(next);
    setNextIdx(next.length);
  }

  function updateRow(idx: number, patch: Partial<FieldRow>) {
    setRows((current) => current.map((row) => (row.idx === idx ? { ...row, ...patch } : row)));
  }

  function addRow() {
    setRows((current) => [...current, { idx: nextIdx, target: "", source: "" }]);
    setNextIdx((current) => current + 1);
  }

  function removeRow(idx: number) {
    setRows((current) => current.filter((row) => row.idx !== idx));
  }

  const fieldColumns: ReadonlyArray<StandardTableColumn<FieldRow>> = [
    {
      key: "target",
      header: "Target field",
      cell: (row) => (
        <StandardInput
          className="ppiq-am-table-field"
          value={row.target}
          placeholder="e.g. DefectCode"
          aria-label={`Target field ${row.idx + 1}`}
          size="sm"
          onChange={(value) => updateRow(row.idx, { target: value })}
        />
      ),
    },
    {
      key: "source",
      header: "Source column or literal",
      cell: (row) => (
        <StandardInput
          className="ppiq-am-table-field"
          value={row.source}
          placeholder="e.g. defect_code or const:CRACK_LONG"
          aria-label={`Source field ${row.idx + 1}`}
          size="sm"
          onChange={(value) => updateRow(row.idx, { source: value })}
        />
      ),
    },
    {
      key: "actions",
      header: "Action",
      align: "right",
      cell: (row) => (
        <StandardButton variant="ghost" size="sm" onClick={() => removeRow(row.idx)}>
          Remove
        </StandardButton>
      ),
    },
  ];

  async function onSave() {
    if (!selectedBatch) {
      setFormError("Select an import batch first.");
      return;
    }

    const map: Record<string, string> = {};
    rows.forEach((row) => {
      if (row.target.trim() && row.source.trim()) map[row.target.trim()] = row.source.trim();
    });

    if (Object.keys(map).length === 0) {
      setFormError("Add at least one field map (target field + source column or const:VALUE).");
      return;
    }

    setFormError(null);
    setNotice(null);
    setBusy(true);
    try {
      const response = await createMappingDefinition({
        sourceSystemDefinitionId: selectedBatch.sourceSystemDefinitionId,
        mappingCode: `UI-${selectedBatch.sourceObjectName}-${targetEntity}-${Date.now()}`,
        mappingName: `${targetEntity} from ${selectedBatch.sourceObjectName}`,
        sourceObjectName: selectedBatch.sourceObjectName,
        targetEntityName: targetEntity,
        mappingJson: JSON.stringify(map),
        mappingVersion: "v1",
        description: "Authored in the PlantProcess IQ data-preparation workspace.",
        isSynthetic: false,
        sourceSystem: selectedBatch.sourceSystem ?? null,
        sourceRecordId: null,
      });
      setMappingId(response.id);
      setNotice("Mapping saved. Review it, then execute the projection for this batch.");
    } catch (caught: unknown) {
      setFormError(caught instanceof Error ? caught.message : "Could not save the mapping.");
    } finally {
      setBusy(false);
    }
  }

  async function onExecute() {
    if (!mappingId || !selectedBatch) return;
    setFormError(null);
    setBusy(true);
    try {
      const response = await executeMapping(mappingId, selectedBatch.id);
      setResult(response);
      setNotice("Projection completed. Review mapped and failed rows below.");
    } catch (caught: unknown) {
      setFormError(caught instanceof Error ? caught.message : "Could not execute the mapping.");
    } finally {
      setBusy(false);
    }
  }

  const mapped = result ? readNum(result, ["mapped", "mappedCount", "mappedRows"]) : null;
  const failed = result ? readNum(result, ["failed", "failedCount", "failedRows", "errors"]) : null;
  const total = result ? readNum(result, ["total", "processed", "rowCount"]) : null;

  return (
    <div className="ppiq-author-mapping">
      <StandardPageHeader
        title="Load to Plant Data"
        subtitle="Map staged source fields into the canonical plant model, then execute the projection."
        description="Use customer taxonomy first, keep every source field explicit, and review failures before moving to analysis."
      />

      <DataFetchBoundary
        title="Import batches"
        isLoading={isLoading}
        error={error}
        isEmpty={batches.length === 0}
        emptyMessage="No import batches yet. Connect a source and run an import, then return here."
        onRetry={() => void load()}
      >
        <div className="ppiq-am-workbench">
          <StandardCard
            eyebrow="Step 1"
            title="Choose the source and target"
            subtitle="Select one completed import batch and the canonical entity it should populate."
            elevation="flat"
          >
            <div className="ppiq-am-row">
              <StandardSelect
                label="Import batch"
                helperText="Search by source object, provider or run status."
                value={batchId}
                options={batchOptions}
                searchable
                onChange={(value) => {
                  setBatchId(String(value));
                  setMappingId(null);
                  setResult(null);
                }}
              />
              <StandardSelect
                label="Target entity"
                helperText="The target determines the required canonical fields."
                value={targetEntity}
                options={targetOptions}
                onChange={(value) => {
                  const entity = String(value);
                  setTargetEntity(entity);
                  seedRows(entity);
                  setMappingId(null);
                  setResult(null);
                }}
              />
            </div>

            {selectedBatch ? (
              <div className="ppiq-am-source-summary" aria-label="Selected import batch summary">
                <span><small>Source object</small><strong>{selectedBatch.sourceObjectName}</strong></span>
                <span><small>Source system</small><strong>{selectedBatch.sourceSystem ?? "Not reported"}</strong></span>
                <span><small>Literal syntax</small><code>const:VALUE</code></span>
              </div>
            ) : null}
          </StandardCard>

          <StandardCard
            eyebrow="Step 2"
            title="Define the field map"
            subtitle="Each row maps one canonical target field to a source column or an explicit literal."
            elevation="flat"
          >
            <div className="ppiq-am-table-wrap">
              <StandardTable columns={fieldColumns} data={rows} getRowKey={(row) => String(row.idx)} />
            </div>

            <div className="ppiq-am-actions">
              <StandardButton variant="secondary" onClick={addRow} isDisabled={busy}>
                Add field
              </StandardButton>
              <StandardButton variant="secondary" onClick={onSave} isDisabled={busy || !selectedBatch} isLoading={busy && !mappingId}>
                Save mapping
              </StandardButton>
              <StandardButton onClick={onExecute} isDisabled={busy || !mappingId} isLoading={busy && Boolean(mappingId)}>
                Execute projection
              </StandardButton>
            </div>

            {notice ? <div className="ppiq-am-notice" role="status">{notice}</div> : null}
            {formError ? <div className="ppiq-am-error" role="alert">{formError}</div> : null}
          </StandardCard>

          {result ? (
            <StandardCard
              eyebrow="Step 3"
              title="Projection result"
              subtitle="Mapped and failed rows are reported separately; failures remain traceable in job logs."
              elevation="flat"
            >
              <div className="ppiq-am-result-nums">
                {mapped !== null ? <span>Mapped: {mapped}</span> : null}
                {failed !== null ? <span>Failed: {failed}</span> : null}
                {total !== null ? <span>Total: {total}</span> : null}
              </div>

              <details className="ppiq-journey-disclosure">
                <summary>Technical response details</summary>
                <div className="ppiq-journey-disclosure__content">
                  <pre className="ppiq-am-json">{JSON.stringify(result, null, 2)}</pre>
                </div>
              </details>
            </StandardCard>
          ) : null}
        </div>
      </DataFetchBoundary>
    </div>
  );
}

export default AuthorMappingPage;
