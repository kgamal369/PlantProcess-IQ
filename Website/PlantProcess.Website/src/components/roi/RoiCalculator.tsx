import { useMemo, useState } from "react";

/**
 * Interactive ROI model - the visitor's own math, never our benchmark.
 * We compute the value of the yield recovery THEY choose to model, on THEIR
 * tonnage and THEIR margin, and label it a directional estimate. The CTA turns
 * the result into a conversation.
 */
const fmt = (n: number) =>
  new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "EUR",
    maximumFractionDigits: 0,
  }).format(n);

export function RoiCalculator({ demoHref = "#request-demo" }: { demoHref?: string }) {
  const [tonnage, setTonnage] = useState(1_000_000);
  const [margin, setMargin] = useState(80);
  const [recovery, setRecovery] = useState(1.0);

  const annual = useMemo(
    () => tonnage * (recovery / 100) * margin,
    [tonnage, margin, recovery]
  );
  const perMonth = annual / 12;

  return (
    <section className="ppiq-roi" aria-labelledby="roi-h2">
      <p className="roi-eyebrow">WHAT IS 1% WORTH IN YOUR PLANT?</p>
      <h2 id="roi-h2">Model it with your own numbers</h2>

      <div className="roi-grid">
        <div className="roi-inputs">
          <label>
            <span className="roi-label">Annual production</span>
            <span className="roi-value">{tonnage.toLocaleString("en-US")} t</span>
            <input
              type="range" min={50_000} max={5_000_000} step={50_000}
              value={tonnage}
              onChange={(e) => setTonnage(Number(e.target.value))}
              aria-label="Annual production in tonnes"
            />
          </label>
          <label>
            <span className="roi-label">Contribution margin</span>
            <span className="roi-value">{margin} &euro; / t</span>
            <input
              type="range" min={20} max={400} step={5}
              value={margin}
              onChange={(e) => setMargin(Number(e.target.value))}
              aria-label="Contribution margin in euros per tonne"
            />
          </label>
          <label>
            <span className="roi-label">Prime-yield recovery you model</span>
            <span className="roi-value">{recovery.toFixed(1)} %</span>
            <input
              type="range" min={0.2} max={3} step={0.1}
              value={recovery}
              onChange={(e) => setRecovery(Number(e.target.value))}
              aria-label="Modelled prime yield recovery in percent"
            />
          </label>
        </div>

        <div className="roi-result" role="status" aria-live="polite">
          <div className="roi-headline">{fmt(annual)}</div>
          <div className="roi-sub">per year &middot; {fmt(perMonth)} per month</div>
          <p className="roi-explain">
            {recovery.toFixed(1)}% of {tonnage.toLocaleString("en-US")} t moved from
            downgrade or scrap back to prime, at {margin}&nbsp;&euro;/t contribution.
          </p>
          <a className="roi-cta" href={demoHref}>
            Discuss these numbers with us
          </a>
          <p className="roi-disclaimer">
            Directional estimate from your inputs - not a guarantee. A pilot on one
            line is how we validate the recoverable share together.
          </p>
        </div>
      </div>
    </section>
  );
}