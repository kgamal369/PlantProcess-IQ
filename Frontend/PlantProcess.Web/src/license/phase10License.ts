export type Phase10Tier = "Lite" | "Pro" | "ProPlus" | "Enterprise";

export type Phase10FeatureCode =
  | "DashboardRead"
  | "AdvancedDashboardBuilder"
  | "CorrelationScheduledRun"
  | "MlLearningJobs"
  | "EnterpriseConnectors"
  | "OfflineActivation";

export type Phase10FeatureStatus = {
  code: Phase10FeatureCode;
  label: string;
  requiredTier: Phase10Tier;
  enabled: boolean;
  upgradePath: string;
};

const tierRank: Record<Phase10Tier, number> = {
  Lite: 1,
  Pro: 2,
  ProPlus: 3,
  Enterprise: 4,
};

export const phase10TierVisuals: Record<Phase10Tier, {
  label: string;
  className: string;
  accentName: string;
  description: string;
}> = {
  Lite: {
    label: "Lite",
    className: "tier-badge tier-badge--lite",
    accentName: "Muted Steel",
    description: "Read-only operational visibility with limited configuration.",
  },
  Pro: {
    label: "Pro",
    className: "tier-badge tier-badge--pro",
    accentName: "Amber",
    description: "Professional analytics and controlled configuration.",
  },
  ProPlus: {
    label: "Pro Plus",
    className: "tier-badge tier-badge--proplus",
    accentName: "Electric Blue",
    description: "Advanced AI/ML and premium intelligence workflows.",
  },
  Enterprise: {
    label: "Enterprise",
    className: "tier-badge tier-badge--enterprise",
    accentName: "Cyan Green",
    description: "Full enterprise scope including offline activation and enterprise connectors.",
  },
};

export const phase10FeatureCatalog: Array<Omit<Phase10FeatureStatus, "enabled" | "upgradePath">> = [
  { code: "DashboardRead", label: "Existing dashboard read access", requiredTier: "Lite" },
  { code: "AdvancedDashboardBuilder", label: "Advanced dashboard builder", requiredTier: "Pro" },
  { code: "CorrelationScheduledRun", label: "Scheduled correlation runs", requiredTier: "Pro" },
  { code: "MlLearningJobs", label: "ML learning jobs", requiredTier: "ProPlus" },
  { code: "EnterpriseConnectors", label: "Enterprise-only connectors", requiredTier: "Enterprise" },
  { code: "OfflineActivation", label: "Offline signed-license activation", requiredTier: "Enterprise" },
];

export function canUseTierFeature(activeTier: Phase10Tier, requiredTier: Phase10Tier) {
  return tierRank[activeTier] >= tierRank[requiredTier];
}

export function upgradePathFor(activeTier: Phase10Tier, requiredTier: Phase10Tier) {
  if (canUseTierFeature(activeTier, requiredTier)) {
    return "Included in current tier";
  }

  return `Upgrade from ${phase10TierVisuals[activeTier].label} to ${phase10TierVisuals[requiredTier].label}`;
}

export function buildPhase10FeatureMatrix(activeTier: Phase10Tier): Phase10FeatureStatus[] {
  return phase10FeatureCatalog.map((feature) => ({
    ...feature,
    enabled: canUseTierFeature(activeTier, feature.requiredTier),
    upgradePath: upgradePathFor(activeTier, feature.requiredTier),
  }));
}

export function liveTierToggleResult(fromTier: Phase10Tier, toTier: Phase10Tier) {
  const before = buildPhase10FeatureMatrix(fromTier);
  const after = buildPhase10FeatureMatrix(toTier);

  const hiddenNow = before
    .filter((item) => item.enabled)
    .filter((item) => !after.find((next) => next.code === item.code)?.enabled)
    .map((item) => item.code);

  const restoredNow = before
    .filter((item) => !item.enabled)
    .filter((item) => after.find((next) => next.code === item.code)?.enabled)
    .map((item) => item.code);

  return {
    fromTier,
    toTier,
    hiddenNow,
    restoredNow,
    immediate: true,
    verifiedBy: "PPIQ_REALIZATION_T058_LIVE_TIER_TOGGLE_DEMO",
  };
}

export function phase10LifecycleBadge(state: string) {
  if (/expired/i.test(state)) return "Expired read-only";
  if (/grace/i.test(state)) return "Grace read-only";
  if (/overage/i.test(state)) return "Overage read-only";
  if (/warning/i.test(state)) return "Pre-expiry warning";
  return "Active";
}