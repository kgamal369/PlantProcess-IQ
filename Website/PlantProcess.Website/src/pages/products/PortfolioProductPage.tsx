/* PPIQ-T069-03 */
import { Link } from "react-router-dom";
import { findProductBySlug, productPath, souProducts } from "../../content/portfolio/souProducts";

/**
 * A single standalone product. This is a SHELL, deliberately not the twenty
 * section model of Chapter 6 6.2.13 - that belongs to the later Website parts
 * and absorbing it here would widen T-069 past its frozen text.
 *
 * PlantProcess IQ never reaches this component. Its canonical route renders the
 * existing PlatformPage, because route equality does not require renderer
 * equality and the richer PPIQ page must not be traded for a generic one.
 *
 * Wording follows the 6.2.10 honesty rule by construction: everything shown here
 * comes from the registry, where the four non-flagship products carry
 * claimBasis "target-design" and are written as what the product does by design.
 */
export function PortfolioProductPage({ slug }: { slug: string }) {
  const product = findProductBySlug(slug);
  if (!product) {
    return (
      <div className="new-landing-wrapper">
        <section className="section">
          <div className="wrap">
            <h2>That product is not in the portfolio.</h2>
            <p><Link to="/products">See all five products</Link></p>
          </div>
        </section>
      </div>
    );
  }

  const siblings = souProducts.filter((other) => other.slug !== product.slug);

  return (
    <div className="new-landing-wrapper">
      <section className="section">
        <div className="wrap">
          <p className="eyebrow">{product.stackLayer}</p>
          <h2>{product.name}</h2>
          <p>{product.valueLine}</p>

          <p><strong>The problem it owns.</strong> {product.problemOwned}</p>
          <p><strong>Typical buyer.</strong> {product.typicalBuyer}</p>
          <p><strong>Where it sits.</strong> {product.operationalPosition}</p>

          <ul>
            {product.primaryBenefits.map((benefit) => (
              <li key={benefit}>{benefit}</li>
            ))}
          </ul>

          <p><strong>On its own.</strong> {product.independence}</p>

          <ul>
            {product.relationships.map((rel) => (
              <li key={rel.slug}>{rel.statement}</li>
            ))}
          </ul>

          <p className="eyebrow">THE REST OF THE PORTFOLIO</p>
          <div className="inds">
            {siblings.map((other) => (
              <Link className="ind" key={other.slug} to={productPath(other)}>
                {other.menuLabel}
              </Link>
            ))}
          </div>
        </div>
      </section>
    </div>
  );
}