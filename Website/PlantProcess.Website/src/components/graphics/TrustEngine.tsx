import { BadgeCheck, BrainCircuit, Calculator, FileSearch, Link2, ShieldAlert } from "lucide-react";

export function TrustEngine() {
  return (
    <div className="trust-engine" role="img" aria-labelledby="trust-engine-title trust-engine-desc">
      <span id="trust-engine-title" className="sr-only">Deterministic engine and grounded assistant separation</span>
      <span id="trust-engine-desc" className="sr-only">
        A deterministic statistical engine computes findings and stores evidence. A separate grounded assistant retrieves that evidence, explains it with citations, and refuses unsupported claims.
      </span>

      <section className="trust-engine__lane trust-engine__lane--compute" aria-hidden="true">
        <div className="trust-engine__icon"><Calculator size={28} /></div>
        <span className="trust-engine__eyebrow">COMPUTE</span>
        <h3>Deterministic Engine</h3>
        <ul>
          <li>Reconstructs genealogy</li>
          <li>Selects governed methods</li>
          <li>Calculates effect and q-value</li>
          <li>Ranks evidence, not opinions</li>
        </ul>
      </section>

      <section className="trust-engine__hub" aria-hidden="true">
        <div className="trust-engine__hub-ring" />
        <div className="trust-engine__hub-core"><Link2 size={27} /></div>
        <strong>Evidence Store</strong>
        <small>Finding · population · method · run · source</small>
        <div className="trust-engine__verified"><BadgeCheck size={16} /> Resolvable handles</div>
      </section>

      <section className="trust-engine__lane trust-engine__lane--explain" aria-hidden="true">
        <div className="trust-engine__icon"><BrainCircuit size={28} /></div>
        <span className="trust-engine__eyebrow">EXPLAIN</span>
        <h3>Grounded Assistant</h3>
        <ul>
          <li>Retrieves approved evidence</li>
          <li>Explains in plant language</li>
          <li>Cites every numeric claim</li>
          <li>Refuses when evidence is absent</li>
        </ul>
      </section>

      <div className="trust-engine__rule trust-engine__rule--one" aria-hidden="true"><FileSearch size={15} /> Evidence only</div>
      <div className="trust-engine__rule trust-engine__rule--two" aria-hidden="true"><ShieldAlert size={15} /> LLM never calculates</div>
    </div>
  );
}

export default TrustEngine;
