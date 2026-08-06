/* PPIQ-T069-03 */
import { Link } from "react-router-dom";
import { souProducts, productPath, stackLayers } from "../../content/portfolio/souProducts";

/**
 * The /products portfolio. Chapter 6 6.2.15 calls this a major sales page rather
 * than a directory, so every product carries the six fields that section names:
 * the problem it owns, its typical buyer, its operational position, its primary
 * benefits, what it does independently, and its relationship with the others.
 *
 * STRUCTURAL ONLY. It uses nothing but classes the site already defines. The
 * visual pass is a separate pack, gated on the homepage quality verdict, so this
 * page cannot introduce a second visual language before that gate clears.
 */
export function ProductsPortfolioPage() {
  return (
    <div className="new-landing-wrapper">
      <section className="section">
        <div className="wrap">
          <p className="eyebrow">SOU INDUSTRIAL SOFTWARE</p>
          <h2>Five products. One plant.</h2>
          <p>
            PlantProcess IQ is our flagship. It is not a container around the other four - each of
            these is bought, deployed and run in its own right.
          </p>

          <div className="inds">
            {stackLayers.map((layer) => (
              <div className="ind" key={layer}>{layer}</div>
            ))}
          </div>

          {souProducts.map((product) => (
            <section className="section" key={product.slug}>
              <p className="eyebrow">{product.stackLayer}</p>
              <h2>
                <Link to={productPath(product)}>{product.name}</Link>
              </h2>
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
            </section>
          ))}
        </div>
      </section>
    </div>
  );
}