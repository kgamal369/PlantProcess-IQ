import { websiteConnectors } from "../../content/phase1WebsiteProof";

function statusClass(status: string) {
  return `connector-status connector-status--${status}`;
}

export function ConnectorHonestyBlock() {
  return (
    <section className="website-section connector-honesty-section" id="connectors">
      <div className="section-kicker">Connector status honesty</div>

      <div className="section-heading-row">
        <div>
          <h2>Connector truth is part of the product, not sales decoration.</h2>
          <p>
            PlantProcess IQ separates available connectors, implemented-but-not-certified
            providers, planned connectors, and source-shaped demo schemas.
          </p>
        </div>
      </div>

      <div className="connector-honesty-table">
        {websiteConnectors.map((connector) => (
          <article key={connector.provider}>
            <div>
              <strong>{connector.label}</strong>
              <span className={statusClass(connector.status)}>
                {connector.frontendLabel}
              </span>
            </div>
            <p>{connector.note}</p>
          </article>
        ))}
      </div>
    </section>
  );
}

export default ConnectorHonestyBlock;