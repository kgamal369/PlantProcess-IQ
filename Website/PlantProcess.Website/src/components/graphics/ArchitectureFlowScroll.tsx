import { useScrollDraw } from "../motion/useScrollDraw";

/** Scroll-drawn architecture: sources -> read-only link -> unified model -> intelligence. */
export function ArchitectureFlowScroll() {
  const ref = useScrollDraw<SVGSVGElement>();
  const box = "var(--sou-panel-2, #102a43)";
  const line = "#1d3a63";
  return (
    <svg
      ref={ref}
      viewBox="0 0 1120 300"
      width="100%"
      role="img"
      aria-label="Plant systems flow through a read-only link into one unified model and out to dashboards, predictions and recommendations."
      className="ppiq-archflow"
    >
      {[
        ["Level 2 / L2 DB", 26],
        ["SAP / ERP", 82],
        ["L1 Sensors / Historian", 138],
        ["Quality / LIMS / Inspection", 194],
      ].map(([label, y]) => (
        <g key={label as string}>
          <rect x="20" y={y as number} width="200" height="40" rx="7" fill={box} stroke={line} />
          <text x="120" y={(y as number) + 25} textAnchor="middle" className="af-t">{label}</text>
        </g>
      ))}

      <path data-draw d="M220 46 H300 M220 102 H300 M220 158 H300 M220 214 H300"
        fill="none" stroke="var(--sou-cyan, #00d4ff)" strokeWidth="2" strokeLinecap="round" />

      <rect x="300" y="96" width="150" height="72" rx="8" fill={box} stroke="var(--sou-cyan, #00d4ff)" />
      <text x="375" y="126" textAnchor="middle" className="af-t">READ-ONLY LINK</text>
      <text x="375" y="146" textAnchor="middle" className="af-s">observes, never commands</text>

      <path data-draw d="M450 132 H540" fill="none" stroke="var(--sou-cyan, #00d4ff)" strokeWidth="2" strokeLinecap="round" />

      <rect x="540" y="86" width="180" height="92" rx="8" fill={box} stroke="var(--sou-blue, #0a84ff)" />
      <text x="630" y="118" textAnchor="middle" className="af-t">UNIFIED PLANT MODEL</text>
      <text x="630" y="138" textAnchor="middle" className="af-s">full material genealogy</text>
      <text x="630" y="156" textAnchor="middle" className="af-s">every row keeps its source</text>

      <path data-draw d="M720 110 H820 M720 132 H820 M720 154 H820"
        fill="none" stroke="var(--sou-green, #2ce6a2)" strokeWidth="2" strokeLinecap="round" />

      {[
        ["Dashboards", 88],
        ["Predictions &middot; AI+ML", 122],
        ["Recommendations", 156],
      ].map(([label, y]) => (
        <g key={label as string}>
          <rect x="820" y={(y as number)} width="270" height="30" rx="7" fill={box} stroke="var(--sou-green, #2ce6a2)" />
          <text x="955" y={(y as number) + 20} textAnchor="middle" className="af-t" fill="var(--sou-green, #2ce6a2)">{label}</text>
        </g>
      ))}
    </svg>
  );
}