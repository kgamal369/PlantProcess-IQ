/* PPIQ-PHASE6 entitlement gating. Feed EntitlementsProvider from your VERIFIED
 * Ed25519 license entitlements (entitlementSource) - never from an editable row.
 * Use <EntitlementGate need="..."> to show/hide features; <TierBadge/> shows caps. */
import React, { createContext, useContext } from "react";
import type { Entitlements } from "../../types/execOpsContracts";

const DEFAULT: Entitlements = { verified: false, tier: "none", features: [], seats: 0, sources: 0 };
const Ctx = createContext<Entitlements>(DEFAULT);

export function EntitlementsProvider({ value, children }: { value: Entitlements; children: React.ReactNode }) {
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useEntitlements() {
  const e = useContext(Ctx);
  return {
    ...e,
    has: (key: string) => e.verified && e.features.includes(key),
  };
}

export function EntitlementGate({
  need,
  children,
  fallback = null,
}: {
  need: string;
  children: React.ReactNode;
  fallback?: React.ReactNode;
}) {
  const { has } = useEntitlements();
  const granted = has(need);
  return (
    <div data-testid="entitlement-gate" data-need={need} data-granted={granted ? "true" : "false"}>
      {granted ? children : fallback}
    </div>
  );
}

export function TierBadge() {
  const e = useEntitlements();
  return (
    <span
      data-testid="tier-badge"
      data-tier={e.tier}
      data-verified={e.verified ? "true" : "false"}
      style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 12, color: e.verified ? "#2CE6A2" : "#FF4D6D", border: "1px solid #16243D", borderRadius: 6, padding: "2px 8px", background: "#050B18" }}
    >
      {e.tier.toUpperCase()}
      {typeof e.sources === "number" ? ` - sources ${e.sources}` : ""}
      {typeof e.seats === "number" ? ` - seats ${e.seats}` : ""}
      {e.verified ? "" : " (UNVERIFIED)"}
    </span>
  );
}