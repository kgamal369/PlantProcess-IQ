import { API_BASE_URL } from "../apiConfig";
import { getAccessToken } from "../http";
import { toast } from "@/notifications/toast";
import { mapErrorToFriendly } from "@/utils/errorMapping";

// ============================================================
// FE-HARD-002 / FE-HARD-003 / FE-HARD-010
// Legacy API hardening bridge.
//
// Why this file exists:
// The project already has a modern api/http/apiClient.ts, but many
// existing production/demo pages still call plantProcessApi from the
// legacy module.
//
// This file keeps the hardening behavior OUTSIDE plantProcessApi.ts so
// the noLegacyApiGrowth architecture guard remains meaningful.
// ============================================================

export type PrimitiveQueryValue = string | number | boolean | null | undefined;
export type QueryParams = Record<string, PrimitiveQueryValue>;

type LegacyHttpMethod =
  | "GET"
  | "HEAD"
  | "OPTIONS"
  | "POST"
  | "PUT"
  | "PATCH"
  | "DELETE";

export interface LegacyRequestOptions extends RequestInit {
  suppressToast?: boolean;
  retry?: boolean;
  timeoutMs?: number;
}

export class LegacyApiError extends Error {
  public readonly status: number;
  public readonly responseText: string;
  public readonly path: string;
  public readonly method: LegacyHttpMethod;

  constructor(
    message: string,
    status: number,
    responseText: string,
    path: string,
    method: LegacyHttpMethod
  ) {
    super(message);
    this.name = "LegacyApiError";
    this.status = status;
    this.responseText = responseText;
    this.path = path;
    this.method = method;
  }
}

export function buildQuery(params?: QueryParams): string {
  if (!params) return "";

  const searchParams = new URLSearchParams();

  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === "") return;
    searchParams.set(key, String(value));
  });

  const query = searchParams.toString();
  return query ? `?${query}` : "";
}

function normalizeMethod(method?: string): LegacyHttpMethod {
  const normalized = (method ?? "GET").toUpperCase();

  if (
    normalized === "GET" ||
    normalized === "HEAD" ||
    normalized === "OPTIONS" ||
    normalized === "POST" ||
    normalized === "PUT" ||
    normalized === "PATCH" ||
    normalized === "DELETE"
  ) {
    return normalized;
  }

  return "GET";
}

function defaultTimeoutMs(method: LegacyHttpMethod): number {
  if (method === "GET" || method === "HEAD" || method === "OPTIONS") {
    return 6_000;
  }

  if (method === "DELETE") {
    return 8_000;
  }

  return 12_000;
}

function isIdempotentMethod(method: LegacyHttpMethod): boolean {
  return method === "GET" || method === "HEAD" || method === "OPTIONS";
}

function shouldRetryLegacyRequest(
  method: LegacyHttpMethod,
  attempt: number,
  retryOption: boolean | undefined,
  error: unknown
): boolean {
  if (attempt > 0) return false;

  const retryEnabled = retryOption ?? isIdempotentMethod(method);

  if (!retryEnabled) return false;
  if (!isIdempotentMethod(method)) return false;

  if (error instanceof DOMException && error.name === "AbortError") {
    return true;
  }

  if (error instanceof TypeError) {
    // Browser fetch uses TypeError for network failure / DNS / CORS failure.
    return true;
  }

  return false;
}

function dispatchLegacyAuthFailure(
  status: number,
  path: string,
  responseText: string
) {
  if (status !== 401 && status !== 403) return;

  window.dispatchEvent(
    new CustomEvent("plantprocess:auth-failure", {
      detail: {
        status,
        path,
        responseText,
      },
    })
  );
}

