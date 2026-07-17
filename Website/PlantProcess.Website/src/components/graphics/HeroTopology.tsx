import { Database, FileSpreadsheet, FlaskConical, Gauge, LockKeyhole, Network, Radar, ShieldCheck } from "lucide-react";

const sources = [
  { label: "MES / ERP", icon: Database },
  { label: "L2 / SCADA", icon: Gauge },
  { label: "Historian", icon: Radar },
  { label: "Inspection", icon: Network },
  { label: "Lab / QMS", icon: FlaskConical },
  { label: "Files", icon: FileSpreadsheet },
];

export function HeroTopology() {
  return (
    <div className="hero-topology" role="img" aria-labelledby="hero-topology-title hero-topology-desc">
      <span id="hero-topology-title" className="sr-only">Read-only plant intelligence topology</span>
      <span id="hero-topology-desc" className="sr-only">
        Fragmented plant systems send approved read-only data through a secure boundary into PlantProcess IQ, which produces evidence, dashboards, alerts, and cited answers without sending commands back.
      </span>

      <div className="hero-topology__grid" aria-hidden="true" />
      <div className="hero-topology__sources" aria-hidden="true">
        {sources.map(({ label, icon: Icon }, index) => (
          <div className="source-chip" style={{ "--source-delay": `${index * 0.18}s` } as React.CSSProperties} key={label}>
            <Icon size={17} />
            <span>{label}</span>
          </div>
        ))}
      </div>

      <div className="hero-topology__flows" aria-hidden="true">
        {Array.from({ length: 6 }).map((_, index) => (
          <span className={`data-flow data-flow--${index + 1}`} key={index} />
        ))}
      </div>

      <div className="hero-topology__boundary" aria-hidden="true">
        <div className="boundary-pulse boundary-pulse--one" />
        <div className="boundary-pulse boundary-pulse--two" />
        <div className="boundary-shield">
          <LockKeyhole size={25} />
        </div>
        <strong>READ-ONLY</strong>
        <small>No setpoints · recipes · commands</small>
      </div>

      <div className="hero-topology__core" aria-hidden="true">
        <div className="core-orbit core-orbit--one" />
        <div className="core-orbit core-orbit--two" />
        <div className="core-logo">
          <ShieldCheck size={31} />
          <span>PPIQ</span>
        </div>
        <div className="core-caption">
          <strong>Evidence Engine</strong>
          <small>Map · trace · analyse · explain</small>
        </div>
      </div>

      <div className="hero-topology__outputs" aria-hidden="true">
        <span>Golden Thread</span>
        <span>Findings</span>
        <span>Alerts</span>
        <span>Cited Answers</span>
      </div>

      <div className="hero-topology__blocked" aria-hidden="true">
        <span>NO WRITE-BACK</span>
      </div>
    </div>
  );
}

export default HeroTopology;
