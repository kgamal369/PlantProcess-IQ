import { LockKeyhole } from "lucide-react";

import type { DemoLicensePlan } from "@/demo/plantProcessDemoScenario";

type LockedFeatureOverlayProps = {
  featureName: string;
  requiredPlan: DemoLicensePlan | "Pro Plus";
  compact?: boolean;
};

export function LockedFeatureOverlay({
  featureName,
  requiredPlan,
  compact = false,
}: LockedFeatureOverlayProps) {
  return (
    <div
      className={
        compact ? "locked-feature-inline" : "locked-feature-overlay"
      }
      role="note"
      aria-label={`${featureName} is locked`}
    >
      <div className="locked-feature-icon">
        <LockKeyhole size={20} />
      </div>

      <div>
        <strong>{featureName} is locked in the current license.</strong>
        <p>
          This workflow is visible for demo/storytelling, but it should be
          unlocked only for <strong>{requiredPlan}</strong> or higher.
        </p>
      </div>
    </div>
  );
}