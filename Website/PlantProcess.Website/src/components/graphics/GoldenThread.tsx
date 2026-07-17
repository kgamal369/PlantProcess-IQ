import { CircleDot, Factory, Layers3, ScanLine } from "lucide-react";

const stages = [
  { code: "H-26014", label: "Meltshop heat", meta: "Chemistry · superheat · treatment", icon: Factory },
  { code: "SL-60105", label: "Caster slab", meta: "Casting speed · width · cooling", icon: Layers3 },
  { code: "C-700394", label: "Rolled coil", meta: "Force · temperature · gauge", icon: CircleDot },
  { code: "CRACK_LONG", label: "Surface inspection", meta: "Position · severity · extent", icon: ScanLine },
];

export function GoldenThread() {
  return (
    <div className="golden-thread" role="img" aria-labelledby="golden-thread-title golden-thread-desc">
      <span id="golden-thread-title" className="sr-only">Golden thread production genealogy</span>
      <span id="golden-thread-desc" className="sr-only">
        A traceable production chain connects heat H-26014 to slab SL-60105, coil C-700394, and a CRACK_LONG surface inspection event.
      </span>
      <div className="golden-thread__line" aria-hidden="true" />
      {stages.map(({ code, label, meta, icon: Icon }, index) => (
        <article className="golden-thread__stage" style={{ "--stage-index": index } as React.CSSProperties} key={code}>
          <div className="golden-thread__node"><Icon size={25} /></div>
          <div className="golden-thread__copy">
            <span>0{index + 1}</span>
            <strong>{code}</strong>
            <h3>{label}</h3>
            <p>{meta}</p>
            <small>Source + batch + timestamp + provenance</small>
          </div>
        </article>
      ))}
    </div>
  );
}

export default GoldenThread;
