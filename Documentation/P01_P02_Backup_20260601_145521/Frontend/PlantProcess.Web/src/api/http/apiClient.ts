/**
 * api/http/apiClient.ts
 * --------------------------------------------------------------------
 * HTTP client for PlantProcess IQ.
 *
 * Tasks covered:
 *   FE-HARD-002  toast notification system (auto-toast on errors)
 *   FE-HARD-003  timeout reduction (30s → 6s GET / 12s POST) + retry
 *   FE-HARD-010  six failure modes via mapErrorToFriendly()
 *
 * Behaviour:
 *   - GET / HEAD / OPTIONS: 6s timeout, automatic retry once on
 *     AbortError or network failure. NEVER retried on 4xx/5xx.
 *   - POST / PUT / PATCH: 12s timeout, no retry (not idempotent).
 *   - DELETE: 8s timeout, no retry by default.
 *   - All non-2xx responses raise ApiError AND auto-toast unless the
 *     caller passes { suppressToast: true }.
 *   - 401/403 dispatch a "plantprocess:auth-failure" window event so
 *     the auth context can react (sign-out, redirect to login, etc).
 *
 * This is a DROP-IN REPLACEMENT for the previous apiClient.ts.
 * All previous public exports are preserved.
 */

import { API_BASE_URL } from "../apiConfig";
import { toast } from "@/notifications/toast";
import { mapErrorToFriendly } from "@/utils/errorMapping";

// ───────────────────────────────────────────────────────────
// Public types
// ───────────────────────────────────────────────────────────

export type QueryPrimitive = string | number | boolean | null | undefined;
export type QueryParams = Record<string, QueryPrimitive>;

export const ACCESS_TOKEN_KEY = "plantprocess.auth.accessToken";
export const AUTH_USER_KEY = "plantprocess.auth.user";

export interface AuthenticatedUser {
  userName: string;
  displayName?: string | null;
  role: "Admin" | "DataManager" | "Engineer" | "Viewer" | string;
  expiresAtUtc: string;
  scopes?: string[];
}

export interface LoginResponse {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  userName: string;
  displayName?: string | null;
  role: string;
  scopes?: string[];
}

export class ApiError extends Error {
  public readonly status: number;
  public readonly responseText: string;
  public readonly path: string;
  public readonly method: string;

  constructor(
    message: string,
    status: number,
    responseText: string,
    path: string,
    method: string,
  ) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.responseText = responseText;
    this.path = path;
    this.method = method;
  }
}

/**
 * Options accepted by every apiClient method.
 */
export interface RequestOptions {
  /** Override the default per-method timeout (ms). */
  timeoutMs?: number;
  /** Disable auto-toast on failure. Use when the caller renders errors inline. */
  suppressToast?: boolean;
  /** Force retry behaviour. Defaults to true for idempotent methods only. */
  retry?: boolean;
}

// ───────────────────────────────────────────────────────────
// Timeout configuration  (FE-HARD-003)
// ───────────────────────────────────────────────────────────

const DEFAULT_TIMEOUTS: Record<string, number> = {
  GET:     6_000,
  HEAD:    6_000,
  OPTIONS: 6_000,
  POST:    12_000,
  PUT:     12_000,
  PATCH:   12_000,
  DELETE:  8_000,
};

const IDEMPOTENT_METHODS = new Set(["GET", "HEAD", "OPTIONS"]);

// ───────────────────────────────────────────────────────────
// Auth token helpers (unchanged from previous apiClient)
// ───────────────────────────────────────────────────────────

export function setAccessToken(token: string | null) {
  if (!token) {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    return;
  }
  localStorage.setItem(ACCESS_TOKEN_KEY, token);
}

export function getAccessToken() {
  return localStorage.getItem(ACCESS_TOKEN_KEY);
}

export function setAuthenticatedUser(user: AuthenticatedUser | null) {
  if (!user) {
    localStorage.removeItem(AUTH_USER_KEY);
    return;
  }
  localStorage.setItem(AUTH_USER_KEY, JSON.stringify(user));
}

export function getAuthenticatedUser(): AuthenticatedUser | null {
  const raw = localStorage.getItem(AUTH_USER_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as AuthenticatedUser;
  } catch {
    localStorage.removeItem(AUTH_USER_KEY);
    return null;
  }
}

export function clearAuthentication() {
  setAccessToken(null);
  setAuthenticatedUser(null);
}

export function buildQuery(params?: QueryParams): string {
  if (!params) return "";
  const searchParams = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === null || value === undefined || value === "") continue;
    searchParams.set(key, String(value));
  }
  const query = searchParams.toString();
  return query ? `?${query}` : "";
}

function dispatchAuthFailure(error: ApiError) {
  if (error.status === 401 || error.status === 403) {
    window.dispatchEvent(
      new CustomEvent("plantprocess:auth-failure", {
        detail: {
          status: error.status,
          path: error.path,
          responseText: error.responseText,
        },
      }),
    );
  }
}

