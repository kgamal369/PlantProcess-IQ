import { useState } from "react";

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
      style={{
        minHeight: "100vh",
        background: "linear-gradient(135deg,#06101f,#091a33 52%,#071426)",
        color: "#eaf7ff",
        padding: 24,
      }}
    >
      <section
        aria-labelledby="assistant-title"
        style={{
          maxWidth: 1080,
          margin: "0 auto",
          display: "grid",
          gap: 18,
          border: "1px solid rgba(0,212,255,0.22)",
          borderRadius: 24,
          padding: 24,
          background: "rgba(7,20,38,0.86)",
        }}
      >
        <div>
          <p style={{ color: "#00d4ff", textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12 }}>
            P4 · Production Assistant
          </p>
          <h1 id="assistant-title">Grounded Assistant</h1>
          <p>
            Answers are extractive and must cite retrieved evidence. If evidence is insufficient, the assistant abstains.
          </p>
        </div>

        <label style={{ display: "grid", gap: 8 }}>
          <span>Ask a grounded question</span>
          <textarea
            value={question}
            onChange={(event) => setQuestion(event.target.value)}
            style={{
              minHeight: 110,
              borderRadius: 16,
              border: "1px solid rgba(255,255,255,0.18)",
              background: "#071426",
              color: "#eaf7ff",
              padding: 14,
            }}
          />
        </label>

        <button
          type="button"
          onClick={ask}
          disabled={isLoading || question.trim().length === 0}
          style={{
            minHeight: 44,
            borderRadius: 14,
            border: "1px solid rgba(0,212,255,0.35)",
            background: "rgba(0,132,255,0.28)",
            color: "#eaf7ff",
            fontWeight: 800,
          }}
        >
          {isLoading ? "Asking..." : "Ask assistant"}
        </button>

        {answer && (
          <article
            aria-live="polite"
            style={{
              border: "1px solid rgba(0,212,255,0.22)",
              borderRadius: 18,
              padding: 18,
              display: "grid",
              gap: 12,
            }}
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