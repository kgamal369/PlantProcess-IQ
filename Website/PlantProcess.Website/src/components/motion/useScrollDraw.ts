import { useEffect, useRef } from "react";

/**
 * Scroll-bound SVG path drawing.
 * Attach the returned ref to an <svg>. Every <path>, <line> and <polyline>
 * carrying data-draw inside it draws itself as the svg traverses the viewport.
 * Draw-only (never un-draws), rAF-throttled; prefers-reduced-motion renders
 * everything fully drawn and static.
 */
export function useScrollDraw<T extends SVGSVGElement>() {
  const ref = useRef<T | null>(null);

  useEffect(() => {
    const svg = ref.current;
    if (!svg) return;

    const els = Array.from(
      svg.querySelectorAll<SVGGeometryElement>("[data-draw]")
    );
    if (els.length === 0) return;

    const lengths = els.map((el) => {
      const len = el.getTotalLength ? el.getTotalLength() : 0;
      el.style.strokeDasharray = `${len}`;
      el.style.strokeDashoffset = `${len}`;
      return len;
    });

    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)");
    if (reduced.matches) {
      els.forEach((el) => { el.style.strokeDashoffset = "0"; });
      return;
    }

    let raf = 0;
    let done = false;
    const ease = (t: number) => 1 - Math.pow(1 - t, 3);

    const update = () => {
      raf = 0;
      if (done) return;
      const r = svg.getBoundingClientRect();
      const vh = window.innerHeight;
      const raw = (vh - r.top) / (vh * 0.62 + r.height * 0.5);
      const p = ease(Math.min(1, Math.max(0, raw)));
      els.forEach((el, i) => {
        const current = parseFloat(el.style.strokeDashoffset || "0");
        const target = lengths[i] * (1 - p);
        if (target < current) el.style.strokeDashoffset = `${target}`;
      });
      if (p >= 1) {
        done = true;
        window.removeEventListener("scroll", onScroll);
      }
    };
    const onScroll = () => { if (!raf) raf = requestAnimationFrame(update); };

    window.addEventListener("scroll", onScroll, { passive: true });
    update();
    return () => {
      window.removeEventListener("scroll", onScroll);
      if (raf) cancelAnimationFrame(raf);
    };
  }, []);

  return ref;
}