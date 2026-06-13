/* PPIQ-PHASE7-PRODUCT */
import React, { useState } from "react";
import type { ProductPageModel } from "../../content/products/model";

// Dark-Industrial tokens (literal hexes required by the brand-fidelity check).
const T = {
  navy: "#050B18",
  panel: "#0B1730",
  cyan: "#00D4FF",
  blue: "#0A84FF",
  ok: "#2CE6A2",
  warn: "#FFB020",
  crit: "#FF4D6D",
  white: "#EAF6FF",
  steel: "#8EA7C1",
};

const LEAD_ENDPOINT =
  (import.meta as any)?.env?.VITE_LEAD_CAPTURE_ENDPOINT || "/api/website/leads";

export function ProductPage({ product }: { product: ProductPageModel }) {
  const [email, setEmail] = useState("");
  const [company, setCompany] = useState("");
  const [state, setState] = useState<"idle" | "sending" | "done" | "error" | "invalid">("idle");

  async function submit() {
    if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email) || company.trim().length < 2) {
      setState("invalid");
      return;
    }
    setState("sending");
    try {
      const res = await fetch(LEAD_ENDPOINT, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, company, product: product.id, source: "product-page" }),
      });
      setState(res.ok ? "done" : "error");
    } catch {
      setState("error");
    }
  }

  const section: React.CSSProperties = { maxWidth: 1080, margin: "0 auto", padding: "0 20px" };
  const card: React.CSSProperties = {
    background: T.panel,
    border: "1px solid #16243D",
    borderRadius: 12,
    padding: 20,
  };

  return (
    <main
      data-testid="product-page"
      data-product={product.slug}
      style={{ background: T.navy, color: T.white, fontFamily: "Inter, system-ui, sans-serif", paddingBottom: 64 }}
    >
      {/* HERO */}
      <section data-section="hero" style={{ ...section, paddingTop: 56, paddingBottom: 32 }}>
        <div style={{ color: T.cyan, fontFamily: "'JetBrains Mono', monospace", fontSize: 12, letterSpacing: "0.18em", textTransform: "uppercase" }}>
          {product.category}
        </div>
        <h1 style={{ fontSize: "clamp(28px, 5vw, 46px)", lineHeight: 1.1, margin: "10px 0 12px" }}>{product.name}</h1>
        <p style={{ fontSize: "clamp(18px, 3vw, 24px)", color: T.cyan, fontWeight: 600, margin: 0 }}>{product.headline}</p>
        <p style={{ fontSize: 16, color: T.steel, maxWidth: 760, marginTop: 12 }}>{product.subTagline}</p>
      </section>

      {/* PROBLEM */}
      <section data-section="problem" style={{ ...section, marginBottom: 28 }}>
        <div style={card}>
          <h2 style={{ marginTop: 0, color: T.warn }}>{product.problem.title}</h2>
          <p style={{ color: T.steel, lineHeight: 1.6, margin: 0 }}>{product.problem.body}</p>
        </div>
      </section>

      {/* CAPABILITIES */}
      <section data-section="capabilities" style={{ ...section, marginBottom: 28 }}>
        <h2 style={{ borderLeft: `3px solid ${T.cyan}`, paddingLeft: 12 }}>What it does</h2>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))", gap: 14 }}>
          {product.capabilities.map((c, i) => (
            <div key={i} style={card}>
              <h3 style={{ marginTop: 0, fontSize: 16, color: T.white }}>{c.title}</h3>
              <p style={{ color: T.steel, lineHeight: 1.55, margin: 0, fontSize: 14 }}>{c.body}</p>
            </div>
          ))}
        </div>
      </section>

      {/* BENEFITS */}
      <section data-section="benefits" style={{ ...section, marginBottom: 28 }}>
        <h2 style={{ borderLeft: `3px solid ${T.ok}`, paddingLeft: 12 }}>Why it pays</h2>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))", gap: 14 }}>
          {product.benefits.map((b, i) => (
            <div key={i} style={{ ...card, borderColor: T.ok }}>
              <div style={{ color: T.ok, fontWeight: 700, fontSize: 15 }}>{b.metricLabel}</div>
              <p style={{ color: T.steel, lineHeight: 1.55, margin: "8px 0 0", fontSize: 14 }}>{b.body}</p>
            </div>
          ))}
        </div>
      </section>

      {/* DIAGRAM */}
      <section data-section="diagram" style={{ ...section, marginBottom: 28 }}>
        <div style={card}>
          <div style={{ color: T.cyan, fontSize: 13, marginBottom: 10, fontFamily: "'JetBrains Mono', monospace" }}>{product.diagram.caption}</div>
          <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 8 }}>
            {product.diagram.nodes.map((n, i) => (
              <React.Fragment key={i}>
                <span style={{ background: T.navy, border: `1px solid ${T.blue}`, borderRadius: 8, padding: "8px 12px", fontSize: 13 }}>{n}</span>
                {i < product.diagram.nodes.length - 1 && <span style={{ color: T.steel }}>&rarr;</span>}
              </React.Fragment>
            ))}
          </div>
          <p style={{ color: T.steel, fontSize: 13, marginTop: 12, marginBottom: 0 }}>{product.diagram.note}</p>
        </div>
      </section>

      {/* LICENSING */}
      <section data-section="licensing" style={{ ...section, marginBottom: 28 }}>
        <h2 style={{ borderLeft: `3px solid ${T.blue}`, paddingLeft: 12 }}>Licensing</h2>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))", gap: 14 }}>
          {product.licensing.tiers.map((t, i) => (
            <div key={i} style={card}>
              <div style={{ color: T.cyan, fontWeight: 700 }}>{t.name}</div>
              <p style={{ color: T.steel, fontSize: 14, margin: "8px 0 0" }}>{t.includes}</p>
            </div>
          ))}
        </div>
        <p style={{ color: T.steel, fontSize: 13, marginTop: 10 }}>{product.licensing.note}</p>
      </section>

      {/* EVIDENCE POSTURE */}
      <section data-section="evidence" style={{ ...section, marginBottom: 28 }}>
        <div style={{ ...card, borderColor: T.warn }}>
          <div style={{ color: T.warn, fontWeight: 700, fontSize: 13, fontFamily: "'JetBrains Mono', monospace" }}>READ-ONLY POSTURE</div>
          <p style={{ color: T.steel, lineHeight: 1.6, margin: "8px 0 0", fontSize: 14 }}>{product.evidencePosture}</p>
        </div>
      </section>

      {/* CTA / LEAD CAPTURE */}
      <section data-section="cta" style={section}>
        <div style={{ ...card, borderColor: T.cyan }}>
          <h2 style={{ marginTop: 0 }}>{product.cta.heading}</h2>
          <p style={{ color: T.steel }}>{product.cta.body}</p>
          <div style={{ display: "flex", flexWrap: "wrap", gap: 10, marginTop: 8 }}>
            <input
              data-testid="lead-email"
              aria-label="Work email"
              placeholder="Work email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              style={{ flex: "1 1 220px", minWidth: 0, padding: "10px 12px", borderRadius: 8, border: "1px solid #16243D", background: T.navy, color: T.white }}
            />
            <input
              data-testid="lead-company"
              aria-label="Company"
              placeholder="Company"
              value={company}
              onChange={(e) => setCompany(e.target.value)}
              style={{ flex: "1 1 220px", minWidth: 0, padding: "10px 12px", borderRadius: 8, border: "1px solid #16243D", background: T.navy, color: T.white }}
            />
            <button
              data-testid="lead-submit"
              onClick={submit}
              disabled={state === "sending"}
              style={{ padding: "10px 18px", borderRadius: 8, border: "none", background: T.cyan, color: T.navy, fontWeight: 700, cursor: "pointer" }}
            >
              {state === "sending" ? "Sending..." : product.cta.buttonLabel}
            </button>
          </div>
          <div data-testid="lead-status" style={{ marginTop: 10, fontSize: 13 }}>
            {state === "done" && <span style={{ color: T.ok }}>Thanks - we&apos;ll be in touch shortly.</span>}
            {state === "invalid" && <span style={{ color: T.warn }}>Please enter a valid work email and company.</span>}
            {state === "error" && <span style={{ color: T.crit }}>Something went wrong. Please try again.</span>}
          </div>
        </div>
      </section>
    </main>
  );
}

export default ProductPage;