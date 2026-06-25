import { useMemo, useState } from "react";
import { StandardButton } from "@/components/standard";

import { P2T08_STANDARD_ROLLOUT_MARKER } from "@/components/standard/StandardP2Controls";
export type ProvenanceHandleRef = { kind: string; id: string; detail?: string | null };

export type SuggestionCard = {
  id: string;
  title: string;
  actionType: string;
  status: string;
  confidence: number;
  population?: number;
  method?: string;
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
        >
          <div>
            <strong>{c.title}</strong>
            <span>{c.status}</span>
          </div>
          <div>
            {c.population ? <span data-testid="suggestion-population">Population {c.population} &middot; </span> : null}
            {c.method ? <span data-testid="suggestion-method">{c.method} &middot; </span> : null}
            Impact {money(c.impactLow)} &ndash; {money(c.impactHigh)} &middot; confidence {(c.confidence * 100).toFixed(0)}%
          </div>

          {c.staleEvidence ? <span data-testid="stale-badge">stale evidence</span> : null}
          {c.unsupported ? (
            <p data-testid="unsupported-note">
              Evidence no longer resolves — shown as unsupported, not removed.
            </p>
          ) : null}

          <div>
            <StandardButton type="button" onClick={() => setDrawer(c)}>Evidence</StandardButton>
            <StandardButton type="button" onClick={() => onAct?.(c.id, "assign")}>Acknowledge</StandardButton>
            <StandardButton
              type="button"
              isDisabled={!isManager}
              title={!isManager ? "Requires engineer/admin" : undefined}
              data-testid="accept-button"
              onClick={() => isManager && onAct?.(c.id, "accept")}
            >
              Accept
            </StandardButton>
            <StandardButton type="button" isDisabled={!isManager} onClick={() => isManager && onAct?.(c.id, "reject")}>Reject</StandardButton>
          </div>
          <p>{c.honestyText}</p>
        </section>
      ))}

      {drawer ? (
        <aside data-testid="evidence-drawer">
          <div>
            <strong>Evidence</strong>
            <StandardButton type="button" onClick={() => setDrawer(null)}>Close</StandardButton>
          </div>
          <p>{drawer.title}</p>
          {drawer.evidence.length === 0 ? (
            <p>No resolvable evidence.</p>
          ) : (
            drawer.evidence.map((h, i) => (
              <StandardButton key={i} type="button" data-testid="evidence-link" onClick={() => onOpenEvidence?.(h)}>
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