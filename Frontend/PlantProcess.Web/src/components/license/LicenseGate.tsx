import type { ReactNode } from "react";
import { useAuth } from "@/state/AuthContext";
import "./license-components.css";

export interface LicenseGateProps {
  feature?: string;
  permission?: string;
  children: ReactNode;

  /**
   * Backward-compatible props used by existing pages such as CommercialLicensePage.
   */
  fallbackTitle?: string;
  fallbackDescription?: string;

  /**
   * Optional custom fallback.
   */
  fallback?: ReactNode;

  /**
   * hide = remove locked content completely
   * lock = show content with locked overlay
   */
  mode?: "hide" | "lock";
}

export function LicenseGate({
  feature,
  permission,
  children,
  fallbackTitle,
  fallbackDescription,
  fallback,
  mode = "lock",
}: LicenseGateProps) {
  const { user } = useAuth();
  const entitlements = user?.entitlements;

  const hasFeature = !feature || !!entitlements?.features?.includes(feature);
  const hasPermission =
    !permission || !!entitlements?.permissions?.includes(permission);

  const allowed = hasFeature && hasPermission;

  if (allowed) return <>{children}</>;

  const reason =
    fallbackDescription ||
    (feature && entitlements?.lockReasons?.[feature]) ||
    (permission && `Requires permission: ${permission}`) ||
    "This action is locked for your current role or license.";

  const title =
    fallbackTitle ||
    (feature ? `Feature locked: ${feature}` : "Locked feature");

  if (mode === "hide") {
    if (fallback) return <>{fallback}</>;

    return (
      <div className="license-locked-inline" aria-disabled="true">
        <div className="license-locked-inline__overlay">
          <span>{title}</span>
          <small>{reason}</small>
        </div>
      </div>
    );
  }

  if (fallback) return <>{fallback}</>;

  return (
    <div
      className="license-locked-inline"
      title={reason}
      aria-disabled="true"
      data-locked-feature={feature ?? ""}
      data-required-permission={permission ?? ""}
    >
      <div className="license-locked-inline__content">{children}</div>
      <div className="license-locked-inline__overlay">
        <span>{title}</span>
        <small>{reason}</small>
      </div>
    </div>
  );
}
