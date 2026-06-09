import { executiveDashboardCards } from "@/security/phase9RoleAccess";
import "./phase9-personas.css";

export function ExecutivePersonaDashboardPage() {
  const cards = executiveDashboardCards();

  return (
    <main className="phase9-page" data-testid="phase9-executive-dashboard">
      <section className="phase9-hero">
        <p className="phase9-eyebrow">P09 · T-052 · Executive / CEO persona scope</p>
        <h1>Executive Decision Dashboard</h1>
        <p className="phase9-muted">
          A curated CEO-facing view for value, ROI, KPI, quality and downtime summaries. Engineering and configuration pages are intentionally hidden from this persona scope.
        </p>
        <strong className="phase9-badge">Decision-grade summary</strong>
      </section>

      <section className="phase9-grid">
        {cards.map((card) => (
          <article className="phase9-card" key={card.code}>
            <span className="phase9-badge">{card.code}</span>
            <h2>{card.title}</h2>
            <strong>{card.value}</strong>
            <p className="phase9-muted">{card.caveat}</p>
          </article>
        ))}
      </section>
    </main>
  );
}

export default ExecutivePersonaDashboardPage;