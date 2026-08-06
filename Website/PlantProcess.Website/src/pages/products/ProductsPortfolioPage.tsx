/* PPIQ-T069-05 */
import { Link } from "react-router-dom";
import {
  souProducts,
  productPath,
  stackLayers,
  type PortfolioProduct,
} from "../../content/portfolio/souProducts";

/**
 * The /products portfolio. Chapter 6 6.2.15 calls this a major sales page rather
 * than a directory, so each product carries the six fields that section names.
 *
 * IT USES THE HERO'S DESIGN SYSTEM AND NOTHING ELSE. Every colour, font, radius
 * and motion value is a token new-landing.css already defines, and the stack
 * graphic is drawn with the hero's own SVG classes - srcbox, srct, srcs, spoke,
 * spoke-flow, ring2, hub-core. No keyframe, palette or type scale is invented
 * here, so this page cannot drift into a second visual language.
 */

const LAYER_ROW_H = 96;
const BAND_X = 18;
const BAND_W = 604;

function StackGraphic() {
  const rows = stackLayers.map((layer) => ({
    layer,
    items: souProducts.filter((product) => product.stackLayer === layer),
  }));
  const height = rows.length * LAYER_ROW_H + 24;

  return (
    <svg
      viewBox={`0 0 640 ${height}`}
      className="pf-stack"
      role="img"
      aria-label="The SOU stack: plant intelligence above plant execution, material flow and resource efficiency, with five products placed in their layer."
    >
      <line className="ring2 pf-spine" x1="34" y1="16" x2="34" y2={height - 20} />
      {rows.map((row, index) => {
        const y = index * LAYER_ROW_H + 16;
        return (
          <g key={row.layer}>
            <rect className="pf-band" x={BAND_X} y={y} width={BAND_W} height={LAYER_ROW_H - 18} rx="12" />
            <text className="srcs pf-band-label" x={BAND_X + 22} y={y + 22}>
              {row.layer.toUpperCase()}
            </text>
            {row.items.map((product, itemIndex) => {
              const x = BAND_X + 22 + itemIndex * 196;
              const nodeY = y + 34;
              return (
                <g key={product.slug}>
                  <path
                    className="spoke"
                    d={`M34 ${nodeY + 20} H ${x}`}
                    fill="none"
                  />
                  <path
                    data-draw
                    className={`spoke-flow${itemIndex === 0 ? "" : " s3"}`}
                    d={`M34 ${nodeY + 20} H ${x}`}
                    fill="none"
                  />
                  <g className={product.isFlagship ? "hub-core" : ""}>
                    <rect
                      className={product.isFlagship ? "srcbox pf-node pf-node--flag" : "srcbox pf-node"}
                      x={x}
                      y={nodeY}
                      width="182"
                      height="40"
                      rx="8"
                    />
                    <text className="srct" x={x + 16} y={nodeY + 25}>
                      {product.menuLabel}
                    </text>
                  </g>
                </g>
              );
            })}
          </g>
        );
      })}
    </svg>
  );
}

function ProductCard({ product }: { product: PortfolioProduct }) {
  return (
    <article className={product.isFlagship ? "pf-card pf-card--flag rv" : "pf-card rv"}>
      <p className="pf-value">{product.stackLayer}</p>
      {product.isFlagship ? <span className="pf-flag">FLAGSHIP</span> : null}
      <h3>{product.name}</h3>
      <p className="pf-tag">{product.valueLine}</p>

      <p className="pf-row"><b>The problem it owns.</b> {product.problemOwned}</p>
      <p className="pf-row"><b>Typical buyer.</b> {product.typicalBuyer}</p>
      <p className="pf-row"><b>Where it sits.</b> {product.operationalPosition}</p>

      <ul className="pf-benefits">
        {product.primaryBenefits.map((benefit) => (
          <li key={benefit}>{benefit}</li>
        ))}
      </ul>

      <p className="pf-row pf-independent"><b>On its own.</b> {product.independence}</p>

      <Link className="pf-more" to={productPath(product)}>
        Explore {product.menuLabel}
      </Link>
    </article>
  );
}

export function ProductsPortfolioPage() {
  return (
    <div className="new-landing-wrapper">
      <section className="section pf-top">
        <div className="wrap">
          <div className="pf-hero">
            <div>
              <p className="eyebrow rv">SOU INDUSTRIAL SOFTWARE</p>
              <h2 className="rv pf-title">
                Five products.<br />
                <span className="g">One plant.</span>
              </h2>
              <p className="pf-lead rv">
                PlantProcess IQ is our flagship. It is not a container around the other four -
                each of these is bought, deployed and run in its own right, and each owns a
                different problem on the floor.
              </p>
              <div className="inds rv">
                {stackLayers.map((layer) => (
                  <div className="ind" key={layer}>{layer}</div>
                ))}
              </div>
            </div>
            <div className="pf-graphic rv">
              <StackGraphic />
            </div>
          </div>
        </div>
      </section>

      <div className="band rv">
        <div className="wrap">
          <div className="cell">
            <div className="big">Bought separately</div>
            <div className="s">Each product stands on its own. None of them is a module inside another.</div>
          </div>
          <div className="cell">
            <div className="big">One plant model</div>
            <div className="s">Where two are deployed together they describe the same unit of production.</div>
          </div>
          <div className="cell">
            <div className="big">Read-only where it matters</div>
            <div className="s">PlantProcess IQ observes the systems you already run and never commands them.</div>
          </div>
        </div>
      </div>

      <section className="section">
        <div className="wrap">
          <p className="eyebrow rv">THE PORTFOLIO</p>
          <h2 className="rv">Which one do you need?</h2>
          <p className="pf-lead rv">
            Start from the problem, not the feature list. Each card names the problem that
            product owns and who usually buys it.
          </p>
          <div className="pf-grid">
            {souProducts.map((product) => (
              <ProductCard key={product.slug} product={product} />
            ))}
          </div>
        </div>
      </section>
    </div>
  );
}