// ───────────────────────────────────────────────────────────
// Core request function  (FE-HARD-003)
// ───────────────────────────────────────────────────────────

async function requestJson<T>(
  method: string,
  path: string,
  body?: unknown,
  options: RequestOptions = {},
): Promise<T> {
  const effectiveTimeout =
    options.timeoutMs ?? DEFAULT_TIMEOUTS[method] ?? 10_000;

  const allowRetry =
    options.retry ?? IDEMPOTENT_METHODS.has(method);
  const maxAttempts = allowRetry ? 2 : 1;

  let lastError: unknown = null;

  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    const controller = new AbortController();
    const timer = window.setTimeout(() => controller.abort(), effectiveTimeout);

    const token = getAccessToken();
    const headers: Record<string, string> = { Accept: "application/json" };
    if (body !== undefined) headers["Content-Type"] = "application/json";
    if (token) headers.Authorization = `Bearer ${token}`;

    try {
      const response = await fetch(`${API_BASE_URL}${path}`, {
        method,
        headers,
        signal: controller.signal,
        body: body === undefined ? undefined : JSON.stringify(body),
      });

      const text = await response.text();

      if (!response.ok) {
        const error = new ApiError(
          text || `API request failed: ${method} ${path} returned ${response.status}`,
          response.status,
          text,
          path,
          method,
        );

        dispatchAuthFailure(error);

        // Auto-toast unless caller suppresses
        if (!options.suppressToast) {
          const friendly = mapErrorToFriendly({
            status: response.status,
            responseText: text,
            method,
            path,
          });
          const toastId = `${method}-${path}`;
          if (friendly.severity === "error") {
            toast.error(friendly.headline, { id: toastId, description: friendly.description });
          } else if (friendly.severity === "warning") {
            toast.warning(friendly.headline, { id: toastId, description: friendly.description });
          } else {
            toast.info(friendly.headline, { id: toastId, description: friendly.description });
          }
        }

        // NEVER retry on a 4xx / 5xx — the server already processed our request.
        // Throw immediately so the caller can react with form state etc.
        throw error;
      }

      if (!text) return undefined as T;
      return JSON.parse(text) as T;
    } catch (err) {
      lastError = err;

      // Did the request abort due to timeout, OR was there a true network failure?
      const isAbort =
        err instanceof DOMException && err.name === "AbortError";
      const isTypeError =
        err instanceof TypeError && /fetch|network/i.test(err.message);
      const isRetryable = isAbort || isTypeError;

      // Only retry idempotent methods on network/abort, and only if we have attempts left.
      if (attempt < maxAttempts && isRetryable && allowRetry) {
        // Optional: short delay before retry so we don't pile on a flapping network.
        await sleep(150 * attempt);
        continue;
      }

      // Auto-toast for the network/abort case (status 0) — ApiError is already toasted above.
      if (!(err instanceof ApiError) && !options.suppressToast) {
        const friendly = mapErrorToFriendly({
          status: 0,
          method,
          path,
        });
        toast.warning(friendly.headline, {
          id: `network-${method}-${path}`,
          description: friendly.description,
        });
      }

      throw err;
    } finally {
      window.clearTimeout(timer);
    }
  }

  // Defensive — should be unreachable
  throw lastError ?? new Error(`Request failed: ${method} ${path}`);
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => window.setTimeout(resolve, ms));
}

// ───────────────────────────────────────────────────────────
// Public client surface
// ───────────────────────────────────────────────────────────

export const apiClient = {
  get<T>(path: string, params?: QueryParams, options?: RequestOptions) {
    return requestJson<T>("GET", `${path}${buildQuery(params)}`, undefined, options);
  },

  post<T>(path: string, body?: unknown, options?: RequestOptions) {
    return requestJson<T>("POST", path, body, options);
  },

  put<T>(path: string, body?: unknown, options?: RequestOptions) {
    return requestJson<T>("PUT", path, body, options);
  },

  patch<T>(path: string, body?: unknown, options?: RequestOptions) {
    return requestJson<T>("PATCH", path, body, options);
  },

  delete<T>(path: string, options?: RequestOptions) {
    return requestJson<T>("DELETE", path, undefined, options);
  },

  async login(userName: string, password: string, requestedRole?: string | null) {
    const response = await requestJson<LoginResponse>(
      "POST",
      "/auth/login",
      { userName, password, requestedRole },
      { suppressToast: true }, // login forms render their own error UI
    );

    setAccessToken(response.accessToken);
    setAuthenticatedUser({
      userName: response.userName,
      displayName: response.displayName,
      role: response.role,
      expiresAtUtc: response.expiresAtUtc,
      scopes: response.scopes ?? [],
    });

    return response;
  },

  logout() {
    clearAuthentication();
  },

  setAccessToken,
  getAccessToken,
  setAuthenticatedUser,
  getAuthenticatedUser,
  clearAuthentication,
};
