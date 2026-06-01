import type { ReactNode } from "react";
import { LockedFeatureOverlay } from "@/components/demo/LockedFeatureOverlay";
import { useDemoMode } from "@/state/DemoModeContext";
import { useLicense } from "@/state/LicenseContext";
import type { DemoLicensePlan } from "@/demo/plantProcessDemoScenario";
import "./license-components.css";

type DemoPlanGateProps = {
  featureName: string;
  requiredPlan: DemoLicensePlan | "Pro Plus";
  children: ReactNode;
  compact?: boolean;
};

type BackendFeatureGateProps = {
  feature: string;
  children: ReactNode;
  fallbackTitle?: string;
  fallbackDescription?: string;
};

type LicenseGateProps = DemoPlanGateProps | BackendFeatureGateProps;

function normalizePlan(plan: DemoLicensePlan | "Pro Plus") {
  return plan === "Pro Plus" ? "ProPlus" : plan;
}

function isBackendFeatureGate(
  props: LicenseGateProps
): props is BackendFeatureGateProps {
  return "feature" in props && typeof props.feature === "string";
}

export function LicenseGate(props: LicenseGateProps) {
  const demoMode = useDemoMode();
  const license = useLicense();

  if (isBackendFeatureGate(props)) {
    const featureStatus = license.getFeature(props.feature);

    if (license.isLoading) {
      return (
        <section className="license-gate-card license-gate-card--loading">
          <strong>Checking license...</strong>
          <span>Loading commercial feature gates from backend.</span>
        </section>
      );
    }

    if (!featureStatus?.isEnabled) {
      return (
        <section className="license-gate-card license-gate-card--locked">
          <strong>{props.fallbackTitle ?? `${props.feature} is locked`}</strong>
          <span>
            {props.fallbackDescription ??
              featureStatus?.message ??
              "This capability requires a higher license tier."}
          </span>
        </section>
      );
    }

    return <>{props.children}</>;
  }

  const unlocked = demoMode.isFeatureAvailable(
    normalizePlan(props.requiredPlan)
  );

  if (!unlocked) {
    return (
      <LockedFeatureOverlay
        featureName={props.featureName}
        requiredPlan={props.requiredPlan}
        compact={props.compact ?? false}
      />
    );
  }

  return <>{props.children}</>;
}