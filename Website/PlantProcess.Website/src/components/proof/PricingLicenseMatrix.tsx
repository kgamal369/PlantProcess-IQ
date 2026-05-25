import { licensePlans } from "../../content/phase1WebsiteProof";

export function PricingLicenseMatrix() {
  return (
    <section className="website-section pricing-section" id="pricing">
      <div className="section-kicker">Pricing and license logic</div>
      <div className="section-heading-row">
        <div>
          <h2>Start small. Prove the workflow. Expand when the plant data story is real.</h2>
          <p>
            The commercial packaging mirrors the product gating: users, feature
            depth, connector readiness, refresh cadence and analysis allowance.
          </p>
        </div>
      </div>

      <div className="pricing-grid">
        {licensePlans.map((plan) => (
          <article className={`pricing-card pricing-card--${plan.code}`} key={plan.code}>
            <div className="pricing-card__top">
              <span>{plan.name}</span>
              <strong>{plan.monthlyPrice}</strong>
            </div>

            <p>{plan.idealFor}</p>

            <dl>
              <div>
                <dt>Users</dt>
                <dd>{plan.users}</dd>
              </div>
              <div>
                <dt>Analysis allowance</dt>
                <dd>{plan.tokens}</dd>
              </div>
              <div>
                <dt>Refresh</dt>
                <dd>{plan.refresh}</dd>
              </div>
              <div>
                <dt>Connectors</dt>
                <dd>{plan.connectors}</dd>
              </div>
            </dl>

            <ul>
              {plan.features.map((feature) => (
                <li key={feature}>{feature}</li>
              ))}
            </ul>

            <a
              className="website-button website-button--secondary"
              href={`#request-demo`}
            >
              {plan.cta}
            </a>
          </article>
        ))}
      </div>
    </section>
  );
}

export default PricingLicenseMatrix;