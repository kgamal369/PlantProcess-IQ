
export function ProductScreenshotShowcase() {
  return (
    <section className="website-section proof-screenshot-section" id="product-proof">
      <div className="section-kicker">Product proof</div>
      <div className="section-heading-row">
        <div>
          <h2>Real PlantProcess IQ workspace, not a placeholder story.</h2>
          <p>
            The website shows the actual product: source connection, staging and import
            monitoring, schema mapping, dashboards and widgets, material investigation,
            correlation analysis, and customer-grade reporting.
          </p>
        </div>
      </div>

      <div className="product-screenshot-frame">
        <img
          src="/screenshots/product-dashboard.png"
          alt="PlantProcess IQ dashboard screenshot showing manufacturing intelligence widgets"
          onError={(event) => {
            event.currentTarget.style.display = "none";
          }}
        />

        <div className="product-screenshot-fallback">
          <span>Replace this frame with:</span>
          <strong>/public/screenshots/product-dashboard.png</strong>
          <p>
            Capture the current dashboard after golden demo data is loaded. This
            keeps the website honest and avoids fake UI mockups.
          </p>
        </div>
      </div>

      <div className="proof-grid">
        <div>
          <strong>Shown in app</strong>
          <span>Source connection, import monitoring, dashboards, material investigation, correlation analysis, and report export.</span>
        </div>
        <div>
          <strong>Shown in data</strong>
          <span>11,997 material units, 1,993 quality events, and 5,688 genealogy links unified from 6 live source systems.</span>
        </div>
        <div>
          <strong>Shown in message</strong>
          <span>Evidence-based investigation with the population and method shown, not root-cause theatre.</span>
        </div>
      </div>
    </section>
  );
}

export default ProductScreenshotShowcase;