function notifyLegacyApiFailure(
  method: LegacyHttpMethod,
  path: string,
  status: number,
  responseText: string | undefined,
  suppressToast: boolean | undefined
) {
  if (suppressToast) return;

  const friendly = mapErrorToFriendly({
    status,
    responseText,
    method,
    path,
  });

  const toastId = `legacy-api:${method}:${path}:${status}`;

  if (friendly.severity === "warning") {
    toast.warning(friendly.headline, {
      id: toastId,
      description: friendly.description,
    });
    return;
  }

  if (friendly.severity === "info") {
    toast.info(friendly.headline, {
      id: toastId,
      description: friendly.description,
    });
    return;
  }

  toast.error(friendly.headline, {
    id: toastId,
    description: friendly.description,
  });
}

async function executeLegacyFetch<T>(
  path: string,
  options: LegacyRequestOptions,
  method: LegacyHttpMethod,
  timeoutMs: number
): Promise<T> {
  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), timeoutMs);
  const token = getAccessToken();

  const headers = new Headers(options.headers);

  if (!headers.has("Accept")) {
    headers.set("Accept", "application/json");
  }

  if (options.body !== undefined && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  if (token && !headers.has("Authorization")) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  try {
    const response = await fetch(`${API_BASE_URL}${path}`, {
      ...options,
      method,
      signal: controller.signal,
      headers,
    });

    const text = await response.text();

    if (!response.ok) {
      dispatchLegacyAuthFailure(response.status, path, text);

      notifyLegacyApiFailure(
        method,
        path,
        response.status,
        text,
        options.suppressToast
      );

      const friendly = mapErrorToFriendly({
        status: response.status,
        responseText: text,
        method,
        path,
      });

      throw new LegacyApiError(
        friendly.headline,
        response.status,
        text,
        path,
        method
      );
    }

    if (!text) {
      return undefined as T;
    }

    try {
      return JSON.parse(text) as T;
    } catch {
      return text as T;
    }
  } finally {
    window.clearTimeout(timeout);
  }
}

export async function requestJson<T>(
  path: string,
  options?: LegacyRequestOptions,
  timeoutMsFromOldCall?: number
): Promise<T> {
  const method = normalizeMethod(options?.method);
  const timeoutMs =
    options?.timeoutMs ??
    timeoutMsFromOldCall ??
    defaultTimeoutMs(method);

  let lastError: unknown;

  for (let attempt = 0; attempt < 2; attempt += 1) {
    try {
      return await executeLegacyFetch<T>(
        path,
        options ?? {},
        method,
        timeoutMs
      );
    } catch (error) {
      lastError = error;

      if (
        !shouldRetryLegacyRequest(
          method,
          attempt,
          options?.retry,
          error
        )
      ) {
        break;
      }

      await new Promise((resolve) => window.setTimeout(resolve, 250));
    }
  }

  if (lastError instanceof DOMException && lastError.name === "AbortError") {
    notifyLegacyApiFailure(
      method,
      path,
      0,
      "Request timed out.",
      options?.suppressToast
    );

    throw new LegacyApiError(
      "Request timed out",
      0,
      "Request timed out.",
      path,
      method
    );
  }

  if (lastError instanceof TypeError) {
    notifyLegacyApiFailure(
      method,
      path,
      0,
      lastError.message,
      options?.suppressToast
    );

    throw new LegacyApiError(
      "Network problem",
      0,
      lastError.message,
      path,
      method
    );
  }

  throw lastError;
}

export function getJson<T>(
  path: string,
  params?: QueryParams
): Promise<T> {
  return requestJson<T>(`${path}${buildQuery(params)}`, {
    method: "GET",
    retry: true,
  });
}

export function postJson<T>(
  path: string,
  body?: unknown
): Promise<T> {
  return requestJson<T>(path, {
    method: "POST",
    retry: false,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}

export function putJson<T>(
  path: string,
  body?: unknown
): Promise<T> {
  return requestJson<T>(path, {
    method: "PUT",
    retry: false,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}

export function patchJson<T>(
  path: string,
  body?: unknown
): Promise<T> {
  return requestJson<T>(path, {
    method: "PATCH",
    retry: false,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}

export function deleteJson<T>(path: string): Promise<T> {
  return requestJson<T>(path, {
    method: "DELETE",
    retry: false,
  });
}
