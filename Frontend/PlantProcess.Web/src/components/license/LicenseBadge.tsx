import type { LicenseStatus } from "../../api/license";

interface LicenseBadgeProps {
  license: LicenseStatus | null;
}

export function LicenseBadge({ license }: LicenseBadgeProps) {
  if (!license) {
    return (
      <span className="status-badge status-badge--warning">
        License loading
      </span>
    );
  }

  const tierClass =
    license.tier === "Enterprise"
      ? "status-badge--info"
      : license.tier === "ProPlus"
      ? "status-badge--success"
      : license.tier === "Pro"
      ? "status-badge--warning"
      : "status-badge--muted";

  return (
    <span className={`status-badge ${tierClass}`} title={license.displayName}>
      {license.displayName}
    </span>
  );
}