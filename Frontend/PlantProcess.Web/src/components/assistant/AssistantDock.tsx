/* PPIQ-T071 */
import { useLocation } from "react-router-dom";
import { AssistantChat } from "@/components/assistant/AssistantChat";
import { ASSISTANT_CONTEXT_CHIPS, useAssistantDock } from "@/components/assistant/AssistantDockContext";
import { useAssistantPageContext } from "@/components/assistant/assistantPageContext";
import { StandardButton } from "@/components/standard";
import "./AssistantDock.css";

/**
 * The persistent dock. PRESENTATION AND DOCK BEHAVIOUR ONLY - it never calls
 * the assistant api. Every question goes through the provider's ask(), which is
 * the single call site, and an architecture test asserts this file contains no
 * askAssistant call of its own.
 *
 * It suppresses itself on /assistant so the user is never looking at two
 * expanded assistant surfaces at once. Both read the same context, so it is one
 * conversation either way.
 */
export function AssistantDock() {
  const { turns, busy, status, ask, setStatus, expanded, setExpanded } = useAssistantDock();
  const location = useLocation();
  const pageContext = useAssistantPageContext();

  /* The full-page assistant owns the screen on its own route. */
  if (location.pathname.startsWith("/assistant")) return null;

  if (!expanded) {
    return (
      <div className="piq-dock piq-dock--collapsed">
        <StandardButton
          type="button"
          className="piq-dock-toggle"
          variant="primary"
          size="md"
          aria-expanded={false}
          aria-controls="piq-assistant-dock-panel"
          onClick={() => setExpanded(true)}
        >
          Assistant
          {turns.length > 0 ? <span className="piq-dock-count">{turns.length}</span> : null}
        </StandardButton>
      </div>
    );
  }

  return (
    <div className="piq-dock piq-dock--expanded">
      <section
        id="piq-assistant-dock-panel"
        className="piq-dock-panel"
        role="complementary"
        aria-label="Grounded assistant"
        onKeyDown={(event) => {
          if (event.key === "Escape") {
            event.stopPropagation();
            setExpanded(false);
          }
        }}
      >
        <header className="piq-dock-head">
          <span className="piq-dock-title">Assistant</span>
          <StandardButton
            type="button"
            className="piq-dock-close"
            variant="primary"
            size="md"
            aria-expanded={true}
            aria-controls="piq-assistant-dock-panel"
            onClick={() => setExpanded(false)}
          >
            Collapse
          </StandardButton>
        </header>
        <div className="piq-dock-body">
          <AssistantChat
            turns={turns}
            chips={ASSISTANT_CONTEXT_CHIPS}
            isBusy={busy}
            onAsk={(question) => void ask(question, pageContext)}
            onOpenEvidence={(handle) => setStatus("Evidence handle: " + handle.kind + " " + handle.id)}
          />
        </div>
        <p className="piq-dock-status">{status}</p>
      </section>
    </div>
  );
}