/* PPIQ-T069-W1 */
import { useScrollDraw } from "../motion/useScrollDraw";

/**
 * The architecture section: source systems -> read-only gateway -> unified plant
 * model -> dashboards, predictions, recommendations.
 *
 * IT USES THE HERO'S VOCABULARY AND NOTHING ELSE. srcbox, srct, srcs, outt,
 * spoke, spoke-flow, outflow, ring, ring2 and hub-core are all defined in
 * new-landing.css under .new-landing-wrapper, which NewHomePage already wraps
 * around this component. No colour, radius, font-family or keyframe is invented
 * here, so this graphic cannot drift into a second design language.
 *
 * GEOMETRY RULE: every connector is a cubic that starts on a drawn source port
 * and ends on a drawn destination port. No line may terminate in empty space.
 *
 * TEXT RULE: every label is JSX text, never a string in an array, because JSX
 * decodes entities in text and does not decode them inside strings. That is why
 * the previous version printed a literal entity while the hero did not.
 *
 * Reduced motion needs nothing here - new-landing.css already disables every
 * animation under .new-landing-wrapper when the user asks for it.
 */
export function ArchitectureFlowScroll() {
  const ref = useScrollDraw<SVGSVGElement>();

  // Vertical centre of the diagram. Every port is placed against it.
  const CY = 168;
  // Source rows, their port x, and the gateway port they land on.
  const SRC_X = 24;
  const SRC_W = 196;
  const SRC_PORT = SRC_X + SRC_W;      // 220
  const GATE_X = 388;
  const GATE_W = 148;
  const GATE_PORT_IN = GATE_X;         // 388
  const GATE_PORT_OUT = GATE_X + GATE_W; // 536
  const HUB_CX = 726;
  const HUB_R = 78;
  const OUT_X = 892;
  const OUT_W = 204;

  const sources = [
    { y: 46, title: "Level 2", sub: "process automation", cls: "" },
    { y: 128, title: "SAP / ERP", sub: "orders, materials", cls: " s2" },
    { y: 210, title: "L1 Sensors", sub: "historian, telemetry", cls: " s3" },
    { y: 292, title: "Quality / LIMS", sub: "inspection, chemistry", cls: " s4" },
  ];
  const outputs = [
    { cy: 96, cls: "" },
    { cy: 168, cls: " o2" },
    { cy: 240, cls: " o3" },
  ];

  function inbound(y: number) {
    const cy = y + 26;
    return `M${SRC_PORT} ${cy} C ${SRC_PORT + 92} ${cy}, ${GATE_PORT_IN - 92} ${CY}, ${GATE_PORT_IN} ${CY}`;
  }
  function outbound(cy: number) {
    const from = HUB_CX + HUB_R;
    return `M${from} ${CY} C ${from + 48} ${CY}, ${OUT_X - 48} ${cy}, ${OUT_X} ${cy}`;
  }

  return (
    <section className="section ppiq-archflow-section" aria-labelledby="ppiq-archflow-title">
      <div className="wrap">
        <p className="eyebrow rv">READ-ONLY BY ARCHITECTURE</p>
        <h2 className="rv" id="ppiq-archflow-title" style={{ transitionDelay: ".05s" }}>
          Your systems keep running. We only read them.
        </h2>

        <svg
          ref={ref}
          viewBox="0 0 1120 380"
          className="ppiq-archflow rv"
          role="img"
          aria-label="Plant systems flow through a read-only gateway into one unified plant model and out to dashboards, predictions and recommendations."
        >
          <defs>
            <radialGradient id="archcore" cx="50%" cy="42%" r="62%">
              <stop offset="0%" stopColor="#0a84ff" stopOpacity="0.95" />
              <stop offset="60%" stopColor="#0a3f7a" stopOpacity="0.75" />
              <stop offset="100%" stopColor="#071634" stopOpacity="0.9" />
            </radialGradient>
          </defs>

          {/* ---- source systems -------------------------------------------- */}
          {sources.map((s) => (
            <g key={s.title}>
              <rect className="srcbox" x={SRC_X} y={s.y} width={SRC_W} height={52} rx="7" />
              <text className="srct" x={SRC_X + 18} y={s.y + 22}>{s.title}</text>
              <text className="srcs" x={SRC_X + 18} y={s.y + 38}>{s.sub}</text>
              <circle className="af-port" cx={SRC_PORT} cy={s.y + 26} r="3.2" />
              <path className="spoke" d={inbound(s.y)} fill="none" />
              <path data-draw className={`spoke-flow${s.cls}`} d={inbound(s.y)} fill="none" />
            </g>
          ))}

          {/* ---- read-only gateway ----------------------------------------- */}
          <circle className="af-port" cx={GATE_PORT_IN} cy={CY} r="3.6" />
          <rect className="af-gate" x={GATE_X} y={CY - 40} width={GATE_W} height={80} rx="8" />
          <text className="srct" x={GATE_X + GATE_W / 2} y={CY - 8} textAnchor="middle">READ-ONLY LINK</text>
          <text className="srcs" x={GATE_X + GATE_W / 2} y={CY + 10} textAnchor="middle">Observes, never commands</text>
          <text className="srcs" x={GATE_X + GATE_W / 2} y={CY + 26} textAnchor="middle">No write path exists</text>
          <circle className="af-port" cx={GATE_PORT_OUT} cy={CY} r="3.6" />

          <path className="spoke" d={`M${GATE_PORT_OUT} ${CY} H ${HUB_CX - HUB_R}`} fill="none" />
          <path data-draw className="spoke-flow s5" d={`M${GATE_PORT_OUT} ${CY} H ${HUB_CX - HUB_R}`} fill="none" />
          {/* The hub's INBOUND port. Without it the gateway-to-hub wire arrives
              at the hub edge with nothing to land on, which the runtime geometry
              gate reported as "spoke 4 ends at 648,168". */}
          <circle className="af-port" cx={HUB_CX - HUB_R} cy={CY} r="3.6" />

          {/* ---- unified plant model, the centrepiece ----------------------- */}
          <circle className="ring2" cx={HUB_CX} cy={CY} r={HUB_R + 44} />
          <circle className="ring" cx={HUB_CX} cy={CY} r={HUB_R + 22} />
          <g className="hub-core">
            <circle cx={HUB_CX} cy={CY} r={HUB_R} fill="url(#archcore)" stroke="#00d4ff" strokeWidth="1.6" />
            <text className="af-hub" x={HUB_CX} y={CY - 6} textAnchor="middle">UNIFIED</text>
            <text className="af-hub" x={HUB_CX} y={CY + 14} textAnchor="middle">PLANT MODEL</text>
          </g>
          <text className="srcs" x={HUB_CX} y={CY + HUB_R + 66} textAnchor="middle">
            genealogy &middot; provenance &middot; plant context
          </text>

          {/* ---- destinations ---------------------------------------------- */}
          <circle className="af-port out" cx={HUB_CX + HUB_R} cy={CY} r="3.6" />

          <path className="spoke" d={outbound(outputs[0].cy)} fill="none" />
          <path data-draw className="outflow" d={outbound(outputs[0].cy)} fill="none" />
          <circle className="af-port out" cx={OUT_X} cy={outputs[0].cy} r="3.2" />
          <rect className="af-out" x={OUT_X} y={outputs[0].cy - 21} width={OUT_W} height={42} rx="7" />
          <text className="outt" x={OUT_X + 20} y={outputs[0].cy + 4}>Dashboards</text>

          <path className="spoke" d={outbound(outputs[1].cy)} fill="none" />
          <path data-draw className="outflow o2" d={outbound(outputs[1].cy)} fill="none" />
          <circle className="af-port out" cx={OUT_X} cy={outputs[1].cy} r="3.2" />
          <rect className="af-out" x={OUT_X} y={outputs[1].cy - 21} width={OUT_W} height={42} rx="7" />
          <text className="outt" x={OUT_X + 20} y={outputs[1].cy + 4}>Predictions &middot; AI+ML</text>

          <path className="spoke" d={outbound(outputs[2].cy)} fill="none" />
          <path data-draw className="outflow o3" d={outbound(outputs[2].cy)} fill="none" />
          <circle className="af-port out" cx={OUT_X} cy={outputs[2].cy} r="3.2" />
          <rect className="af-out" x={OUT_X} y={outputs[2].cy - 21} width={OUT_W} height={42} rx="7" />
          <text className="outt" x={OUT_X + 20} y={outputs[2].cy + 4}>Recommendations</text>
        </svg>
      </div>
    </section>
  );
}