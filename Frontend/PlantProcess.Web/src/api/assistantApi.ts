// ============================================================
// FILE: Frontend/PlantProcess.Web/src/api/assistantApi.ts
// M1-08: renamed from phase8Assistant.ts (Naming Golden Rule).
// Backend endpoint paths (/api/phase8/...) are SERVER routes and are
// intentionally unchanged; renaming them is separate backend debt.
// ============================================================

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

export type AssistantConfiguration = {
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

export type SuggestionRequest = {
  scope: string;
  outcomeKey: string;
  materialScope: string;
  minimumConfidence: number;
  includeValueProjection: boolean;
};

export type SuggestionRecommendation = {
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

export type SuggestionResponse = {
  status: string;
  honestyCaveat: string;
  recommendations: SuggestionRecommendation[];
};

export type AssistantCitation = {
  kind: string;
  id: string;
  detail?: string | null;
};

export type AssistantAnswer = {
  isRefusal: boolean;
  refusalReason?: string | null;
  text: string;
  citations: AssistantCitation[];
  blocked: string[];
};

export type AssistantConfigSaveResponse = {
  saved: boolean;
  tenantId: string;
  isValid: boolean;
  normalized: AssistantConfiguration;
  findings: string[];
};

export const assistantApi = {
  getSuggestionHealth: () =>
    api<Record<string, unknown>>("/api/phase8/suggestions/health"),

  generateSuggestions: (request: SuggestionRequest) =>
    api<SuggestionResponse>("/api/phase8/suggestions/generate", {
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
    api<AssistantAnswer>("/api/assistant/ask", {
      method: "POST",
      body: JSON.stringify({
        question,
        contextChips,
        tools: tools.map((tool) => ({ tool, args: {} })),
      }),
    }),

  getAssistantConfig: () =>
    api<AssistantConfiguration>("/api/phase8/assistant-config"),

  saveAssistantConfig: (config: AssistantConfiguration) =>
    api<AssistantConfigSaveResponse>("/api/phase8/assistant-config", {
      method: "PUT",
      body: JSON.stringify(config),
    }),

  resetAssistantConfig: () =>
    api<AssistantConfigSaveResponse>("/api/phase8/assistant-config/reset", {
      method: "POST",
    }),
};

export function formatEuroRange(item: Pick<SuggestionRecommendation, "expectedValueLow" | "expectedValueExpected" | "expectedValueHigh" | "currencyCode">) {
  const formatter = new Intl.NumberFormat(undefined, {
    style: "currency",
    currency: item.currencyCode || "EUR",
    maximumFractionDigits: 0,
  });

  return formatter.format(item.expectedValueLow) + " - " +
    formatter.format(item.expectedValueHigh) +
    " expected " + formatter.format(item.expectedValueExpected);
}

export function assistantModeLabel(config?: AssistantConfiguration | null) {
  if (!config) return "Configuration pending";
  if (config.noEgress) return config.mode + " no-egress";
  return config.mode + " private endpoint required";
}
