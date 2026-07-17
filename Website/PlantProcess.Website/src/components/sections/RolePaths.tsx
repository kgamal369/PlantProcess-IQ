import { BadgeDollarSign, Factory, FlaskConical, ShieldCheck, SlidersHorizontal } from "lucide-react";

const paths = [
  { code: "operations", title: "Operations", headline: "See which losses require attention now.", text: "Prioritize active risks, recurring downtime and production-impact signals before they compound.", icon: Factory, accent: "cyan" },
  { code: "quality", title: "Quality", headline: "Trace defects to evidence-ranked contributors.", text: "Move from inspection event to material history, upstream conditions, population and proof.", icon: FlaskConical, accent: "green" },
  { code: "engineering", title: "Process Engineering", headline: "Replace spreadsheet stitching with a governed investigation.", text: "Compare process parameters, stages and outcomes without losing genealogy or statistical context.", icon: SlidersHorizontal, accent: "blue" },
  { code: "security", title: "IT & OT", headline: "Approve intelligence without control-system risk.", text: "Read-only access, no command path, governed deployment, auditability and evidence lineage.", icon: ShieldCheck, accent: "amber" },
  { code: "value", title: "CFO & Procurement", headline: "Fund what proves measurable plant value.", text: "Start with one costly outcome, expose assumptions, and scale only after the signal is proven.", icon: BadgeDollarSign, accent: "silver" },
];

export function RolePaths() {
  return (
    <section className="commercial-section role-paths-section" id="solutions">
      <div className="section-shell">
        <div className="section-heading split-heading">
          <div>
            <div className="section-kicker">Choose your entry point</div>
            <h2>One evidence system. Five reasons to buy.</h2>
          </div>
          <p>Each role sees the same governed truth through the decision lens they own.</p>
        </div>

        <div className="role-paths-grid">
          {paths.map(({ code, title, headline, text, icon: Icon, accent }) => (
            <a className={`role-card role-card--${accent}`} href={`#${code}-path`} key={code}>
              <div className="role-card__icon"><Icon size={25} /></div>
              <span>{title}</span>
              <h3>{headline}</h3>
              <p>{text}</p>
              <strong>Explore the value path →</strong>
            </a>
          ))}
        </div>
      </div>
    </section>
  );
}

export default RolePaths;
