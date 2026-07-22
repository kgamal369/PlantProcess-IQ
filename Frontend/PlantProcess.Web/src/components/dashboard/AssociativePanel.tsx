import { useState } from "react";
import { AssociativeProvider, useAssociative } from "../../state/AssociativeContext";
import "./associative.css";

/** M2-37: the green-white-grey strip. Additive + behind its own toggle:
 * mounts under the global filters without touching the existing bar. */
function PanelInner() {
  const { enabled, setEnabled, fields, toggleValue } = useAssociative();
  const [open, setOpen] = useState(true);
  return (
    <section className="assoc" aria-label="Associative selection view">
      <header className="assoc__head">
        <button className="assoc__toggle" onClick={() => setOpen((o) => !o)} aria-expanded={open}>
          {open ? "\u25BE" : "\u25B8"} ASSOCIATIVE VIEW
        </button>
        <span className="assoc__legend">
          <i className="lg lg--sel" /> selected <i className="lg lg--pos" /> possible <i className="lg lg--exc" /> excluded
        </span>
        <label className="assoc__enable">
          <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} /> live
        </label>
      </header>
      {open && (
        <div className="assoc__grid">
          {fields.map((fa) => (
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
                  return (
                    <button
                      key={v}
                      className={`assoc__chip assoc__chip--${st}`}
                      onClick={() => toggleValue(fa.field.key, v)}
                      title={`${fa.field.label}: ${v} (${st})`}
                    >
                      {v}
                    </button>
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