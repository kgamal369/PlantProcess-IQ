import { useState } from "react";
import { StandardButton } from "@/components/standard";

import { P2T08_STANDARD_ROLLOUT_MARKER } from "@/components/standard/StandardP2Controls";
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
      <section data-testid="value-abstain">
        <strong>Insufficient basis</strong>
        <p>
          {impact.abstainReason ?? "A required cost assumption is missing, so no figure is shown."}
        </p>
        <p>{CAVEAT}</p>
      </section>
    );
  }

  return (
    <section data-testid="value-card">
      <div>
        <strong>Estimated impact</strong>
        <span>assumptions v{impact.assumptionVersion}</span>
      </div>
      <div data-testid="value-range">
        {money(impact.low, impact.currency)} &ndash; {money(impact.high, impact.currency)}
        <span> (mid {money(impact.mid, impact.currency)})</span>
      </div>

      <div>
        {impact.terms
          .filter((t) => !!t.handle) /* guard: a term with no resolvable handle is not rendered */
          .map((t) => (
            <div key={t.name}>
              <StandardButton
                type="button"
                onClick={() => setOpen(open === t.name ? null : t.name)}
              >
                <span>{t.name}</span>
                <span>
                  {money(t.low, impact.currency)} &ndash; {money(t.high, impact.currency)}
                </span>
              </StandardButton>
              {open === t.name ? (
                <div>
                  <div>Inputs: <code>{t.inputsJson}</code></div>
                  <div>
                    Assumptions v{impact.assumptionVersion}
                    {t.handle ? (
                      <>
                        {" · "}
                        <StandardButton
                          type="button"
                          data-testid="value-term-evidence"
                          onClick={() => onOpenEvidence?.(t.handle as ProvenanceHandleRef)}
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

      <p>{CAVEAT}</p>
    </section>
  );
}