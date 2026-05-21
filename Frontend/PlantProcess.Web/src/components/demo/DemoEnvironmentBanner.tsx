import { useLicense } from "../../state/LicenseContext";
import "./demo-environment-banner.css";

export function DemoEnvironmentBanner() {
  const { license } = useLicense();

  return (
    <aside className="demo-environment-banner">
      <strong>Demo environment</strong>
      <span>
        Synthetic / controlled manufacturing data · Read-only intelligence
        layer · Active tier: {license?.displayName ?? "loading"}
      </span>
      <span>
        No MES, SCADA, L2, PLC, or BI replacement. No guaranteed root cause. No
        production-ready ML model active.
      </span>
    </aside>
  );
}