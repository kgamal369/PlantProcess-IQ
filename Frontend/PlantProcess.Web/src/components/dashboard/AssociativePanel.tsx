import { useState } from "react";
import { StandardP2Button } from "@/components/standard/StandardP2Controls";
import { AssociativeProvider, useAssociative } from "../../state/AssociativeContext";
import "./associative.css";

/** M2-37: the green-white-grey strip. Additive + behind its own toggle:
 * mounts under the global filters without touching the existing bar.
 * Design-system conformant: Standard* primitives, no raw controls. */
/**
 * DEMO-010. A 36-character identifier is not a label a plant manager can read.
 *
 * The dimension enumeration returns opaque values for entity dimensions, so a
 * chip showing 7922750e-2768-5083-9cc3-cc0ab890b32b tells the reader that the
 * system does not know their plant - when the database in fact holds
 * equipment_code HSM-01 and equipment_name "Hot strip mill" for that exact id.
 *
 * NO NAME IS INVENTED HERE. When a governed label is not carried by the value
 * itself, the identifier is shortened for display and the full value stays in
 * the title attribute, so nothing is hidden and nothing is fabricated. The
 * value passed to toggleValue is always the real one.
 */
const UUID_PATTERN =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

export function displayValue(value: string): string {
  if (!UUID_PATTERN.test(value)) return value;
  return value.slice(0, 8) + "\u2026";
}

function PanelInner() {
  const { enabled, setEnabled, fields, toggleValue } = useAssociative();

  // DEMO-006. The customer opens on business content, not on a full-width
  // dimension explorer. Collapsed is the presentation state; the toggle, the
  // legend, the live switch and every selection semantic are unchanged, and one
  // click restores the panel exactly as it was.
  const [open, setOpen] = useState(false);
  return (
    <section className="assoc" aria-label="Associative selection view">
      <header className="assoc__head">
        <StandardP2Button variant="ghost" className="assoc__toggle"
          onClick={() => setOpen((o) => { const next = !o; setEnabled(next); return next; })} aria-expanded={open}>
          {open ? "\u25BE" : "\u25B8"} ASSOCIATIVE VIEW
        </StandardP2Button>
        <span className="assoc__legend">
          <i className="lg lg--sel" /> selected <i className="lg lg--pos" /> possible{" "}
          <i className="lg lg--alt" /> alternative <i className="lg lg--exc" /> excluded
        </span>
        <StandardP2Button variant="ghost" className="assoc__enable"
          aria-pressed={enabled} onClick={() => setEnabled(!enabled)}>
          {enabled ? "live: on" : "live: off"}
        </StandardP2Button>
      </header>
      {open && (
        <div className="assoc__grid">
          {fields.filter((fa) => fa.available || fa.loading).map((fa) => (
            <div className="assoc__field" key={fa.field.key}>
              <div className="assoc__label">
                {fa.field.label}
                {fa.available
                  ? <span className="assoc__count">{fa.possibleCount}/{fa.all.length}</span>
                  : <span className="assoc__na">n/a</span>}
                {fa.loading && <span className="assoc__spin" aria-hidden="true" />}
              </div>
              <div className="assoc__values">
                {fa.all.slice(0, 40).map((v) => {
                  const st = fa.states.get(v) ?? "possible";
                  const shown = displayValue(v);
                  return (
                    <StandardP2Button key={v} variant="ghost"
                      className={"assoc__chip assoc__chip--" + st}
                      onClick={() => toggleValue(fa.field.key, v)}
                      title={fa.field.label + ": " + v + " (" + st + ")"}>
                      {shown}
                    </StandardP2Button>
                  );
                })}
                {fa.all.length > 40 && <span className="assoc__more">+{fa.all.length - 40}</span>}
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

export function AssociativePanel() {
  return (
    <AssociativeProvider>
      <PanelInner />
    </AssociativeProvider>
  );
}