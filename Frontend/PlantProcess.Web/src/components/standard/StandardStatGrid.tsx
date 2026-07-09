// ============================================================
// FILE: src/components/standard/StandardStatGrid.tsx
// A grid of labelled figures.
//
// Replaces the hand-rolled `<div><span>{label}</span><strong>{value}</strong></div>`
// that shipped on eight pages with no class and no style, which is why the HMI
// rendered "Median107" and "Modeprivacy-preserving-...".
//
// - label sits ABOVE the value, never beside it, so they can never collide
// - values use tabular numerals so columns of figures line up
// - `emphasis` promotes the one figure the operator acts on
// - `tone` colours a value without inventing new colours (tokens only)
// ============================================================
import type { ReactNode } from "react";
import "./standard-components.css";

export type StandardStatTone = "neutral" | "good" | "warn" | "bad";

export type StandardStatItem = {
  label: string;
  value: ReactNode;
  hint?: string;
  tone?: StandardStatTone;
  emphasis?: boolean;
};

export type StandardStatGridProps = {
  items: ReadonlyArray<StandardStatItem>;
  /**
   * Label of the single item to promote. Matched case-insensitively as a PREFIX,
   * so `emphasize="Realized"` also promotes a label rendered as "Realized EUR"
   * or "Realized \u20AC". Exact matching broke on currency-suffixed labels.
   */
  emphasize?: string;
  ariaLabel?: string;
};

export function StandardStatGrid({ items, emphasize, ariaLabel }: StandardStatGridProps) {
  if (!items || items.length === 0) return null;

  const key = emphasize ? emphasize.trim().toLowerCase() : null;

  return (
    <dl className="ppiq-std-stat-grid" aria-label={ariaLabel ?? "Key figures"}>
      {items.map((item) => {
        const promoted =
          item.emphasis === true ||
          (key !== null && item.label.trim().toLowerCase().startsWith(key));
        const tone = item.tone ?? "neutral";
        const cls =
          "ppiq-std-stat" +
          (promoted ? " ppiq-std-stat--emphasis" : "") +
          " ppiq-std-stat--" + tone;

        return (
          <div className={cls} key={item.label}>
            <dt className="ppiq-std-stat__label">{item.label}</dt>
            <dd className="ppiq-std-stat__value">{item.value}</dd>
            {item.hint ? <p className="ppiq-std-stat__hint">{item.hint}</p> : null}
          </div>
        );
      })}
    </dl>
  );
}