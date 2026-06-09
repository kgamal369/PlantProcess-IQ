
const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? "http://localhost:5063";

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(apiBaseUrl + path, {
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
    ...init,
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || response.status + " " + response.statusText);
  }

  return (await response.json()) as T;
}

export type Phase8AssistantConfiguration = {
  mode: string;
  groundingPolicy: string;
  evidencePolicy: string;
  noEgress: boolean;
  maxCitations: number;
  allowedTools: string[];
  requireHumanApprovalForRecommendations: boolean;
  enableSuggestionWorkflow: boolean;
  updatedBy: string;
  updatedAtUtc: string;
};

export type Phase8SuggestionRequest = {
  scope: string;
  outcomeKey: string;
  materialScope: string;
  minimumConfidence: number;
  includeValueProjection: boolean;
};

export type Phase8SuggestionRecommendation = {
  recommendationId: string;
  title: string;
  summary: string;
  actionType: string;
  confidence: number;
  expectedValueLow: number;
  expectedValueExpected: number;
  expectedValueHigh: number;
  currencyCode: string;
  requiresHumanApproval: boolean;
  evidence: string[];
  guardrails: string[];
  nextSteps: string[];
};

export type Phase8SuggestionResponse = {
  status: string;
  honestyCaveat: string;
  recommendations: Phase8SuggestionRecommendation[];
};

export type Phase8AssistantCitation = {
  kind: string;
  id: string;
  detail?: string | null;
};

export type Phase8AssistantAnswer = {
  isRefusal: boolean;
  refusalReason?: string | null;
  text: string;
  citations: Phase8AssistantCitation[];
  blocked: string[];
};

export type Phase8AssistantConfigSaveResponse = {
  saved: boolean;
  tenantId: string;
  isValid: boolean;
  normalized: Phase8AssistantConfiguration;
  findings: string[];
};

export const phase8AssistantApi = {
  getSuggestionHealth: () =>
    api<Record<string, unknown>>("/api/phase8/suggestions/health"),

  generateSuggestions: (request: Phase8SuggestionRequest) =>
    api<Phase8SuggestionResponse>("/api/phase8/suggestions/generate", {
      method: "POST",
      body: JSON.stringify(request),
    }),

  decideSuggestion: (recommendationId: string, decision: "approve" | "dismiss", comment: string) =>
    api<{ recommendationId: string; status: string; message: string; decidedAtUtc: string }>(
      "/api/phase8/suggestions/decision",
      {
        method: "POST",
        body: JSON.stringify({
          recommendationId,
          decision,
          comment,
          decidedBy: "hmi-user",
        }),
      },
    ),

  askAssistant: (question: string, contextChips: string[], tools: string[]) =>
    api<Phase8AssistantAnswer>("/api/assistant/ask", {
      method: "POST",
      body: JSON.stringify({
        question,
        contextChips,
        tools: tools.map((tool) => ({ tool, args: {} })),
      }),
    }),

  getAssistantConfig: () =>
    api<Phase8AssistantConfiguration>("/api/phase8/assistant-config"),

  saveAssistantConfig: (config: Phase8AssistantConfiguration) =>
    api<Phase8AssistantConfigSaveResponse>("/api/phase8/assistant-config", {
      method: "PUT",
      body: JSON.stringify(config),
    }),

  resetAssistantConfig: () =>
    api<Phase8AssistantConfigSaveResponse>("/api/phase8/assistant-config/reset", {
      method: "POST",
    }),
};

export function formatEuroRange(item: Pick<Phase8SuggestionRecommendation, "expectedValueLow" | "expectedValueExpected" | "expectedValueHigh" | "currencyCode">) {
  const formatter = new Intl.NumberFormat(undefined, {
    style: "currency",
    currency: item.currencyCode || "EUR",
    maximumFractionDigits: 0,
  });

  return formatter.format(item.expectedValueLow) + " - " +
    formatter.format(item.expectedValueHigh) +
    " expected " + formatter.format(item.expectedValueExpected);
}

export function assistantModeLabel(config?: Phase8AssistantConfiguration | null) {
  if (!config) return "Configuration pending";
  if (config.noEgress) return config.mode + " no-egress";
  return config.mode + " private endpoint required";
}
