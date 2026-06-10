import { useMemo, useState } from "react";
import {
  buildPhase10FeatureMatrix, liveTierToggleResult, phase10LifecycleBadge, phase10TierVisuals, type Phase10Tier, } from "@/license/phase10License";
import "./phase10-license.css";

import { StandardP2Button } from "@/components/standard/StandardP2Controls";
import { StandardButton } from "@/components/standard";
const tiers: Phase10Tier[] = ["Lite", "Pro", "ProPlus", "Enterprise"];

export function Phase10LicenseDemoPage() {
  const [tier, setTier] = useState<Phase10Tier>("Enterprise");

  const featureMatrix = useMemo(() => buildPhase10FeatureMatrix(tier), [tier]);
  const proToggle = useMemo(() => liveTierToggleResult("Enterprise", "Pro"), []);
  const enterpriseToggle = useMemo(() => liveTierToggleResult("Pro", "Enterprise"), []);

  const visual = phase10TierVisuals[tier];

  return (
    <main className="phase10-license-page" data-testid="phase10-license-demo">
      <section className="phase10-hero">
        <p className="phase10-eyebrow">P10 · T-058/T-062 · Live tier-toggle demo</p>
        <h1>Phase 10 License Runtime Demo</h1>
        <p className="phase10-copy">
          Switching the active tier in the HMI updates feature visibility immediately. Existing dashboard read access remains protected during grace, expiry and overage flows.
        </p>
        <span className={visual.className}>{visual.label} · {visual.accentName}</span>
      </section>

      <section className="phase10-panel">
        <p className="phase10-eyebrow">Live tier toggle</p>
        <div className="phase10-toggle-row">
          {tiers.map((item) => (
            <StandardButton
              key={item}
              type="button"
              data-active={tier === item}
              onClick={() => setTier(item)}
            >
              {phase10TierVisuals[item].label}
            </StandardButton>
          ))}
        </div>
        <p className="phase10-copy">
          Enterprise to Pro hides: {proToggle.hiddenNow.join(", ")}. Pro to Enterprise restores: {enterpriseToggle.restoredNow.join(", ")}.
        </p>
      </section>

      <section className="phase10-grid">
        {featureMatrix.map((feature) => (
          <article className="phase10-card" key={feature.code}>
            <span className={feature.enabled ? "phase10-feature-enabled" : "phase10-feature-disabled"}>
              {feature.enabled ? "Enabled" : "Disabled"}
            </span>
            <h2>{feature.label}</h2>
            <p className="phase10-copy">
              Required tier: {phase10TierVisuals[feature.requiredTier].label}
            </p>
            <strong>{feature.upgradePath}</strong>
          </article>
        ))}
      </section>

      <section className="phase10-grid">
        <article className="phase10-card">
          <p className="phase10-eyebrow">T-059 Grace / expiry / overage</p>
          <h2>{phase10LifecycleBadge("GraceReadOnly")}</h2>
          <p className="phase10-copy">
            Expired or over-limit licenses become read-only for existing dashboards. Customer data is never deleted by the license flow.
          </p>
        </article>

        <article className="phase10-card">
          <p className="phase10-eyebrow">T-061 Offline activation</p>
          <h2>Ed25519 signed-license envelope</h2>
          <p className="phase10-copy">
            Air-gapped activation is certified by a signed envelope contract. Tampered payloads are rejected and audited.
          </p>
        </article>
      </section>
    </main>
  );
}

export default Phase10LicenseDemoPage;