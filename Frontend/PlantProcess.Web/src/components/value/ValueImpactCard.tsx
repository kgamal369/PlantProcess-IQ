import { useState } from "react";
import { StandardButton } from "@/components/standard";

export type ProvenanceHandleRef = { kind: string; id: string; detail?: string | null };

export type ValueTerm = {
  name: string;
  inputsJson: string;
  low: number;
  mid: number;
  high: number;
  handle?: ProvenanceHandleRef | null;
};

export type ValueImpact = {
  currency: string;
  low: number;
  mid: number;
  high: number;
  terms: ValueTerm[];
  assumptionVersion: number;
  isAbstained: boolean;
  abstainReason?: string | null;
};

const CAVEAT = "Impact is an estimated range from your cost assumptions, not a guaranteed amount.";

const money = (v: number, ccy: string) =>
  new Intl.NumberFormat(undefined, { style: "currency", currency: ccy || "EUR", maximumFractionDigits: 0 }).format(v);

export function ValueImpactCard({
  impact,
  onOpenEvidence,
}: {
  impact: ValueImpact;
  onOpenEvidence?: (handle: ProvenanceHandleRef) => void;
}) {
  const [open, setOpen] = useState<string | null>(null);

  if (impact.isAbstained) {
    return (
      <section data-testid="value-abstain" style={{ border: "1px dashed #b9770e", borderRadius: 10, padding: 14, background: "#fffaf0" }}>
        <strong style={{ color: "#b9770e" }}>Insufficient basis</strong>
        <p style={{ margin: "6px 0 0", fontSize: 13, color: "#6b7280" }}>
          {impact.abstainReason ?? "A required cost assumption is missing, so no figure is shown."}
        </p>
        <p style={{ margin: "10px 0 0", fontSize: 12, fontStyle: "italic", color: "#6b7280" }}>{CAVEAT}</p>
      </section>
    );
  }

  return (
    <section data-testid="value-card" style={{ border: "1px solid #d8dee9", borderRadius: 10, padding: 14, background: "#fff" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline" }}>
        <strong style={{ fontSize: 14 }}>Estimated impact</strong>
        <span style={{ fontSize: 12, color: "#6b7280" }}>assumptions v{impact.assumptionVersion}</span>
      </div>
      <div data-testid="value-range" style={{ fontSize: 22, fontWeight: 700, margin: "6px 0" }}>
        {money(impact.low, impact.currency)} &ndash; {money(impact.high, impact.currency)}
        <span style={{ fontSize: 13, fontWeight: 500, color: "#6b7280" }}> (mid {money(impact.mid, impact.currency)})</span>
      </div>

      <div style={{ marginTop: 8 }}>
        {impact.terms
          .filter((t) => !!t.handle) /* guard: a term with no resolvable handle is not rendered */
          .map((t) => (
            <div key={t.name} style={{ borderTop: "1px solid #eef1f5", padding: "6px 0" }}>
              <StandardButton
                type="button"
                onClick={() => setOpen(open === t.name ? null : t.name)}
                style={{ display: "flex", justifyContent: "space-between", width: "100%", background: "none", border: "none", padding: 0, cursor: "pointer", fontSize: 13 }}
              >
                <span style={{ color: "#374151" }}>{t.name}</span>
                <span style={{ fontWeight: 600 }}>
                  {money(t.low, impact.currency)} &ndash; {money(t.high, impact.currency)}
                </span>
              </StandardButton>
              {open === t.name ? (
                <div style={{ marginTop: 6, fontSize: 12, color: "#6b7280" }}>
                  <div>Inputs: <code>{t.inputsJson}</code></div>
                  <div style={{ marginTop: 4 }}>
                    Assumptions v{impact.assumptionVersion}
                    {t.handle ? (
                      <>
                        {" · "}
                        <StandardButton
                          type="button"
                          data-testid="value-term-evidence"
                          onClick={() => onOpenEvidence?.(t.handle as ProvenanceHandleRef)}
                          style={{ color: "#3b6fb5", background: "none", border: "none", padding: 0, cursor: "pointer", textDecoration: "underline" }}
                        >
                          source finding ({t.handle.kind})
                        </StandardButton>
                      </>
                    ) : null}
                  </div>
                </div>
              ) : null}
            </div>
          ))}
      </div>

      <p style={{ margin: "10px 0 0", fontSize: 12, fontStyle: "italic", color: "#6b7280" }}>{CAVEAT}</p>
    </section>
  );
}