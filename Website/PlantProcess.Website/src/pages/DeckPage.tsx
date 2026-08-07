/* DECK-03 - the customer presentation, four tabs. */
import { useEffect, useMemo, useState } from "react";
import { souProducts } from "../content/portfolio/souProducts";
import { WORLD_PATHS } from "./worldPaths";
import "../styles/new-landing.css";
import "../styles/motion-roi.css";

/**
 * THE PRESENTATION.
 *
 * Four tabs: who I am, the application, a worked example, and how the licence
 * is calculated. Everything reuses the site's own type scale, node vocabulary,
 * motion classes and card language, so the deck looks like the product.
 *
 * THE CALCULATOR RETURNS LICENCE UNITS AND A TIER, NEVER A CURRENCY FIGURE.
 * That is deliberate: it shows the customer which factors drive his licence and
 * lets him move them, without committing to a price in the room.
 */

type TabKey = "me" | "application" | "tutorial" | "pricing";

const TABS: { key: TabKey; label: string }[] = [
  { key: "me", label: "Me" },
  { key: "application", label: "Application" },
  { key: "tutorial", label: "Example tutorial" },
  { key: "pricing", label: "Pricing and licence" },
];

/* ---------------------------------------------------------------- ME ---- */

const CAREER = [
  {
    years: "2013 - 2018",
    place: "EZDK flat steel plant, Alexandria, Egypt",
    role: "Level 2 for the whole plant",
    detail: "Level 2 systems and the process models that run the plant end to end - electric arc furnace, ladle furnace, continuous casting and the hot strip mill.",
  },
  {
    years: "2018 - 2020",
    place: "PSI Metals, Brussels, Belgium",
    role: "Project engineer, plant digitalisation",
    detail: "Digitalisation projects for Tata Steel and for ArcelorMittal.",
  },
  {
    years: "2020 - 2024",
    place: "SMS Group, Digital department, Duesseldorf",
    role: "MES and QES projects",
    detail: "Manufacturing and quality execution systems for SSAB in Sweden, NorthStar BlueScope, Nucor Steel and Big River Steel.",
  },
  {
    years: "2024 - 2026",
    place: "SMS Group, Level 2 department, Duesseldorf",
    role: "Level 2 process models",
    detail: "Sabic Hadeed in Saudi Arabia, JSW Piombino in Italy, and the rail system at Suez Steel.",
  },
];

/* Real geography. The outlines come from the public world.geo.json country
 * data, projected to this exact 1000 x 500 equirectangular viewBox, so a plant
 * plotted from its true longitude and latitude lands where it belongs.
 * See worldPaths.ts for why the data is checked in rather than fetched. */
const SITES = [
  { x: 250.1, y: 150.8, name: "Big River Steel", country: "USA", year: "2021 - 2024" },
  { x: 258.0, y: 134.4, name: "ArcelorMittal Burns Harbor", country: "USA", year: "2018 - 2020" },
  { x: 266.7, y: 134.5, name: "NorthStar BlueScope", country: "USA", year: "2020 - 2024" },
  { x: 275.4, y: 152.1, name: "Nucor Steel", country: "USA", year: "2020 - 2024" },
  { x: 512.8, y: 104.3, name: "Tata Steel IJmuiden", country: "Netherlands", year: "2018 - 2020" },
  { x: 512.1, y: 108.8, name: "PSI Metals Brussels", country: "Belgium", year: "2018 - 2020" },
  { x: 518.8, y: 107.7, name: "SMS Group Duesseldorf", country: "Germany", year: "2020 - 2026" },
  { x: 547.5, y: 87.0, name: "SSAB", country: "Sweden", year: "2020 - 2024" },
  { x: 529.2, y: 130.8, name: "JSW Piombino", country: "Italy", year: "2024 - 2026" },
  { x: 583.1, y: 163.3, name: "EZDK Alexandria", country: "Egypt", year: "2013 - 2018" },
  { x: 589.8, y: 166.8, name: "Suez Steel", country: "Egypt", year: "2024 - 2026" },
  { x: 637.9, y: 175.0, name: "Sabic Hadeed Jubail", country: "Saudi Arabia", year: "2024 - 2026" },
  { x: 863.4, y: 155.9, name: "Nippon Steel", country: "Japan", year: "2024" },
];

function WorldMap() {
  const [active, setActive] = useState<number | null>(null);
  const site = active === null ? null : SITES[active];

  return (
    <div className="deck-mapwrap">
      <svg
        viewBox="0 40 1000 380"
        className="deck-map"
        role="img"
        aria-label="A world map with the plants worked on marked in North America, Europe, the Middle East, North Africa and Japan."
      >
        <g className="deck-land">
          {WORLD_PATHS.map((d, i) => (
            <path key={i} d={d} />
          ))}
        </g>

        {SITES.map((s, i) => (
          <g
            key={s.name}
            className={active === i ? "deck-pin deck-pin--on" : "deck-pin"}
            onMouseEnter={() => setActive(i)}
            onMouseLeave={() => setActive(null)}
            onClick={() => setActive(active === i ? null : i)}
          >
            <circle className="deck-pin-halo" cx={s.x} cy={s.y} r="13" />
            <circle className="deck-pin-dot" cx={s.x} cy={s.y} r="4" />
          </g>
        ))}
      </svg>

      <div className={site ? "deck-pincard deck-pincard--on" : "deck-pincard"}>
        {site ? (
          <span>
            <strong>{site.name}</strong>
            <em>{site.country}</em>
            <b>{site.year}</b>
          </span>
        ) : (
          <span className="deck-pincard-hint">Hover a point to see the plant and the years.</span>
        )}
      </div>

      <p className="deck-map-cap">
        Thirteen plants across eight countries - melting, casting, rolling, inspection and logistics.
      </p>
    </div>
  );
}

