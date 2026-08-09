/* PPIQ-T071 */
import { AssistantChat } from "@/components/assistant/AssistantChat";
import { ASSISTANT_CONTEXT_CHIPS, useAssistantDock } from "@/components/assistant/AssistantDockContext";
import { useAssistantPageContext } from "@/components/assistant/assistantPageContext";
import { starterQuestions } from "@/components/assistant/assistantEvidence";
import { StandardButton } from "@/components/standard";
import "./AssistantDock.css";

/**
 * The persistent dock. PRESENTATION AND DOCK BEHAVIOUR ONLY - it never calls
 * the assistant api. Every question goes through the provider's ask(), which is
 * the single call site, and an architecture test asserts this file contains no
 * askAssistant call of its own.
 *
 * G1 visible contract: the assistant is a GLOBAL SHELL COMPONENT and not a
 * route. There is no standalone assistant page left for it to suppress itself
 * against - /assistant is a hidden compatibility redirect that nothing
 * navigates to - so the dock is present on every authenticated surface without
 * exception.
 */
export function AssistantDock() {
  const { turns, busy, status, ask, setStatus, expanded, setExpanded } = useAssistantDock();
  const pageContext = useAssistantPageContext();

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
            starters={starterQuestions({
              pageCode: pageContext.pageCode,
              widgetCode: pageContext.widgetCode,
              selections: pageContext.selections,
            })}
            onOpenEvidence={(handle) => setStatus("Evidence handle: " + handle.kind + " " + handle.id)}
          />
        </div>
        <p className="piq-dock-status">{status}</p>
      </section>
    </div>
  );
}