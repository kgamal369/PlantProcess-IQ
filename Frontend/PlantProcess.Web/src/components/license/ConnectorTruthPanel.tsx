import type { DemoConnectorStatus } from "../../api/demo";
import { useLicense } from "../../state/LicenseContext";
import "./license-components.css";

const fallbackConnectors: DemoConnectorStatus[] = [
  {
    providerType: "Csv",
    displayName: "CSV Snapshot",
    implementationStatus: "ImplementedAndTested",
    licenseStatus: "AllowedByCurrentLicense",
    isAvailableNow: true,
    isAllowedByLicense: true,
    isSafeForDemo: true,
    message: "Safe to show in controlled demo.",
  },
  {
    providerType: "Excel",
    displayName: "Excel Snapshot",
    implementationStatus: "InternalImplementationRequiresEndToEndProof",
    licenseStatus: "AllowedByCurrentLicense",
    isAvailableNow: false,
    isAllowedByLicense: true,
    isSafeForDemo: false,
    message: "Show as planned unless end-to-end proof passes.",
  },
  {
    providerType: "PostgreSql",
    displayName: "PostgreSQL Read-only DB Link",
    implementationStatus: "ImplementedRequiresEnvironmentProof",
    licenseStatus: "RequiresPro",
    isAvailableNow: true,
    isAllowedByLicense: false,
    isSafeForDemo: false,
    message: "Safe only when license and environment proof are green.",
  },
  {
    providerType: "SqlServer",
    displayName: "Microsoft SQL Server",
    implementationStatus: "Planned",
    licenseStatus: "RequiresEnterprise",
    isAvailableNow: false,
    isAllowedByLicense: false,
    isSafeForDemo: false,
    message: "Do not show as available.",
  },
  {
    providerType: "Oracle",
    displayName: "Oracle",
    implementationStatus: "Planned",
    licenseStatus: "RequiresEnterprise",
    isAvailableNow: false,
    isAllowedByLicense: false,
    isSafeForDemo: false,
    message: "Do not show as available.",
  },
  {
    providerType: "MySql",
    displayName: "MySQL",
    implementationStatus: "Planned",
    licenseStatus: "RequiresEnterprise",
    isAvailableNow: false,
    isAllowedByLicense: false,
    isSafeForDemo: false,
    message: "Do not show as available.",
  },
];

export function ConnectorTruthPanel({
  connectors,
}: {
  connectors?: DemoConnectorStatus[];
}) {
  const { license } = useLicense();
  const rows = connectors?.length ? connectors : fallbackConnectors;

  return (
    <section className="license-panel">
      <div className="license-panel-header">
        <div>
          <p className="license-eyebrow">Dimension 8</p>
          <h2>Connector Truth Matrix</h2>
          <p>
            A connector is demo-safe only when implementation, tests, backend
            registration, frontend label, website label, and license tier all
            agree.
          </p>
        </div>
        <strong className="license-pill">{license?.tier ?? "Unknown"}</strong>
      </div>

      <div className="connector-truth-grid">
        <div className="connector-truth-head">
          <span>Connector</span>
          <span>Implementation</span>
          <span>License</span>
          <span>Demo safe</span>
        </div>

        {rows.map((connector) => (
          <div className="connector-truth-row" key={connector.providerType}>
            <strong>{connector.displayName}</strong>
            <span>{connector.implementationStatus}</span>
            <span>{connector.licenseStatus}</span>
            <span
              className={
                connector.isSafeForDemo
                  ? "ready-chip compact"
                  : "warning-chip compact"
              }
            >
              {connector.isSafeForDemo ? "Safe" : "Not safe"}
            </span>
          </div>
        ))}
      </div>
    </section>
  );
}