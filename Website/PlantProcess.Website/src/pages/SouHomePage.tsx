/* PPIQ-T070-05 */
import { useEffect } from "react";
import { Link } from "react-router-dom";
import { BrainCircuit, Factory, ScanLine, Layers3, Gauge, ArrowRight } from "lucide-react";
import {
  souProducts,
  productPath,
  type PortfolioProduct,
} from "../content/portfolio/souProducts";
import "../styles/new-landing.css";
import "../styles/motion-roi.css";

/**
 * The SOU Industrial Software company home.
 *
 * IT IS NOT A PRODUCT PAGE. The full PlantProcess IQ narrative lives at
 * /products/plantprocess-iq, where it belongs - the ROI calculator, the
 * architecture flow and the genealogy thread are PPIQ capabilities, not company
 * claims, and MES or Yard do not share them.
 *
 * NO SIXTH PRODUCT IS DRAWN. The graphic reads plant signals -> the five
 * products -> smarter operations, with each product on its own node. There is
 * deliberately no central core for them to hang off, because none exists and
 * drawing one would turn four standalone products into accessories of a fifth.
 *
 * Everything here uses the established design system: the hero type scale, the
 * .wrap grid, the band, the srcbox/srct/spoke/spoke-flow SVG vocabulary and the
 * .pf-* card language. Nothing new is invented.
 */

const ICONS: Record<string, typeof Factory> = {
  BrainCircuit,
  Factory,
  ScanLine,
  Layers3,
  Gauge,
};

const SIGNALS = ["Process & automation", "Quality & lab", "Material & logistics", "Energy & utilities"];
const OUTCOMES = ["Fewer defects", "Less downtime", "Higher throughput", "Lower energy per unit"];

function PortfolioGraphic() {
  const colX = 96;
  const midX = 560;
  const outX = 1024;
  const rowY = (i: number, n: number) => 70 + i * (380 / Math.max(n, 1));

  return (
    <svg
      viewBox="0 0 1120 460"
      className="sou-graphic"
      role="img"
      aria-label="Plant signals flow into the five SOU products, which return smarter operations and better outcomes."
    >
      <text className="srcs sou-col" x={colX} y="34" textAnchor="middle">PLANT SIGNALS</text>
      <text className="srcs sou-col" x={midX} y="34" textAnchor="middle">SOU SOFTWARE PORTFOLIO</text>
      <text className="srcs sou-col" x={outX} y="34" textAnchor="middle">SMARTER OPERATIONS</text>

      {SIGNALS.map((label, i) => {
        const y = rowY(i, SIGNALS.length) + 40;
        return (
          <g key={label}>
            <rect className="srcbox" x={colX - 92} y={y - 18} width="184" height="38" rx="8" />
            <text className="srct" x={colX} y={y + 6} textAnchor="middle">{label}</text>
            <circle className="af-port" cx={colX + 92} cy={y} r="3" />
            <path className="spoke" d={`M${colX + 92} ${y} C 300 ${y}, 340 230, ${midX - 116} 230`} fill="none" />
            <path data-draw className={`spoke-flow${i === 0 ? "" : ` s${i + 1}`}`} d={`M${colX + 92} ${y} C 300 ${y}, 340 230, ${midX - 116} 230`} fill="none" />
          </g>
        );
      })}

      {souProducts.map((product, i) => {
        const y = rowY(i, souProducts.length) + 26;
        return (
          <g key={product.slug}>
            <circle className="af-port" cx={midX - 116} cy="230" r="3.6" />
            <path className="spoke" d={`M${midX - 116} 230 C ${midX - 150} 230, ${midX - 150} ${y}, ${midX - 116} ${y}`} fill="none" />
            <rect className="srcbox sou-prod" x={midX - 116} y={y - 19} width="232" height="40" rx="9" />
            <text className="srct" x={midX} y={y + 6} textAnchor="middle">{product.menuLabel}</text>
            <circle className="af-port out" cx={midX + 116} cy={y} r="3" />
            <path className="spoke" d={`M${midX + 116} ${y} C ${outX - 190} ${y}, ${outX - 190} 230, ${outX - 108} 230`} fill="none" />
            <path data-draw className={`outflow${i === 0 ? "" : ` o${(i % 3) + 1}`}`} d={`M${midX + 116} ${y} C ${outX - 190} ${y}, ${outX - 190} 230, ${outX - 108} 230`} fill="none" />
          </g>
        );
      })}

      {OUTCOMES.map((label, i) => {
        const y = rowY(i, OUTCOMES.length) + 40;
        return (
          <g key={label}>
            <circle className="af-port out" cx={outX - 108} cy="230" r="3.6" />
            <path className="spoke" d={`M${outX - 108} 230 C ${outX - 150} 230, ${outX - 150} ${y}, ${outX - 92} ${y}`} fill="none" />
            <rect className="srcbox sou-out" x={outX - 92} y={y - 18} width="184" height="38" rx="8" />
            <text className="outt" x={outX} y={y + 6} textAnchor="middle">{label}</text>
          </g>
        );
      })}
    </svg>
  );
}

