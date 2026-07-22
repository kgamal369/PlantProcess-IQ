import { useScrollDraw } from "../motion/useScrollDraw";

/**
 * Scroll-drawn Golden Thread: HEAT -> SLAB -> COIL -> EVIDENCE.
 * The lineage draws itself as the reader scrolls; nodes fade in after
 * their segment completes (CSS-driven via the same progress classes).
 * Quiet by design: single stroke, no loops, brand tokens only.
 */
export function GoldenThreadScroll() {
  const ref = useScrollDraw<SVGSVGElement>();
  return (
    <svg
      ref={ref}
      viewBox="0 0 1120 220"
      width="100%"
      role="img"
      aria-label="A heat flows to a slab and then a coil; the thread ends in quality evidence."
      className="ppiq-goldenthread"
    >
      <defs>
        <linearGradient id="gt-grad" x1="0" x2="1">
          <stop offset="0" stopColor="var(--sou-blue, #0a84ff)" />
          <stop offset="1" stopColor="var(--sou-cyan, #00d4ff)" />
        </linearGradient>
      </defs>

      <path
        data-draw
        d="M80 120 H340 M420 120 H640 M720 120 H930"
        fill="none"
        stroke="url(#gt-grad)"
        strokeWidth="2.2"
        strokeLinecap="round"
      />

      <circle cx="80" cy="120" r="24" fill="none" stroke="var(--sou-blue, #0a84ff)" strokeWidth="1.6" data-draw />
      <text x="80" y="168" textAnchor="middle" className="gt-label">HEAT</text>
      <text x="80" y="184" textAnchor="middle" className="gt-id">H-2214</text>

      <rect x="340" y="98" width="80" height="44" rx="6" fill="none" stroke="var(--sou-blue, #0a84ff)" strokeWidth="1.4" data-draw />
      <text x="380" y="168" textAnchor="middle" className="gt-label">SLAB</text>
      <text x="380" y="184" textAnchor="middle" className="gt-id">S-88410</text>

      <circle cx="680" cy="120" r="26" fill="none" stroke="var(--sou-cyan, #00d4ff)" strokeWidth="1.6" data-draw />
      <circle cx="680" cy="120" r="15" fill="none" stroke="var(--sou-cyan, #00d4ff)" strokeWidth="1" opacity=".6" data-draw />
      <text x="680" y="168" textAnchor="middle" className="gt-label">COIL</text>
      <text x="680" y="184" textAnchor="middle" className="gt-id">C-710909</text>

      <rect x="930" y="74" width="160" height="94" rx="8" fill="none" stroke="var(--sou-green, #2ce6a2)" strokeWidth="1.5" data-draw />
      <text x="1010" y="100" textAnchor="middle" className="gt-label" fill="var(--sou-green, #2ce6a2)">EVIDENCE</text>
      <text x="948" y="124" className="gt-ev">cause: superheat window</text>
      <text x="948" y="142" className="gt-ev">source: L2_CASTER</text>
      <text x="948" y="160" className="gt-ev">batch: IMP-2026-118</text>
    </svg>
  );
}