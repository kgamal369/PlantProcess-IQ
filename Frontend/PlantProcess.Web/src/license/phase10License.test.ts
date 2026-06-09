import { describe, expect, it } from "vitest";
import {
  buildPhase10FeatureMatrix,
  liveTierToggleResult,
  phase10LifecycleBadge,
  phase10TierVisuals,
  upgradePathFor,
} from "./phase10License";

describe("Phase 10 license tier toggle and badges", () => {
  it("T-058 hides Enterprise-only features immediately when toggling Enterprise to Pro", () => {
    const result = liveTierToggleResult("Enterprise", "Pro");

    expect(result.immediate).toBe(true);
    expect(result.hiddenNow).toContain("EnterpriseConnectors");
    expect(result.hiddenNow).toContain("OfflineActivation");
  });

  it("T-058 restores Enterprise-only features when toggling back", () => {
    const result = liveTierToggleResult("Pro", "Enterprise");

    expect(result.restoredNow).toContain("EnterpriseConnectors");
    expect(result.restoredNow).toContain("OfflineActivation");
  });

  it("T-060 maps every tier to the requested badge color token", () => {
    expect(phase10TierVisuals.Lite.accentName).toBe("Muted Steel");
    expect(phase10TierVisuals.Pro.accentName).toBe("Amber");
    expect(phase10TierVisuals.ProPlus.accentName).toBe("Electric Blue");
    expect(phase10TierVisuals.Enterprise.accentName).toBe("Cyan Green");
  });

  it("T-060 returns upgrade CTA for disabled features", () => {
    expect(upgradePathFor("Lite", "ProPlus")).toMatch(/Upgrade from Lite to Pro Plus/i);
  });

  it("T-060 marks disabled features with upgrade paths", () => {
    const matrix = buildPhase10FeatureMatrix("Lite");
    const ml = matrix.find((x) => x.code === "MlLearningJobs");

    expect(ml?.enabled).toBe(false);
    expect(ml?.upgradePath).toMatch(/Pro Plus/i);
  });

  it("T-059 labels read-only lifecycle states", () => {
    expect(phase10LifecycleBadge("GraceReadOnly")).toBe("Grace read-only");
    expect(phase10LifecycleBadge("ExpiredReadOnly")).toBe("Expired read-only");
    expect(phase10LifecycleBadge("OverageReadOnly")).toBe("Overage read-only");
  });
});