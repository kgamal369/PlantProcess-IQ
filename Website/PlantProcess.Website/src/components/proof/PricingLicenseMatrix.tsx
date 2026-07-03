
import { licensePlans } from "../../content/phase1WebsiteProof";

export function PricingLicenseMatrix() {
  return (
    <section className="website-section pricing-section" id="pricing">
      <div className="section-kicker">Pricing and license logic</div>
      <div className="section-heading-row">
        <div>
          <h2>Start where you are. Expand when the value is proven on your data.</h2>
          <p>
            The commercial packaging mirrors the product gating: number of data sources,
            feature depth, users, refresh cadence and enterprise controls. One deposit,
            one subscription, no hidden line items.
          </p>
        </div>
      </div>

      <div className="pricing-grid">
        {licensePlans.map((plan) => (
          <article className={`pricing-card pricing-card--${plan.code}`} key={plan.code}>
            <div className="pricing-card__top">
              <span>
                {plan.name}
                {plan.recommended ? " (Recommended)" : ""}
              </span>
              <strong>{plan.monthlyPrice}</strong>
            </div>

            <p>{plan.idealFor}</p>

            <dl>
              <div>
                <dt>Deposit</dt>
                <dd>{plan.deposit}</dd>
              </div>
              <div>
                <dt>Data sources</dt>
                <dd>{plan.sources}</dd>
              </div>
              <div>
                <dt>Users</dt>
                <dd>{plan.users}</dd>
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
