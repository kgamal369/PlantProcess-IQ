// PPIQ T-262. THE OUTPUT MAPPING INSPECTOR.
//
// An author chose WHERE this definition writes when they chose the output target. This
// is where they say WHAT it writes: for each canonical business field, which authored
// output supplies it.
//
// NOTHING HERE INFERS A MAPPING. An exact same-name match is offered as a SUGGESTION
// with a button beside it, and until that button is pressed nothing is bound. The
// difference matters: a suggestion the author never looked at is not a decision they
// made, and the store would keep it as though it were.
//
// System-owned fields are not offered, because identity, provenance and lifecycle are
// PPIQ invariants rather than author choices. Hiding them here is a courtesy; the
// server refuses them too, which is the actual rule.
//
// This component owns no lifecycle authority. It produces one declaration and the
// server validates it again.

import { useCallback, useEffect, useMemo, useState } from "react";
import { StandardP2Button, StandardP2Select } from "@/components/standard/StandardP2Controls";
import { listOutputTargetFields } from "@/api/canvasProjectionApi";
import type {
  CanonicalProjectionField, CanvasProjectionDeclaration, ProjectionFieldBinding,
} from "@/api/canvasApi";

/// One output the current definition produces, as the author sees it named.
export type AuthoredOutput = {
  kind: "column" | "derived" | "sql";
  table?: string | null;
  name: string;
};

export interface OutputMappingInspectorProps {
  outputTarget: string;
  outputs: AuthoredOutput[];
  declaration: CanvasProjectionDeclaration | null;
  onChange: (declaration: CanvasProjectionDeclaration | null) => void;
  /** T-262. A version saved before declarations existed. Stated, never fabricated. */
  legacyWithoutDeclaration?: boolean;
  /** The server's own refusal, shown verbatim rather than reworded here. */
  refusal?: string | null;
}

const OUTPUT_KEY = (o: AuthoredOutput) => o.kind + "|" + (o.table ?? "") + "|" + o.name;

export function OutputMappingInspector({
  outputTarget, outputs, declaration, onChange, legacyWithoutDeclaration, refusal,
}: OutputMappingInspectorProps) {
  const [fields, setFields] = useState<CanonicalProjectionField[]>([]);
  const [loadFailure, setLoadFailure] = useState<string | null>(null);

  useEffect(() => {
    if (!outputTarget) { setFields([]); setLoadFailure(null); return; }
    let cancelled = false;

    void (async () => {
      try {
        const r = await listOutputTargetFields(outputTarget);
        if (!cancelled) { setFields(r.fields ?? []); setLoadFailure(null); }
      } catch {
        if (!cancelled) {
          setFields([]);
          // A sentence, not an empty list pretending the target has no fields.
          setLoadFailure(
            "The fields of " + outputTarget + " could not be read, so nothing can be mapped to it yet."
            + " Reopen this page once the server answers.");
        }
      }
    })();

    return () => { cancelled = true; };
  }, [outputTarget]);

  // Only business fields are offered. The system-owned ones are counted so the surface
  // can say WHY they are absent instead of leaving a silent gap.
  const writable = useMemo(() => fields.filter((f) => f.isAuthorWritable), [fields]);
  const systemOwnedCount = fields.length - writable.length;

  const boundBy = useMemo(() => {
    const map: Record<string, ProjectionFieldBinding> = {};
    for (const b of declaration?.fieldBindings ?? []) { map[b.targetField] = b; }
    return map;
  }, [declaration]);

  const setBinding = useCallback((targetField: string, output: AuthoredOutput | null) => {
    const kept = (declaration?.fieldBindings ?? []).filter((b) => b.targetField !== targetField);
    const next = output === null
      ? kept
      : kept.concat([{
          targetField,
          sourceKind: output.kind,
          sourceTable: output.kind === "column" ? (output.table ?? null) : null,
          sourceField: output.name,
        }]);

    onChange(next.length === 0 ? null : { targetEntity: outputTarget, fieldBindings: next });
  }, [declaration, outputTarget, onChange]);

  const missingRequired = writable.filter((f) => f.isRequired && !boundBy[f.name]);

  return (
    <section className="canvas-mapping" data-testid="output-mapping-inspector">
      <h4>Output mapping</h4>
      <p className="canvas-mapping__target" data-testid="output-mapping-target">
        {outputTarget
          ? "Writes to " + outputTarget
          : "Choose a governed output target before mapping fields."}
      </p>

      {legacyWithoutDeclaration && (
        <p className="canvas-mapping__legacy" data-testid="output-mapping-legacy" role="status">
          This version was saved before output mapping existed, so it declares none. Nothing was
          filled in on its behalf. Map its fields and save to create a new version that can run.
        </p>
      )}

      {loadFailure && (
        <p className="canvas-mapping__refusal" data-testid="output-mapping-load-failure" role="alert">
          {loadFailure}
        </p>
      )}

      {refusal && (
        <p className="canvas-mapping__refusal" data-testid="output-mapping-refusal" role="alert">
          {refusal}
        </p>
      )}

      {missingRequired.length > 0 && (
        <p className="canvas-mapping__missing" data-testid="output-mapping-missing">
          {missingRequired.length + " required field(s) not mapped: "
            + missingRequired.map((f) => f.name).join(", ")}
        </p>
      )}

      {writable.map((field) => {
        const bound = boundBy[field.name];
        // A suggestion is an EXACT name match and nothing looser. It is shown, never
        // applied: pressing the button is the decision, and the decision is what is
        // stored.
        const suggestion = bound
          ? undefined
          : outputs.filter((o) => o.name === field.name)[0];

        return (
          <div className="canvas-mapping__row" key={field.name} data-testid={"map-" + field.name}>
            <label className="canvas-mapping__field">
              {field.name}
              {field.isRequired ? <span className="canvas-mapping__required"> required</span> : null}
              <span className="canvas-mapping__type"> {field.clrType}</span>
            </label>

            <StandardP2Select
              aria-label={"Source for " + field.name}
              data-testid={"map-select-" + field.name}
              value={bound ? OUTPUT_KEY({
                kind: bound.sourceKind, table: bound.sourceTable, name: bound.sourceField,
              }) : ""}
              onChange={(e) => {
                const chosen = outputs.filter((o) => OUTPUT_KEY(o) === e.target.value)[0];
                setBinding(field.name, chosen ?? null);
              }}
            >
              <option value="">Not mapped</option>
              {outputs.map((o) => (
                <option key={OUTPUT_KEY(o)} value={OUTPUT_KEY(o)}>
                  {(o.table ? o.table + "." : "") + o.name + " (" + o.kind + ")"}
                </option>
              ))}
            </StandardP2Select>

            {suggestion && (
              <StandardP2Button
                variant="ghost"
                data-testid={"map-suggest-" + field.name}
                onClick={() => setBinding(field.name, suggestion)}
              >
                {"Use " + suggestion.name}
              </StandardP2Button>
            )}
          </div>
        );
      })}

      {outputTarget && writable.length === 0 && !loadFailure && (
        <p className="canvas-mapping__empty" data-testid="output-mapping-empty">
          {outputTarget + " exposes no business fields an author may write."}
        </p>
      )}

      {systemOwnedCount > 0 && (
        <p className="canvas-mapping__system" data-testid="output-mapping-system-owned">
          {systemOwnedCount + " field(s) are owned by the platform - identity, provenance and"
            + " lifecycle - and are set by the runtime rather than mapped here."}
        </p>
      )}
    </section>
  );
}

export default OutputMappingInspector;