function MeTab() {
  return (
    <div>
      <p className="eyebrow rv">WHO IS BEHIND THIS</p>
      <h2 className="rv deck-title">
        Karim Gamal Elsayed<br />
        <span className="g">thirteen years inside the plant.</span>
      </h2>
      <p className="lead rv">
        BSc and MSc in electrical and computer engineering. I have written the Level 2 models that run
        a melt shop, and the MES and quality systems that run above them. I built this product because
        I was the engineer who could not answer why a defect happened.
      </p>

      <div className="rv"><WorldMap /></div>
      <div className="deck-career rv">
        {CAREER.map((row) => (
          <div className="deck-career-row" key={row.place}>
            <span className="deck-years">{row.years}</span>
            <div>
              <strong>{row.place}</strong>
              <p className="deck-role">{row.role}</p>
              <p className="deck-detail">{row.detail}</p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

/* -------------------------------------------------------- APPLICATION ---- */

const INDUSTRIES = ["Steel", "Oil and gas", "Water", "Food", "Paper", "Tyres", "Pharma", "Cement"];

/* 1 - the isolated screens problem, Chapter 1 section 1.1e */
function IsolatedGraphic() {
  const boxes = ["FURNACE HMI", "CASTER HMI", "MILL HMI", "GAUGE PC", "LAB SHEET", "YARD LIST"];
  return (
    <svg viewBox="0 0 1020 300" className="deck-fig" role="img"
      aria-label="Six separate screens each showing only its own machine, versus one hub that sees the whole plant.">
      <text className="srcs deck-col" x="230" y="28" textAnchor="middle">TODAY</text>
      <text className="srcs deck-col deck-col--good" x="800" y="28" textAnchor="middle">WITH ONE HUB</text>
      {boxes.map((b, i) => {
        const col = i % 3;
        const row = Math.floor(i / 3);
        const x = 70 + col * 160;
        const y = 70 + row * 90;
        return (
          <g key={b}>
            <rect className="deck-lonely" x={x} y={y} width="140" height="62" rx="8" />
            <text className="srct deck-slow-t" x={x + 70} y={y + 28} textAnchor="middle">{b}</text>
            <text className="srcs" x={x + 70} y={y + 46} textAnchor="middle">its own data only</text>
          </g>
        );
      })}
      <path className="spoke" d="M 560 150 H 690" fill="none" />
      <path data-draw className="outflow" d="M 560 150 H 690" fill="none" />
      <circle className="ring" cx="800" cy="150" r="86" />
      <g className="hub-core">
        <circle className="deck-canon" cx="800" cy="150" r="66" />
        <text className="deck-code" x="800" y="144" textAnchor="middle">ONE PLANT</text>
        <text className="srcs" x="800" y="162" textAnchor="middle">every stage, one story</text>
      </g>
    </svg>
  );
}

/* 2 - configured, not developed */
function ConfigureGraphic() {
  const items = ["New page", "New widget", "New data link", "New job", "New analysis"];
  return (
    <svg viewBox="0 0 1020 220" className="deck-fig" role="img"
      aria-label="Pages, widgets, data links, jobs and analyses added by your own engineers, in any industry.">
      {items.map((it, i) => {
        const x = 120 + i * 190;
        return (
          <g key={it}>
            {i < items.length - 1 ? (
              <g>
                <path className="spoke" d={`M${x + 62} 84 H ${x + 128}`} fill="none" />
                <path data-draw className={`spoke-flow${i === 0 ? "" : ` s${i + 1}`}`} d={`M${x + 62} 84 H ${x + 128}`} fill="none" />
              </g>
            ) : null}
            <rect className="srcbox" x={x - 62} y="62" width="124" height="44" rx="9" />
            <text className="srct" x={x} y="89" textAnchor="middle">{it}</text>
          </g>
        );
      })}
      <text className="srcs deck-col" x="510" y="150" textAnchor="middle">NO DEVELOPMENT PROJECT - YOUR OWN ENGINEERS</text>
      <text className="srcs" x="510" y="186" textAnchor="middle">
        {INDUSTRIES.join("   -   ")}
      </text>
    </svg>
  );
}

/* 4 - early cause, late defect. Chapter 1 section 1.3a */
const CHAIN = [
  { code: "STAGE 1", note: "a small drift here" },
  { code: "STAGE 2", note: "nothing looks wrong" },
  { code: "STAGE 3", note: "still within limits" },
  { code: "STAGE 4", note: "the defect appears" },
];

function CausalityGraphic() {
  const x = (i: number) => 150 + i * 240;
  return (
    <svg viewBox="0 0 1020 300" className="deck-fig" role="img"
      aria-label="A small deviation at an early stage becomes the defect found at the last stage, and the link is traced back.">
      {CHAIN.map((c, i) => (
        <g key={c.code}>
          {i < CHAIN.length - 1 ? (
            <g>
              <path className="spoke" d={`M${x(i) + 66} 110 H ${x(i + 1) - 66}`} fill="none" />
              <path data-draw className={`spoke-flow${i === 0 ? "" : ` s${i + 1}`}`} d={`M${x(i) + 66} 110 H ${x(i + 1) - 66}`} fill="none" />
            </g>
          ) : null}
          <circle className="ring" cx={x(i)} cy="110" r="52" />
          <circle className={i === 0 ? "deck-node deck-node--cause" : i === 3 ? "deck-node deck-node--bad" : "deck-node"} cx={x(i)} cy="110" r="38" />
          <text className="deck-step" x={x(i)} y="115" textAnchor="middle">{i + 1}</text>
          <text className="srct" x={x(i)} y="184" textAnchor="middle">{c.code}</text>
          <text className="srcs" x={x(i)} y="202" textAnchor="middle">{c.note}</text>
        </g>
      ))}
      <path data-draw className="deck-trace" d={`M${x(3)} 158 C ${x(3)} 274, ${x(0)} 274, ${x(0)} 158`} fill="none" />
      <text className="deck-trace-label" x={x(1) + 120} y="292" textAnchor="middle">
        the correlation points back to the stage that actually caused it
      </text>
    </svg>
  );
}

/* 5 - predict, then advise. Chapter 1 section 1.3c */
function PredictGraphic() {
  return (
    <svg viewBox="0 0 1020 260" className="deck-fig" role="img"
      aria-label="A known bad combination is recognised early, flagged as at risk, and a downstream correction is suggested while there is still time.">
      <rect className="srcbox" x="40" y="70" width="230" height="96" rx="10" />
      <text className="srct" x="155" y="102" textAnchor="middle">THIS COMBINATION</text>
      <text className="srcs" x="155" y="124" textAnchor="middle">this raw material</text>
      <text className="srcs" x="155" y="142" textAnchor="middle">with this speed</text>

      <path className="spoke" d="M 270 118 H 386" fill="none" />
      <path data-draw className="spoke-flow" d="M 270 118 H 386" fill="none" />

      <circle className="ring" cx="470" cy="118" r="76" />
      <g className="hub-core">
        <circle className="deck-canon" cx="470" cy="118" r="58" />
        <text className="deck-code" x="470" y="112" textAnchor="middle">HISTORY</text>
        <text className="srcs" x="470" y="130" textAnchor="middle">months of your own</text>
      </g>

      <path className="spoke" d="M 546 118 C 610 118, 640 62, 706 62" fill="none" />
      <path data-draw className="outflow" d="M 546 118 C 610 118, 640 62, 706 62" fill="none" />
      <rect className="deck-bad" x="706" y="38" width="274" height="48" rx="10" />
      <text className="outt deck-bad-t" x="843" y="68" textAnchor="middle">this piece is at risk</text>

      <path className="spoke" d="M 546 118 C 610 118, 640 182, 706 182" fill="none" />
      <path data-draw className="outflow o2" d="M 546 118 C 610 118, 640 182, 706 182" fill="none" />
      <rect className="deck-out" x="706" y="158" width="274" height="48" rx="10" />
      <text className="outt" x="843" y="188" textAnchor="middle">change this downstream</text>

      <text className="srcs" x="843" y="232" textAnchor="middle">while there is still time to act</text>
    </svg>
  );
}

function ExpertGraphic() {
  const leftX = 250;
  const rightX = 760;
  return (
    <svg viewBox="0 0 1010 260" className="deck-fig" role="img"
      aria-label="Days of manual troubleshooting on one side; the engine that already knows the plant on the other.">
      <text className="srcs deck-col" x={leftX} y="30" textAnchor="middle">TODAY</text>
      <text className="srcs deck-col deck-col--good" x={rightX} y="30" textAnchor="middle">WITH THE ENGINE</text>

      {["Find someone who knows the line", "Pull four systems by hand", "Argue about which number is right", "Days later, a maybe"].map((step, i) => (
        <g key={step}>
          <rect className="deck-slow" x={leftX - 190} y={62 + i * 44} width="380" height="34" rx="8" />
          <text className="srct deck-slow-t" x={leftX} y={84 + i * 44} textAnchor="middle">{step}</text>
        </g>
      ))}

      <circle className="ring" cx={rightX} cy="140" r="76" />
      <g className="hub-core">
        <circle className="deck-canon" cx={rightX} cy="140" r="58" />
        <text className="deck-code" x={rightX} y="134" textAnchor="middle">YOUR PLANT</text>
        <text className="srcs" x={rightX} y="152" textAnchor="middle">fingerprint</text>
      </g>
      {["learned from your own history", "knows what normal looks like", "answers while the shift runs"].map((note, i) => (
        <text key={note} className="srcs" x={rightX} y={236 + i * 16} textAnchor="middle">{note}</text>
      ))}

      <path className="spoke" d={`M${leftX + 190} 140 H ${rightX - 76}`} fill="none" />
      <path data-draw className="outflow" d={`M${leftX + 190} 140 H ${rightX - 76}`} fill="none" />
    </svg>
  );
}

const SECTIONS = [
  {
    tag: "STRENGTH ONE",
    head: "Every screen shows one machine. Nobody sees the plant.",
    lead: "Each production unit and each inspection device has its own screen, its own database, its own spreadsheet or its own log file - and each shows only itself. The furnace does not know what the mill saw. The gauge does not know what the laboratory measured. The plant is complete on paper and invisible as a whole.",
    fig: "isolated",
    close: "One hub reads all of them and puts the same unit of production at the centre, so the story runs end to end instead of stopping at each screen.",
  },
  {
    tag: "STRENGTH TWO",
    head: "Configured by your engineers, not developed by mine.",
    lead: "A new page, a new widget, a new link to a database, a new scheduled job, a new analysis - all of it is configuration your own people do. No release cycle, no consultant, no waiting.",
    fig: "configure",
    close: "And nothing in it is written for one industry. Steel, oil and gas, water, food, paper, tyres, pharmaceuticals - a plant is stages, machines, inspections and material, whatever it produces.",
  },
  {
    tag: "STRENGTH THREE",
    head: "An in-house expert who already knows your plant.",
    lead: "Today the answer lives in one experienced person: somebody who can guess where to look and then spend days proving it. That person is expensive, is not always there, and takes the knowledge with them when they leave.",
    fig: "expert",
    close: "The engine learns the fingerprint of your plant from your own history - which conditions travel together, which precede a problem, what normal looks like on this line and this grade. It does not need to be told your practice. It reads it.",
  },
  {
    tag: "STRENGTH FOUR",
    head: "The cause is early. The defect is late.",
    lead: "A small drift at the first stage passes every check that follows. Nothing looks wrong at stage two or three. The defect only appears at the end - and by then nobody connects it to something that happened hours and four machines earlier.",
    fig: "causality",
    close: "Because the whole plant is one model, the correlation runs backwards across every stage and points at the condition that actually caused it, with the source records attached.",
  },
  {
    tag: "STRENGTH FIVE",
    head: "Predict it early. Fix it downstream.",
    lead: "When a combination has ended badly before - this raw material with this speed, this practice on this grade - the engine recognises it while the material is still in the plant.",
    fig: "predict",
    close: "So it does two things: it flags the piece as at risk, and it suggests what to change at a later stage while there is still time to save it. That is what months of your own history are worth once something can read them.",
  },
];

function SectionFigure({ name }: { name: string }) {
  if (name === "isolated") return <IsolatedGraphic />;
  if (name === "configure") return <ConfigureGraphic />;
  if (name === "expert") return <ExpertGraphic />;
  if (name === "causality") return <CausalityGraphic />;
  return <PredictGraphic />;
}

function ApplicationTab() {
  return (
    <div>
      <p className="eyebrow rv">THE PROBLEM</p>
      <h2 className="rv">Repeated defects. Repeated stops. Nobody can say why.</h2>
      <p className="lead rv">
        The same quality defect keeps coming back and the source is never found. The line stops again
        for a root cause nobody pins down. Equipment and operators repeat the same failures. Everyone
        knows the KPIs should be better and nobody can say which change would move them - so the plant
        loses days in troubleshooting, or pays for a short expensive consultation that ends when the
        expert leaves.
      </p>

      {SECTIONS.map((s) => (
        <div className="deck-section rv" key={s.tag}>
          <p className="eyebrow">{s.tag}</p>
          <h2>{s.head}</h2>
          <p className="lead">{s.lead}</p>
          <SectionFigure name={s.fig} />
          <p className="deck-aside">{s.close}</p>
        </div>
      ))}

      <p className="eyebrow rv deck-spaced">AND ON TOP OF ALL OF IT</p>
      <h2 className="rv">Ask the plant a question.</h2>
      <p className="lead rv">
        The assistant answers from the plant model and from what the engine found, in plain language,
        and cites the records behind every answer. When the evidence does not support an answer it
        says so instead of guessing.
      </p>

      <p className="eyebrow rv deck-spaced">THE COMPANY</p>
      <div className="inds rv">
        {souProducts.map((product) => (
          <div className="ind" key={product.slug}>{product.menuLabel}</div>
        ))}
      </div>
    </div>
  );
}

/* ----------------------------------------------------------- TUTORIAL ---- */

const SOURCES = [
  { code: "EAF", note: "melting" },
  { code: "LF", note: "treatment" },
  { code: "CCM", note: "casting" },
  { code: "HSM", note: "rolling" },
  { code: "QA", note: "inspection and gauges" },
  { code: "YARD", note: "location and movement" },
];

function PipelineGraphic() {
  const srcX = 118;
  const dumpX = 386;
  const canonX = 606;
  const engX = 830;
  const outX = 1010;
  const rowY = (i: number) => 74 + i * 58;
  const mid = 74 + ((SOURCES.length - 1) * 58) / 2;

  return (
    <svg
      viewBox="0 0 1140 520"
      className="deck-pipe"
      role="img"
      aria-label="Plant databases flow into a dump store, then into one canonical plant model, then to the engine; dashboards and the assistant read both the plant model and the engine results."
    >
      <text className="srcs deck-col" x={srcX} y="30" textAnchor="middle">YOUR DATABASES</text>
      <text className="srcs deck-col" x={dumpX} y="30" textAnchor="middle">DUMP STORE</text>
      <text className="srcs deck-col" x={canonX} y="30" textAnchor="middle">CANONICAL PLANT</text>
      <text className="srcs deck-col" x={engX} y="30" textAnchor="middle">ENGINE</text>
      <text className="srcs deck-col" x={outX} y="30" textAnchor="middle">WHAT YOU SEE</text>

      {SOURCES.map((s, i) => {
        const y = rowY(i);
        const d = `M${srcX + 74} ${y} C ${srcX + 160} ${y}, ${dumpX - 160} ${mid}, ${dumpX - 58} ${mid}`;
        return (
          <g key={s.code}>
            <rect className="srcbox" x={srcX - 74} y={y - 17} width="148" height="34" rx="8" />
            <text className="srct" x={srcX - 58} y={y + 5}>{s.code}</text>
            <text className="srcs" x={srcX + 14} y={y + 5}>{s.note}</text>
            <circle className="af-port" cx={srcX + 74} cy={y} r="3" />
            <path className="spoke" d={d} fill="none" />
            <path data-draw className={`spoke-flow${i === 0 ? "" : ` s${i + 1}`}`} d={d} fill="none" />
          </g>
        );
      })}

      <circle className="af-port" cx={dumpX - 58} cy={mid} r="3.6" />
      <rect className="deck-stage" x={dumpX - 58} y={mid - 32} width="116" height="64" rx="10" />
      <text className="srct" x={dumpX} y={mid - 6} textAnchor="middle">RAW COPY</text>
      <text className="srcs" x={dumpX} y={mid + 12} textAnchor="middle">read only, never written back</text>

      <path className="spoke" d={`M${dumpX + 58} ${mid} H ${canonX - 52}`} fill="none" />
      <path data-draw className="spoke-flow s5" d={`M${dumpX + 58} ${mid} H ${canonX - 52}`} fill="none" />

      <circle className="ring" cx={canonX} cy={mid} r="52" />
      <g className="hub-core">
        <circle className="deck-canon" cx={canonX} cy={mid} r="42" />
        <text className="deck-code" x={canonX} y={mid - 2} textAnchor="middle">ONE PLANT</text>
        <text className="srcs" x={canonX} y={mid + 15} textAnchor="middle">genealogy</text>
      </g>

      <path className="spoke" d={`M${canonX + 52} ${mid} H ${engX - 56}`} fill="none" />
      <path data-draw className="spoke-flow s6" d={`M${canonX + 52} ${mid} H ${engX - 56}`} fill="none" />
      <rect className="deck-engine" x={engX - 56} y={mid - 44} width="112" height="88" rx="12" />
      <text className="deck-code" x={engX} y={mid - 16} textAnchor="middle">ENGINE</text>
      <text className="srcs" x={engX} y={mid + 2} textAnchor="middle">correlation</text>
      <text className="srcs" x={engX} y={mid + 18} textAnchor="middle">AI and ML</text>
      <text className="srcs" x={engX} y={mid + 34} textAnchor="middle">prediction</text>

      {[
        { label: "Dashboards and charts", y: mid - 108, from: "both" },
        { label: "Deep analysis views", y: mid, from: "engine" },
        { label: "Grounded assistant", y: mid + 108, from: "both" },
      ].map((out, i) => {
        const d = `M${engX + 56} ${mid} C ${engX + 110} ${mid}, ${outX - 170} ${out.y}, ${outX - 106} ${out.y}`;
        return (
          <g key={out.label}>
            <path className="spoke" d={d} fill="none" />
            <path data-draw className={`outflow${i === 0 ? "" : ` o${i + 1}`}`} d={d} fill="none" />
            <circle className="af-port out" cx={outX - 106} cy={out.y} r="3" />
            <rect className="deck-out" x={outX - 106} y={out.y - 19} width="212" height="38" rx="9" />
            <text className="outt" x={outX} y={out.y + 5} textAnchor="middle">{out.label}</text>
          </g>
        );
      })}

      {/* The plant model also feeds the dashboards and the assistant directly,
          so they show BOTH the plant and what the engine found. */}
      <path className="deck-bypass" d={`M${canonX} ${mid - 52} C ${canonX + 120} ${mid - 190}, ${outX - 260} ${mid - 150}, ${outX - 106} ${mid - 108}`} fill="none" />
      <path className="deck-bypass" d={`M${canonX} ${mid + 52} C ${canonX + 120} ${mid + 190}, ${outX - 260} ${mid + 150}, ${outX - 106} ${mid + 108}`} fill="none" />
      <text className="deck-bypass-label" x={canonX + 150} y={mid - 168}>plant model, direct</text>
      <text className="deck-bypass-label" x={canonX + 150} y={mid + 186}>plant model, direct</text>
    </svg>
  );
}

function TutorialTab() {
  return (
    <div>
      <p className="eyebrow rv">A WORKED EXAMPLE</p>
      <h2 className="rv">How a flat steel plant becomes one model.</h2>
      <p className="lead rv">
        Six systems on the floor, none of which talks to the others. This is what happens to their
        data, step by step - and every arrow points away from your plant. Nothing is ever written back.
      </p>
      <div className="rv"><PipelineGraphic /></div>

      <div className="deck-grid deck-grid--3 rv deck-spaced">
        <article className="pf-card">
          <h3>1. Read, never command</h3>
          <p className="pf-row">
            A read-only connection copies what each database already holds - the melt shop, the caster,
            the mill, inspection and gauges, and the yard. Your automation is untouched.
          </p>
        </article>
        <article className="pf-card">
          <h3>2. Rebuild the material</h3>
          <p className="pf-row">
            The copies are joined into one plant model: this coil came from this slab, from this heat,
            under these process conditions. That chain is what nobody has today.
          </p>
        </article>
        <article className="pf-card">
          <h3>3. Analyse, then explain</h3>
          <p className="pf-row">
            The engine produces correlations, AI and machine-learning results and predictions. Those
            results are displayed in the dashboards and charts alongside the plant data itself.
          </p>
        </article>
      </div>

      <p className="deck-aside rv">
        The assistant answers from both sides: the plant model and the engine's findings. That is why
        it can say which condition correlates with a defect and cite the records behind it, rather
        than only reporting what a table contains.
      </p>
      <p className="deck-aside rv">
        The example is a steel plant because steel is where I come from. The product describes a plant,
        not a plant type: your process stage, your batch, your finished unit, your inspection.
      </p>
    </div>
  );
}

/* ------------------------------------------------------------ PRICING ---- */

/* The four tiers exactly as 6.3.4a defines them. Every figure below is from the
 * document. Nothing here is invented, and no price appears anywhere - 6.3.8
 * puts the pricing tool strictly INTERNAL and off the website. */
const TIERS = [
  {
    name: "Light",
    users: 5, pages: 15, jobs: 5, links: 1, retainedGb: 250, refreshMin: 60,
    stats: false, ml: false, enterprise: false,
    sessions: "3 concurrent", objects: "25 per link", ingest: "10 rows/s",
    footprint: "about 30 GB of source",
    connectors: "Files and PostgreSQL",
    deployment: "Hosted",
    hardware: [
      ["Deployment", "Single host, all containers, one database"],
      ["CPU total", "4 cores"],
      ["RAM total", "16 GB"],
      ["Storage", "500 GB SSD, 3,000 IOPS"],
      ["Backup", "250 GB"],
      ["Network", "100 Mbit/s"],
      ["Workers", "1 instance, pools co-resident"],
    ],
    suits: "One line or one department.",
  },
  {
    name: "Pro",
    users: 25, pages: 100, jobs: 25, links: 3, retainedGb: 1024, refreshMin: 3,
    stats: true, ml: false, enterprise: false,
    sessions: "15 concurrent", objects: "150 per link", ingest: "100 rows/s",
    footprint: "about 150 GB of source",
    connectors: "Adds MySQL and SQL Server",
    deployment: "Hosted or your own cloud",
    hardware: [
      ["Deployment", "Application host plus a separate database host"],
      ["App host", "4 cores / 16 GB"],
      ["Database host", "8 cores / 32 GB"],
      ["Storage", "2 TB SSD, 10,000 IOPS, monthly partitions"],
      ["Backup", "1 TB with point-in-time recovery"],
      ["Network", "500 Mbit/s"],
      ["Workers", "2 instances"],
    ],
    suits: "3 links, 300 tables, 146 GB of source, 10 jobs at a three-minute floor. That is the calibration target, not an example.",
  },
  {
    name: "Pro Plus",
    users: 100, pages: 400, jobs: 100, links: 10, retainedGb: 5120, refreshMin: 1,
    stats: true, ml: true, enterprise: false,
    sessions: "50 concurrent", objects: "500 per link", ingest: "1,000 rows/s",
    footprint: "about 1 TB of source",
    connectors: "Adds Oracle and historians",
    deployment: "Adds on-premise",
    hardware: [
      ["Deployment", "Application host, database host, and an ML and serving host"],
      ["App host", "8 cores / 32 GB"],
      ["Database host", "16 cores / 64 GB"],
      ["ML and serving host", "8 cores / 32 GB, 16 GB reserved for serving"],
      ["Storage", "8 TB, 20,000 IOPS, partitioned and compressed"],
      ["Object storage", "2 TB"],
      ["Backup", "4 TB with point-in-time recovery"],
      ["Network", "1 Gbit/s"],
      ["Workers", "4 instances, ML isolated on its own host"],
    ],
    suits: "A whole plant with prediction, practice learning and the assistant.",
  },
  {
    name: "Enterprise",
    users: Infinity, pages: Infinity, jobs: Infinity, links: Infinity,
    retainedGb: Infinity, refreshMin: 0.25,
    stats: true, ml: true, enterprise: true,
    sessions: "200 and above", objects: "unlimited", ingest: "above 1,000 rows/s",
    footprint: "above 1 TB of source",
    connectors: "All classes",
    deployment: "Adds air-gapped",
    hardware: [
      ["Deployment", "Load-balanced application tier, primary and replica database, worker fleet, dedicated ML nodes"],
      ["App tier", "4 instances of 4 cores / 16 GB behind a balancer"],
      ["Database primary", "32 cores / 128 GB on NVMe"],
      ["Database replica", "Same shape, serving the interactive read path"],
      ["Worker fleet", "3 hosts of 8 cores / 32 GB, plus 2 ML hosts of 8 cores / 64 GB"],
      ["Storage", "50 TB and above, tiered, partitioned, compressed"],
      ["Object storage", "20 TB and above"],
      ["Backup", "Full point-in-time recovery, off-site copy, automatic failover"],
      ["Network", "10 Gbit/s"],
      ["High availability", "Synchronous replica in a separate failure domain"],
    ],
    suits: "Multi-site, air-gapped, or very high ingest.",
  },
];

/* Each dimension carries its four tier boundaries, taken straight from 6.3.4a,
 * so the slider can show WHERE the line is rather than only what the tier is. */
const DIMENSIONS = [
  { key: "users", label: "Named users", min: 1, max: 400, step: 1, start: 25, unit: "",
    bounds: [5, 25, 100, 400] },
  { key: "pages", label: "Pages and dashboards", min: 1, max: 500, step: 1, start: 60, unit: "",
    bounds: [15, 100, 400, 500] },
  { key: "jobs", label: "Scheduled jobs", min: 1, max: 200, step: 1, start: 10, unit: "",
    bounds: [5, 25, 100, 200] },
  { key: "links", label: "Database links", min: 1, max: 20, step: 1, start: 3, unit: "",
    bounds: [1, 3, 10, 20] },
  { key: "retainedGb", label: "History kept online", min: 50, max: 8000, step: 50, start: 900, unit: "GB",
    bounds: [250, 1024, 5120, 8000] },
];

const REFRESH_CHOICES = [
  { label: "Once an hour", minutes: 60, tier: 0 },
  { label: "Every 3 minutes", minutes: 3, tier: 1 },
  { label: "Every minute", minutes: 1, tier: 2 },
  { label: "Every 15 seconds", minutes: 0.25, tier: 3 },
];

const TIER_NAMES = ["Light", "Pro", "Pro Plus", "Enterprise"];

/* Which tier band does this value fall in? The first boundary it fits under. */
function bandOf(value: number, bounds: number[]) {
  for (let i = 0; i < bounds.length; i++) {
    if (value <= bounds[i]) return i;
  }
  return bounds.length - 1;
}

function TierBands({ value, dim }: { value: number; dim: (typeof DIMENSIONS)[number] }) {
  const band = bandOf(value, dim.bounds);
  const span = dim.max - dim.min;
  return (
    <div className="deck-bands" aria-hidden="true">
      {dim.bounds.map((b, i) => {
        const from = i === 0 ? dim.min : dim.bounds[i - 1];
        const width = Math.max(0, ((b - from) / span) * 100);
        return (
          <span
            key={i}
            className={i === band ? `deck-band deck-band--t${i} deck-band--on` : `deck-band deck-band--t${i}`}
            style={{ width: width + "%" }}
          >
            <b>{i === dim.bounds.length - 1 && dim.bounds[i] >= dim.max ? "more" : b}</b>
          </span>
        );
      })}
    </div>
  );
}

function PricingTab() {
  const [v, setV] = useState<Record<string, number>>(
    () => Object.fromEntries(DIMENSIONS.map((d) => [d.key, d.start])),
  );
  const [refresh, setRefresh] = useState(3);
  const [needStats, setNeedStats] = useState(true);
  const [needMl, setNeedMl] = useState(true);
  const [needEnt, setNeedEnt] = useState(false);

  /* 6.3.6 STEP 1: the LOWEST tier satisfying every dimension. One dimension over
   * promotes the tier - the same worst-dimension rule the sizing model uses.
   * There are no weights in this model: whichever dimension needs the highest
   * tier is the one that decides, and that is what gets marked. */
  const need = useMemo(() => {
    const perDim: { key: string; label: string; band: number }[] = DIMENSIONS.map((d) => ({
      key: d.key,
      label: d.label,
      band: bandOf(v[d.key], d.bounds),
    }));
    const refreshTier = REFRESH_CHOICES.find((r) => r.minutes === refresh)?.tier ?? 1;
    perDim.push({ key: "refresh", label: "Refresh interval", band: refreshTier });
    if (needStats) perDim.push({ key: "stats", label: "Statistics and SQL", band: 1 });
    if (needMl) perDim.push({ key: "ml", label: "Machine learning and assistant", band: 2 });
    if (needEnt) perDim.push({ key: "ent", label: "Single sign-on, air-gap or HA", band: 3 });
    const top = Math.max(...perDim.map((p) => p.band));
    const binding = perDim.filter((p) => p.band === top);
    return { tierIndex: top, binding, perDim };
  }, [v, refresh, needStats, needMl, needEnt]);

  const t = TIERS[need.tierIndex];
  const cap = (n: number) => (n === Infinity ? "unlimited" : String(n));
  const bindingKeys = new Set(need.binding.map((b) => b.key));

  return (
    <div>
      <p className="eyebrow rv">DEPLOYMENT AND LICENCE</p>
      <h2 className="rv">It runs on your infrastructure.</h2>
      <p className="lead rv">
        Containerised on your servers or your private cloud. No plant data leaves your network, so
        there is no hosting cost on my side and no data residency question on yours. The licence is a
        function of six things you already know about your own plant - and the tier is derived from
        them rather than chosen, so the same inputs always give the same answer.
      </p>

      <div className="deck-calc rv">
        <div className="deck-calc-inputs">
          <p className="deck-calc-head">Your plant, in six dimensions</p>
          {DIMENSIONS.map((d) => {
            const band = bandOf(v[d.key], d.bounds);
            const isBinding = bindingKeys.has(d.key);
            return (
              <label className={isBinding ? "deck-factor deck-factor--binding" : "deck-factor"} key={d.key}>
                <span className="deck-factor-top">
                  <span>{d.label}</span>
                  <strong>{v[d.key]} {d.unit}</strong>
                </span>
                <input
                  type="range"
                  min={d.min}
                  max={d.max}
                  step={d.step}
                  value={v[d.key]}
                  onChange={(e) => setV((p) => ({ ...p, [d.key]: Number(e.target.value) }))}
                />
                <TierBands value={v[d.key]} dim={d} />
                <span className="deck-factor-band">
                  <span className={`deck-dotb deck-dotb--t${band}`} />
                  {TIER_NAMES[band]}
                  {isBinding ? <em>this sets your tier</em> : null}
                </span>
              </label>
            );
          })}

          <p className="deck-calc-sub">How often the data must refresh</p>
          <div className="deck-chips">
            {REFRESH_CHOICES.map((r) => (
              <button
                key={r.label}
                type="button"
                className={refresh === r.minutes ? "deck-chip deck-chip--on" : "deck-chip"}
                onClick={() => setRefresh(r.minutes)}
              >
                {r.label}
                <i className={`deck-dotb deck-dotb--t${r.tier}`} />
              </button>
            ))}
          </div>

          <p className="deck-calc-sub">What you need it to do</p>
          <div className="deck-chips">
            <button type="button" className={needStats ? "deck-chip deck-chip--on" : "deck-chip"}
              onClick={() => setNeedStats(!needStats)}>
              Statistics and SQL<i className="deck-dotb deck-dotb--t1" />
            </button>
            <button type="button" className={needMl ? "deck-chip deck-chip--on" : "deck-chip"}
              onClick={() => setNeedMl(!needMl)}>
              Machine learning and assistant<i className="deck-dotb deck-dotb--t2" />
            </button>
            <button type="button" className={needEnt ? "deck-chip deck-chip--on" : "deck-chip"}
              onClick={() => setNeedEnt(!needEnt)}>
              Single sign-on, air-gap or HA<i className="deck-dotb deck-dotb--t3" />
            </button>
          </div>

          <p className="deck-legend">
            {TIER_NAMES.map((n, i) => (
              <span key={n}><i className={`deck-dotb deck-dotb--t${i}`} />{n}</span>
            ))}
          </p>
        </div>

        <div className="deck-calc-out">
          <p className="deck-calc-head">Your tier</p>
          <p className="deck-tiername">{t.name}</p>
          <p className="deck-promote">
            {need.binding.length === 1
              ? need.binding[0].label + " is what puts you here."
              : need.binding.map((b) => b.label).join(" and ") + " put you here."}
          </p>
          <p className="deck-rule">
            No dimension counts more than another. The tier is the lowest one that satisfies every
            single dimension, so whichever needs the most decides - move it down and the tier follows.
          </p>

          <table className="deck-env">
            <tbody>
              <tr><td>Named users</td><td>{cap(t.users)}</td></tr>
              <tr><td>Pages</td><td>{cap(t.pages)}</td></tr>
              <tr><td>Jobs</td><td>{cap(t.jobs)}</td></tr>
              <tr><td>Database links</td><td>{cap(t.links)}</td></tr>
              <tr><td>Retained volume</td><td>{t.retainedGb === Infinity ? "20 TB and above" : t.retainedGb >= 1024 ? (t.retainedGb / 1024) + " TB" : t.retainedGb + " GB"}</td></tr>
              <tr><td>Fastest refresh</td><td>{t.refreshMin >= 1 ? t.refreshMin + " min" : "15 s"}</td></tr>
              <tr><td>Concurrent sessions</td><td>{t.sessions}</td></tr>
              <tr><td>Objects per link</td><td>{t.objects}</td></tr>
              <tr><td>Ingest rate</td><td>{t.ingest}</td></tr>
              <tr><td>Source footprint served</td><td>{t.footprint}</td></tr>
              <tr><td>Connectors</td><td>{t.connectors}</td></tr>
              <tr><td>Deployment</td><td>{t.deployment}</td></tr>
            </tbody>
          </table>
          <p className="deck-note">{t.suits}</p>
        </div>
      </div>

      <p className="eyebrow rv deck-spaced">THE SERVER THIS TIER NEEDS</p>
      <h2 className="rv">Derived from the workload, never chosen.</h2>
      <p className="lead rv">
        Every figure below comes from the sizing model evaluated at this tier's capacity envelope, with
        thirty percent twelve-month growth headroom already included. An on-premise customer receives
        exactly the same specification - the product does not run better on our hardware than on yours.
      </p>
      <div className="deck-grid deck-grid--3 rv">
        {t.hardware.map((row) => (
          <article className="pf-card deck-hw" key={row[0]}>
            <p className="pf-value">{row[0]}</p>
            <p className="pf-row">{row[1]}</p>
          </article>
        ))}
      </div>

      <div className="deck-grid deck-grid--3 rv deck-spaced">
        <article className="pf-card">
          <h3>Counts guide, meters govern</h3>
          <p className="pf-row">
            The six dimensions decide the tier. Underneath, the platform meters what is actually
            consumed - retained volume, ingest rate, refresh floor, compute slots, concurrent sessions -
            and both are visible to you, so an upgrade conversation starts from measured facts.
          </p>
        </article>
        <article className="pf-card">
          <h3>Guardrails throttle, never destroy</h3>
          <p className="pf-row">
            At eighty percent of a count you get a warning and work continues. Beyond an envelope an
            import queues and a job waits for a slot. Nothing is deleted, and your data is never
            destroyed by an expiry or a downgrade - capability is withdrawn, content becomes read-only.
          </p>
        </article>
        <article className="pf-card">
          <h3>What is never limited</h3>
          <p className="pf-row">
            Master items, relationships, bookmarks, saved views, definition versions, genealogy depth,
            and evidence and drill-through at any tier. Charging for the mechanisms that make authoring
            safe would sell you a worse product.
          </p>
        </article>
      </div>

      <p className="deck-aside rv">
        What this page does not show is a price, and that is deliberate. The commercial figure is built
        from the tier, the capacity actually consumed, the deployment mode and the support level, and it
        comes to you in a written proposal after a short sizing workshop - so it is a number you can
        rely on rather than one guessed in a meeting.
      </p>
    </div>
  );
}

/* ---------------------------------------------------------------- PAGE ---- */

export function DeckPage() {
  const [tab, setTab] = useState<TabKey>("me");

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
      { threshold: 0.12, rootMargin: "0px 0px -40px 0px" },
    );
    document.querySelectorAll(".rv").forEach((el) => io.observe(el));
    return () => io.disconnect();
  }, [tab]);

  return (
    <div className="new-landing-wrapper deck">
      <section className="section deck-top">
        <div className="wrap">
          <div className="deck-tabs" role="tablist" aria-label="Presentation sections">
            {TABS.map((t) => (
              <button
                key={t.key}
                type="button"
                role="tab"
                aria-selected={tab === t.key}
                className={tab === t.key ? "deck-tab deck-tab--on" : "deck-tab"}
                onClick={() => setTab(t.key)}
              >
                {t.label}
              </button>
            ))}
          </div>

          <div className="deck-panel" key={tab}>
            {tab === "me" ? <MeTab /> : null}
            {tab === "application" ? <ApplicationTab /> : null}
            {tab === "tutorial" ? <TutorialTab /> : null}
            {tab === "pricing" ? <PricingTab /> : null}
          </div>
        </div>
      </section>
    </div>
  );
}