function ProductTeaser({ product }: { product: PortfolioProduct }) {
  const Icon = ICONS[product.icon] ?? Factory;
  return (
    <article className="pf-card sou-teaser rv">
      <div className="sou-teaser-icon"><Icon size={20} /></div>
      <p className="pf-value">{product.stackLayer}</p>
      <h3>{product.name}</h3>
      <p className="pf-tag">{product.valueLine}</p>
      <ul className="pf-benefits">
        {product.primaryBenefits.slice(0, 2).map((benefit) => (
          <li key={benefit}>{benefit}</li>
        ))}
      </ul>
      <Link className="pf-more" to={productPath(product)}>
        Explore {product.menuLabel} <ArrowRight size={15} />
      </Link>
    </article>
  );
}

export function SouHomePage() {
  /* PPIQ-T070-06: .rv sets opacity 0 and only becomes visible when an
   * IntersectionObserver adds .in. That observer lives in NewHomePage, so this
   * page rendered every element at zero opacity - present in the DOM, invisible
   * on screen. Same observer, same thresholds, mounted here. */
  useEffect(() => {
    const io = new IntersectionObserver(
      (entries) => {
        entries.forEach((e) => {
          if (e.isIntersecting) {
            e.target.classList.add("in");
            io.unobserve(e.target);
          }
        });
      },
      { threshold: 0.16, rootMargin: "0px 0px -40px 0px" }
    );
    document.querySelectorAll(".rv").forEach((el) => io.observe(el));
    return () => io.disconnect();
  }, []);

  return (
    <div className="new-landing-wrapper">
      <section className="section sou-top">
        <div className="wrap">
          <p className="eyebrow rv">SOU INDUSTRIAL SOFTWARE</p>
          <h2 className="rv sou-title">
            Industrial software for<br />
            <span className="g">smarter plants.</span>
          </h2>
          <p className="lead rv sou-lead">
            Five standalone products for process manufacturing - plant intelligence, execution,
            quality, material flow and energy. Machine learning, prediction and conversational
            answers are built into the products that support them, on your data, inside your plant.
          </p>
          <div className="sou-cta rv">
            <Link className="btn primary" to="/products">Explore the products</Link>
            <Link className="btn ghost" to="/contact">Talk to us</Link>
          </div>
          <div className="sou-graphic-wrap rv">
            <PortfolioGraphic />
          </div>
        </div>
      </section>

      <div className="band rv">
        <div className="wrap">
          <div className="cell">
            <div className="big">Built for process plants</div>
            <div className="s">Steel, oil and gas, water, food, chemicals, mining - the products describe a plant, not one industry.</div>
          </div>
          <div className="cell">
            <div className="big">Evidence, not assertion</div>
            <div className="s">Where our software gives you an answer, it can show the source records that answer came from.</div>
          </div>
          <div className="cell">
            <div className="big">Your engineers operate it</div>
            <div className="s">Configuration is visual and no-code, so a process engineer changes it without waiting for a developer.</div>
          </div>
        </div>
      </div>

      <section className="section">
        <div className="wrap">
          <p className="eyebrow rv">THE PORTFOLIO</p>
          <h2 className="rv">Five products. One plant.</h2>
          <p className="lead rv">
            Each one is bought, deployed and run in its own right. PlantProcess IQ is our flagship,
            not a container the other four sit inside.
          </p>
          <div className="sou-grid">
            {souProducts.map((product) => (
              <ProductTeaser key={product.slug} product={product} />
            ))}
          </div>
          <div className="sou-cta rv">
            <Link className="btn primary" to="/products">Compare all five products</Link>
          </div>
        </div>
      </section>
    </div>
  );
}