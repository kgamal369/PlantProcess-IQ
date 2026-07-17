import { CheckCircle2, DatabaseZap, LineChart, SearchCheck } from "lucide-react";

const steps = [
  { step: "01", title: "Choose one costly outcome", text: "A recurring defect, downgrade pattern, claim, downtime mode or performance loss.", icon: SearchCheck },
  { step: "02", title: "Connect approved sources", text: "Read-only access to the minimum relevant data—no plant remodel and no control write-back.", icon: DatabaseZap },
  { step: "03", title: "Recover or reject the signal", text: "Reconstruct genealogy, run governed analysis and expose the method, population and evidence.", icon: LineChart },
  { step: "04", title: "Make the go / no-go decision", text: "Scale only when the plant’s own data supports a measurable operational and financial case.", icon: CheckCircle2 },
];

export function ProofOfValueJourney() {
  return (
    <section className="commercial-section pov-section" id="proof-of-value">
      <div className="section-shell pov-shell">
        <div className="pov-copy">
          <div className="section-kicker">Proof of Value</div>
          <h2>Do not buy a promise. Prove one expensive problem.</h2>
          <p>
            The validation environment proves the engine. A focused pilot proves whether the signal exists in your plant—and whether acting on it creates enough value to scale.
          </p>
          <a className="website-button website-button--primary" href="#request-demo">Initiate Proof of Value</a>
        </div>

        <div className="pov-steps">
          {steps.map(({ step, title, text, icon: Icon }) => (
            <article key={step}>
              <span>{step}</span>
              <div className="pov-steps__icon"><Icon size={22} /></div>
              <div>
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

export default ProofOfValueJourney;
