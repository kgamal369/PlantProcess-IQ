// ============================================================
// FILE: Frontend/PlantProcess.Web/src/api/assistantApi.ts
// M1-08: renamed from phase8Assistant.ts (Naming Golden Rule).
// Backend endpoint paths (/api/phase8/...) are SERVER routes and are
// intentionally unchanged; renaming them is separate backend debt.
// ============================================================

import { getAccessToken } from "@/api/http/apiClient";

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? "http://localhost:5063";

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  /* PPIQ-T071 closure. This module had its own fetch helper that sent cookies
     only. The access token lives in memory in the shared client and is attached
     by that module alone, while both assistant endpoint groups require an
     authenticated principal and the host registers bearer validation with no
     cookie bridge. Every call from here was therefore refused. Attach the same
     token the shared client attaches; caller-supplied headers still win. */
  const accessToken = getAccessToken();
  const authHeaders: Record<string, string> = accessToken
    ? { Authorization: "Bearer " + accessToken }
    : {};

  const response = await fetch(apiBaseUrl + path, {
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...authHeaders,
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

/**
 * T-072 wire shape of the page and widget context envelope.
 *
 * The server narrows retrieval with the identifiers and never lets any of this
 * become answer evidence, so nothing here is a claim - it is a description of
 * what the user is looking at.
 */
export type AssistantContextPayload = {
  route: string | null;
  pageCode: string | null;
  widgetCode: string | null;
  selections: string[];
  filters: string[];
  lastResultSummary: string | null;
  evidenceHandles: string[] | null;
};

/**
 * T-075 wire shape of one persisted widget-result evidence snapshot, as returned
 * by the tenant-scoped endpoint T-073 built. The strip renders THIS, never the
 * answer prose.
 */
export type AssistantWidgetResultEvidence = {
  evidenceId: string;
  available: boolean;
  pageCode: string;
  widgetCode: string;
  widgetDefinitionId?: string | null;
  widgetType?: string | null;
  chartType?: string | null;
  dimensionCode?: string | null;
  measureCode?: string | null;
  parameterCode?: string | null;
  queryFingerprint?: string | null;
  resultFingerprint?: string | null;
  filterContext?: string | null;
  generatedAtUtc?: string | null;
  columns: string[];
  rows: string[][];
  hasObservationCount: boolean;
  observationCountTotal: number;
  sentence: string;
};

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

  askAssistant: (
    question: string,
    contextChips: string[],
    tools: string[],
    context?: AssistantContextPayload | null,
  ) =>
    api<AssistantAnswer>("/api/assistant/ask", {
      method: "POST",
      body: JSON.stringify({
        question,
        contextChips,
        tools: tools.map((tool) => ({ tool, args: {} })),
        context: context ?? null,
      }),
    }),

  /**
   * T-075. Resolves ONE citation's evidence, on demand. Nothing calls this when
   * an answer renders; it is called when a chip is opened.
   *
   * A 404 from this endpoint means the evidence is not available to this tenant,
   * which is a different thing from the request failing, so it is surfaced as a
   * value rather than thrown.
   */
  getWidgetResultEvidence: async (evidenceId: string): Promise<AssistantWidgetResultEvidence | null> => {
    const token = getAccessToken();
    const authHeaders: Record<string, string> = token ? { Authorization: "Bearer " + token } : {};

    const response = await fetch(
      apiBaseUrl + "/api/assistant/evidence/widget-result/" + encodeURIComponent(evidenceId),
      { credentials: "include", headers: { "Content-Type": "application/json", ...authHeaders } },
    );

    if (response.status === 404) return null;
    if (!response.ok) throw new Error("evidence request failed: " + response.status);

    return (await response.json()) as AssistantWidgetResultEvidence;
  },

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
