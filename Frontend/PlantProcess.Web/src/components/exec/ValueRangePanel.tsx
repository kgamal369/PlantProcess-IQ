/* PPIQ-PHASE6 ValueRangePanel - named bounded euro range with per-term drill-
 * through and the abstain path. Term toggles are StandardButtons. */
import { useState } from "react";
import { StandardButton } from "../standard";
import type { ValueImpactResult } from "../../types/execOpsContracts";

const T = { navy: "#050B18", panel: "#0B1730", line: "#16243D", cyan: "#00D4FF", ok: "#2CE6A2", warn: "#FFB020", crit: "#FF4D6D", white: "#EAF6FF", steel: "#8EA7C1" };

function fmt(currency: string, v: number) {
  try { return new Intl.NumberFormat(undefined, { style: "currency", currency, maximumFractionDigits: 0 }).format(v); }
  catch { return `${currency} ${Math.round(v).toLocaleString()}`; }
}

export function ValueRangePanel({ result }: { result: ValueImpactResult }) {
  const [openTerm, setOpenTerm] = useState<string | null>(null);

  if (result?.abstain?.abstained) {
    return (
      <div data-testid="value-range" style={{ background: T.panel, border: `1px solid ${T.warn}`, borderRadius: 12, padding: 16 }}>
        <div data-testid="abstain-panel" style={{ color: T.warn, fontWeight: 700, fontFamily: "'JetBrains Mono', monospace", fontSize: 12 }}>
          INSUFFICIENT EVIDENCE - ABSTAINING
        </div>
        <div style={{ color: T.steel, fontSize: 13, marginTop: 6 }}>
          {result.abstain.reason || "A required assumption is missing, so no euro figure is shown."}
        </div>
        {result.abstain.missingInputs?.length ? (
          <ul data-testid="abstain-missing" style={{ color: T.steel, fontSize: 12, margin: "8px 0 0 16px" }}>
            {result.abstain.missingInputs.map((m) => <li key={m}>{m}</li>)}
          </ul>
        ) : null}
      </div>
    );
  }

  const Big = ({ id, label, v, color }: { id: string; label: string; v: number; color: string }) => (
    <div style={{ textAlign: "center", flex: 1 }}>
      <div style={{ color: T.steel, fontSize: 11, fontFamily: "'JetBrains Mono', monospace" }}>{label}</div>
      <div data-testid={id} style={{ color, fontSize: "clamp(20px,3vw,30px)", fontWeight: 800 }}>{fmt(result.currency, v)}</div>
    </div>
  );

  return (
    <div data-testid="value-range" style={{ background: T.panel, border: `1px solid ${T.line}`, borderRadius: 12, padding: 16 }}>
      <div style={{ display: "flex", gap: 12, alignItems: "flex-end" }}>
        <Big id="value-low" label="LOW" v={result.low} color={T.steel} />
        <Big id="value-expected" label="EXPECTED" v={result.expected} color={T.ok} />
        <Big id="value-high" label="HIGH" v={result.high} color={T.cyan} />
      </div>

      <div style={{ marginTop: 14, borderTop: `1px solid ${T.line}`, paddingTop: 10 }}>
        <div style={{ color: T.steel, fontSize: 11, fontFamily: "'JetBrains Mono', monospace", marginBottom: 6 }}>
          DRILL-THROUGH - every figure is grounded in an input
        </div>
        {result.terms.map((term) => {
          const open = openTerm === term.key;
          return (
            <div key={term.key} data-testid="value-term" data-key={term.key} style={{ borderBottom: `1px solid ${T.line}` }}>
              <StandardButton
                variant="ghost"
                fullWidth
                data-testid="value-term-toggle"
                onClick={() => setOpenTerm(open ? null : term.key)}
                style={{ color: T.white, justifyContent: "flex-start", padding: "8px 0" }}
              >
                <span style={{ display: "flex", justifyContent: "space-between", width: "100%" }}>
                  <span>{term.label}</span>
                  <span style={{ color: T.cyan, fontFamily: "'JetBrains Mono', monospace" }}>
                    {term.sourceValue}{term.unit ? ` ${term.unit}` : ""}
                  </span>
                </span>
              </StandardButton>
              {open ? (
                <pre data-testid="value-term-drill" style={{ background: T.navy, color: T.steel, fontSize: 12, padding: 10, borderRadius: 8, overflowX: "auto", margin: "0 0 8px" }}>
{JSON.stringify(term.inputJson ?? { sourceValue: term.sourceValue, unit: term.unit ?? null }, null, 2)}
                </pre>
              ) : null}
            </div>
          );
        })}
      </div>
    </div>
  );
}
export default ValueRangePanel;