export function ProductScreenshotShowcase() {
  return (
    <section className="website-section proof-screenshot-section" id="product-proof">
      <div className="section-kicker">Product proof</div>
      <div className="section-heading-row">
        <div>
          <h2>Real PlantProcess IQ workspace, not a placeholder story.</h2>
          <p>
            The Phase 1 website must show the actual product: connector truth,
            source-shaped data, schema mapping, widget builder, investigation,
            and customer-grade reporting.
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
          <span>Connector truth, dashboard widgets, material investigation, and report export.</span>
        </div>
        <div>
          <strong>Shown in data</strong>
          <span>630 heats, 5,670 slabs/coils, 39,690 HSM pass measurements, 1,987 defects.</span>
        </div>
        <div>
          <strong>Shown in message</strong>
          <span>Evidence-based investigation, not guaranteed root-cause theatre.</span>
        </div>
      </div>
    </section>
  );
}

export default ProductScreenshotShowcase;