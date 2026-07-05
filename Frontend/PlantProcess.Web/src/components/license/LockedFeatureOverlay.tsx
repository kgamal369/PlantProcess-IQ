import { LockKeyhole } from "lucide-react";

type LockedFeatureOverlayProps = {
  featureName: string;
  requiredPlan: string;
  compact?: boolean;
};

export function LockedFeatureOverlay({
  featureName,
  requiredPlan,
  compact = false,
}: LockedFeatureOverlayProps) {
  return (
    <div
      className={compact ? "locked-feature-inline" : "locked-feature-overlay"}
      role="note"
      aria-label={`${featureName} is locked`}
    >
      <div className="locked-feature-icon">
        <LockKeyhole size={20} />
      </div>

      <div>
        <strong>{featureName} is locked in the current license.</strong>
        <p>
          Available on <strong>{requiredPlan}</strong> and higher plans. The
          active license is resolved from the backend entitlement, never from
          static frontend data.
        </p>
      </div>
    </div>
  );
}
