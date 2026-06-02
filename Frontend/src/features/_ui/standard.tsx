/* PlantProcess IQ - local presentational primitives.
   REPLACE these with your real standard component set (Appendix C tokens). They exist so the
   Phase-2 surfaces are self-contained and type-check on their own. */
import React from "react";

export const tokens = {
  bg: "var(--ppiq-bg, #0b1730)", surface: "var(--ppiq-surface, #102a43)",
  ink: "var(--ppiq-ink, #e8eef6)", sub: "var(--ppiq-sub, #9fb0c3)",
  line: "var(--ppiq-line, #1e3550)", accent: "var(--ppiq-accent, #0a84ff)",
  ok: "var(--ppiq-ok, #2ce6a2)", warn: "var(--ppiq-warn, #ffb020)", bad: "var(--ppiq-bad, #ff4d6d)",
};

export function Button(
  { children, onClick, kind = "primary", disabled, title }:
  { children: React.ReactNode; onClick?: () => void; kind?: "primary" | "ghost" | "danger"; disabled?: boolean; title?: string }
) {
  const bg = kind === "primary" ? tokens.accent : kind === "danger" ? tokens.bad : "transparent";
  return (
    <button title={title} disabled={disabled} onClick={onClick}
      style={{ background: bg, color: kind === "ghost" ? tokens.ink : "#04101f", border: `1px solid ${tokens.line}`,
        borderRadius: 8, padding: "8px 14px", font: "600 13px Inter, sans-serif",
        cursor: disabled ? "not-allowed" : "pointer", opacity: disabled ? 0.5 : 1 }}>
      {children}
    </button>
  );
}

export function Card({ title, children, right }: { title?: string; children: React.ReactNode; right?: React.ReactNode }) {
  return (
    <section style={{ background: tokens.surface, border: `1px solid ${tokens.line}`, borderRadius: 12, padding: 16 }}>
      {(title || right) && (
        <header style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 10 }}>
          <h3 style={{ margin: 0, font: "600 14px Inter", color: tokens.ink }}>{title}</h3>{right}
        </header>
      )}
      {children}
    </section>
  );
}

export function Badge({ children, tone = "info" }: { children: React.ReactNode; tone?: "info" | "ok" | "warn" | "bad" }) {
  const c = tone === "ok" ? tokens.ok : tone === "warn" ? tokens.warn : tone === "bad" ? tokens.bad : tokens.accent;
  return <span style={{ border: `1px solid ${c}`, color: c, borderRadius: 999, padding: "2px 8px", font: "600 11px Inter", whiteSpace: "nowrap" }}>{children}</span>;
}

export function Spinner({ label }: { label?: string }) {
  return (
    <div style={{ color: tokens.sub, font: "13px Inter", display: "flex", gap: 8, alignItems: "center" }}>
      <span style={{ width: 14, height: 14, border: `2px solid ${tokens.line}`, borderTopColor: tokens.accent,
        borderRadius: "50%", display: "inline-block", animation: "ppiq-spin 0.8s linear infinite" }} />
      {label ?? "Loading..."}
      <style>{"@keyframes ppiq-spin{to{transform:rotate(360deg)}}"}</style>
    </div>
  );
}

export function EmptyState({ title, hint, action }: { title: string; hint?: string; action?: React.ReactNode }) {
  return (
    <div style={{ textAlign: "center", padding: "28px 12px", color: tokens.sub }}>
      <div style={{ font: "600 14px Inter", color: tokens.ink }}>{title}</div>
      {hint && <div style={{ marginTop: 6, font: "13px Inter" }}>{hint}</div>}
      {action && <div style={{ marginTop: 12 }}>{action}</div>}
    </div>
  );
}

export function Tabs(
  { tabs, active, onSelect }:
  { tabs: { id: string; label: string; dirty?: boolean }[]; active: string; onSelect: (id: string) => void }
) {
  return (
    <div role="tablist" style={{ display: "flex", gap: 4, borderBottom: `1px solid ${tokens.line}`, flexWrap: "wrap" }}>
      {tabs.map((t) => (
        <button key={t.id} role="tab" aria-selected={t.id === active} onClick={() => onSelect(t.id)}
          style={{ background: "transparent", border: "none",
            borderBottom: `2px solid ${t.id === active ? tokens.accent : "transparent"}`,
            color: t.id === active ? tokens.ink : tokens.sub, padding: "10px 14px", font: "600 13px Inter", cursor: "pointer" }}>
          {t.label}{t.dirty && <span style={{ color: tokens.warn }}> &bull;</span>}
        </button>
      ))}
    </div>
  );
}