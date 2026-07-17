import { Award, BookOpenCheck, Globe2, Microchip, Network, Workflow } from "lucide-react";

const timeline = [
  { years: "2013–2018", title: "Level 2 automation · Flat steel", text: "Built and supported process automation and industrial analytics in Egypt, working directly with tracking, quality and production data.", icon: Microchip },
  { years: "2018–2019", title: "PSI Metals · Brussels", text: "Industrial digitalization, energy analytics and reactive cutting optimization for major steel producers in Europe and the United States.", icon: Network },
  { years: "2019–2024", title: "SMS group · MES engineering", text: "Designed and implemented planning, order, material, equipment and production-management capabilities for international plants.", icon: Workflow },
  { years: "2024–2026", title: "Level 2 commissioning worldwide", text: "Connected Level 1 measurements and production constraints to industrial models across projects in Japan, Egypt and Italy.", icon: Globe2 },
];

export function FounderAuthority() {
  return (
    <section className="commercial-section founder-section" id="founder">
      <div className="section-shell founder-layout">
        <div className="founder-intro">
          <div className="section-kicker">Founder–market fit</div>
          <h2>Built from inside industrial automation—not outside it.</h2>
          <p>
            PlantProcess IQ is the product Karim Gamal wished he had while engineering Level 2 automation, MES and production models across global manufacturing projects.
          </p>
          <div className="founder-credentials">
            <span><Award size={18} /> 13+ years industrial software</span>
            <span><BookOpenCheck size={18} /> MSc Electrical & Computer Engineering</span>
            <span><BookOpenCheck size={18} /> Two published ML research papers</span>
          </div>
        </div>

        <div className="founder-timeline">
          {timeline.map(({ years, title, text, icon: Icon }) => (
            <article key={years}>
              <div className="founder-timeline__icon"><Icon size={20} /></div>
              <div>
                <span>{years}</span>
                <h3>{title}</h3>
                <p>{text}</p>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}

export default FounderAuthority;
