import type { LicenseFeatureStatus } from "../../api/license";

interface FeatureGateHintProps {
  feature?: LicenseFeatureStatus;
  fallback?: string;
}

export function FeatureGateHint({ feature, fallback }: FeatureGateHintProps) {
  if (!feature) {
    return (
      <div className="locked-feature-card">
        <strong>Feature gate unavailable</strong>
        <span>{fallback ?? "This feature requires a higher license tier."}</span>
      </div>
    );
  }

  if (feature.isEnabled) {
    return (
      <div className="feature-available-card">
        <strong>{feature.feature}</strong>
        <span>{feature.message}</span>
      </div>
    );
  }

  return (
    <div className="locked-feature-card">
      <strong>{feature.feature} is locked</strong>
      <span>{feature.message}</span>
    </div>
  );
}