import type { ReactNode } from "react";
import { useLicense } from "../../state/LicenseContext";
import "./license-components.css";

interface LicenseGateProps {
  feature: string;
  children: ReactNode;
  fallbackTitle?: string;
  fallbackDescription?: string;
}

export function LicenseGate({
  feature,
  children,
  fallbackTitle,
  fallbackDescription,
}: LicenseGateProps) {
  const { isLoading, getFeature } = useLicense();

  const featureStatus = getFeature(feature);

  if (isLoading) {
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
        <strong>{fallbackTitle ?? `${feature} is locked`}</strong>
        <span>
          {fallbackDescription ??
            featureStatus?.message ??
            "This capability requires a higher license tier."}
        </span>
      </section>
    );
  }

  return <>{children}</>;
}