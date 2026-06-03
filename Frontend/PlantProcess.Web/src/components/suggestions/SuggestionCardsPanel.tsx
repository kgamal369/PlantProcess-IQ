import { useMemo, useState } from "react";
import { StandardButton } from "@/components/standard";

export type ProvenanceHandleRef = { kind: string; id: string; detail?: string | null };

export type SuggestionCard = {
  id: string;
  title: string;
  actionType: string;
  status: string;
  confidence: number;
  impactLow?: number | null;
  impactHigh?: number | null;
  honestyText: string;
  unsupported?: boolean;
  evidence: ProvenanceHandleRef[];
  sourceFindings: string[];
  staleEvidence?: boolean;
  duplicateOf?: string | null;
};

type Role = "viewer" | "operator" | "engineer" | "admin";

const money = (v?: number | null) =>
  v == null ? "-" : new Intl.NumberFormat(undefined, { style: "currency", currency: "EUR", maximumFractionDigits: 0 }).format(v);

export function SuggestionCardsPanel({
  cards,
  role = "viewer",
  onOpenEvidence,
  onAct,
}: {
  cards: SuggestionCard[];
  role?: Role;
  onOpenEvidence?: (handle: ProvenanceHandleRef) => void;
  onAct?: (id: string, action: "assign" | "accept" | "reject" | "close") => void;
}) {
  const [drawer, setDrawer] = useState<SuggestionCard | null>(null);

  // Ranked by impact (high) then confidence — matches the backend ordering.
  const ranked = useMemo(
    () => [...cards].sort((a, b) => (b.impactHigh ?? 0) - (a.impactHigh ?? 0) || b.confidence - a.confidence),
    [cards],
  );
  const isManager = role === "engineer" || role === "admin";

  return (
    <div data-testid="suggestion-cards">
      {ranked.map((c) => (
        <section
          key={c.id}
          data-testid={c.unsupported ? "suggestion-unsupported" : c.duplicateOf ? "suggestion-duplicate" : "suggestion-card"}
          style={{ border: "1px solid #d8dee9", borderRadius: 10, padding: 14, marginBottom: 10, background: c.duplicateOf ? "#f7f8fa" : "#fff", opacity: c.duplicateOf ? 0.7 : 1 }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline" }}>
            <strong style={{ fontSize: 14 }}>{c.title}</strong>
            <span style={{ fontSize: 12, color: "#6b7280" }}>{c.status}</span>
          </div>
          <div style={{ marginTop: 6, fontSize: 13 }}>
            Impact {money(c.impactLow)} &ndash; {money(c.impactHigh)} &middot; confidence {(c.confidence * 100).toFixed(0)}%
          </div>

          {c.staleEvidence ? <span data-testid="stale-badge" style={{ fontSize: 11, color: "#b9770e" }}>stale evidence</span> : null}
          {c.unsupported ? (
            <p data-testid="unsupported-note" style={{ fontSize: 12, color: "#c0563b", margin: "6px 0 0" }}>
              Evidence no longer resolves — shown as unsupported, not removed.
            </p>
          ) : null}

          <div style={{ marginTop: 8, display: "flex", gap: 8 }}>
            <StandardButton type="button" onClick={() => setDrawer(c)} style={btn}>Evidence</StandardButton>
            <StandardButton type="button" onClick={() => onAct?.(c.id, "assign")} style={btn}>Acknowledge</StandardButton>
            <StandardButton
              type="button"
              isDisabled={!isManager}
              title={!isManager ? "Requires engineer/admin" : undefined}
              data-testid="accept-button"
              onClick={() => isManager && onAct?.(c.id, "accept")}
              style={{ ...btn, opacity: isManager ? 1 : 0.4, cursor: isManager ? "pointer" : "not-allowed" }}
            >
              Accept
            </StandardButton>
            <StandardButton type="button" isDisabled={!isManager} onClick={() => isManager && onAct?.(c.id, "reject")} style={{ ...btn, opacity: isManager ? 1 : 0.4 }}>Reject</StandardButton>
          </div>
          <p style={{ margin: "8px 0 0", fontSize: 11, fontStyle: "italic", color: "#6b7280" }}>{c.honestyText}</p>
        </section>
      ))}

      {drawer ? (
        <aside data-testid="evidence-drawer" style={{ position: "fixed", right: 0, top: 0, bottom: 0, width: 360, background: "#fff", borderLeft: "1px solid #d8dee9", padding: 16, overflowY: "auto" }}>
          <div style={{ display: "flex", justifyContent: "space-between" }}>
            <strong>Evidence</strong>
            <StandardButton type="button" onClick={() => setDrawer(null)} style={btn}>Close</StandardButton>
          </div>
          <p style={{ fontSize: 13, color: "#374151" }}>{drawer.title}</p>
          {drawer.evidence.length === 0 ? (
            <p style={{ fontSize: 12, color: "#c0563b" }}>No resolvable evidence.</p>
          ) : (
            drawer.evidence.map((h, i) => (
              <StandardButton key={i} type="button" data-testid="evidence-link" onClick={() => onOpenEvidence?.(h)} style={{ ...btn, display: "block", width: "100%", textAlign: "left", marginBottom: 6 }}>
                Open {h.kind} ({h.id.slice(0, 8)})
              </StandardButton>
            ))
          )}
        </aside>
      ) : null}
    </div>
  );
}

const btn: React.CSSProperties = { fontSize: 12, color: "#3b6fb5", background: "none", border: "1px solid #d8dee9", borderRadius: 6, padding: "4px 8px", cursor: "pointer" };