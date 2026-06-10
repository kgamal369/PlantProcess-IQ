import { useState } from "react";

import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Button, StandardP2TextArea } from "@/components/standard/StandardP2Controls";
type AssistantCitation = {
  kind: string;
  id: string;
  detail?: string | null;
};

type AssistantAnswer = {
  isRefusal: boolean;
  refusalReason?: string | null;
  text: string;
  citations: AssistantCitation[];
  blocked: string[];
};

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5063";

export function GroundedAssistantPage() {
  const [question, setQuestion] = useState("What evidence supports the latest quality finding?");
  const [answer, setAnswer] = useState<AssistantAnswer | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  async function ask() {
    setIsLoading(true);

    try {
      const response = await fetch(`${apiBaseUrl}/api/assistant/ask`, {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ question, contextChips: [], tools: [] }),
      });

      if (!response.ok) {
        throw new Error(`${response.status} ${response.statusText}`);
      }

      setAnswer((await response.json()) as AssistantAnswer);
    } catch (error) {
      setAnswer({
        isRefusal: true,
        refusalReason: error instanceof Error ? error.message : "Assistant request failed.",
        text: "",
        citations: [],
        blocked: [],
      });
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <main
    >
      <section
        aria-labelledby="assistant-title"
      >
        <div>
          <p>
            P4 · Production Assistant
          </p>
          <h1 id="assistant-title">Grounded Assistant</h1>
          <p>
            Answers are extractive and must cite retrieved evidence. If evidence is insufficient, the assistant abstains.
          </p>
        </div>

        <label>
          <span>Ask a grounded question</span>
          <StandardP2TextArea
            value={question}
            onChange={(event) => setQuestion(event.target.value)}
          />
        </label>

        <StandardP2Button
          type="button"
          onClick={ask}
          disabled={isLoading || question.trim().length === 0}
        >
          {isLoading ? "Asking..." : "Ask assistant"}
        </StandardP2Button>

        {answer && (
          <article
            aria-live="polite"
          >
            {answer.isRefusal ? (
              <strong>No grounded answer — abstained: {answer.refusalReason}</strong>
            ) : (
              <p>{answer.text}</p>
            )}

            <section aria-label="Evidence citations">
              <strong>Citations</strong>
              {answer.citations.length === 0 ? (
                <p>No citations returned.</p>
              ) : (
                <ul>
                  {answer.citations.map((citation) => (
                    <li key={`${citation.kind}:${citation.id}:${citation.detail ?? ""}`}>
                      {citation.kind}:{citation.id}
                      {citation.detail ? ` — ${citation.detail}` : ""}
                    </li>
                  ))}
                </ul>
              )}
            </section>

            {answer.blocked.length > 0 && (
              <section aria-label="Blocked unsupported claims">
                <strong>Blocked unsupported claims</strong>
                <ul>
                  {answer.blocked.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              </section>
            )}
          </article>
        )}
      </section>
    </main>
  );
}

export default GroundedAssistantPage;