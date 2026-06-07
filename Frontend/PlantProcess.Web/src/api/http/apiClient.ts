import { API_BASE_URL } from "../apiConfig";
import { toast } from "@/notifications/toast";
import { mapErrorToFriendly } from "@/utils/errorMapping";

export type QueryPrimitive = string | number | boolean | null | undefined;
export type QueryParams = Record<string, QueryPrimitive>;

export const AUTH_USER_KEY = "plantprocess.auth.user";

export interface EffectiveEntitlementDto {
  tenantId: string;
  tenantCode: string;
  role: string;
  compatibilityRole: string;
  tier: string;
  permissions: string[];
  features: string[];
  lockReasons: Record<string, string>;
}

export interface AuthenticatedUser {
  userName: string;
  displayName?: string | null;
  role: "Admin" | "DataManager" | "Engineer" | "Viewer" | string;
  plantRole?: string;
  tenantId?: string;
  tenantCode?: string;
  expiresAtUtc: string;
  scopes?: string[];
  entitlements?: EffectiveEntitlementDto;
}

export interface LoginResponse {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  userName: string;
  displayName?: string | null;
  role: string;
  plantRole?: string;
  tenantId?: string;
  tenantCode?: string;
  scopes?: string[];
  forcePasswordChangeRequired?: boolean;
  isBootstrapAdmin?: boolean;
  entitlements?: EffectiveEntitlementDto;
}

export class ApiError extends Error {
  public readonly status: number;
  public readonly responseText: string;
  public readonly path: string;
  public readonly method: string;

  constructor(message: string, status: number, responseText: string, path: string, method: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.responseText = responseText;
    this.path = path;
    this.method = method;
  }
}

export interface RequestOptions {
  timeoutMs?: number;
  suppressToast?: boolean;
  retry?: boolean;
  skipAuthRefresh?: boolean;
}

const DEFAULT_TIMEOUTS: Record<string, number> = {
  GET: 6_000,
  HEAD: 6_000,
  OPTIONS: 6_000,
  POST: 12_000,
  PUT: 12_000,
  PATCH: 12_000,
  DELETE: 8_000,
};

/*
 * Retry contract:
 * GET / HEAD / OPTIONS can receive automatic retry once for bounded network failures.
 * ApiError HTTP responses are NEVER retried on 4xx/5xx.
 */
const IDEMPOTENT_METHODS = new Set(["GET", "HEAD", "OPTIONS"]);

let accessTokenMemory: string | null = null;
let authenticatedUserMemory: AuthenticatedUser | null = null;
let refreshPromise: Promise<LoginResponse | null> | null = null;

export function setAccessToken(token: string | null) {
  accessTokenMemory = token || null;
}

export function getAccessToken() {
  return accessTokenMemory;
}

export function setAuthenticatedUser(user: AuthenticatedUser | null) {
  authenticatedUserMemory = user;
}

export function getAuthenticatedUser(): AuthenticatedUser | null {
  return authenticatedUserMemory;
}

export function clearAuthentication() {
  accessTokenMemory = null;
  authenticatedUserMemory = null;
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

async function refreshAccessToken(): Promise<LoginResponse | null> {
  if (refreshPromise) return refreshPromise;

  refreshPromise = (async () => {
    try {
      const response = await rawRequestJson<LoginResponse>(
        "POST",
        "/auth/refresh",
        undefined,
        { suppressToast: true, skipAuthRefresh: true },
      );

      applyLoginResponse(response);
      return response;
    } catch {
      clearAuthentication();
      return null;
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

function applyLoginResponse(response: LoginResponse) {
  setAccessToken(response.accessToken);
  setAuthenticatedUser({
    userName: response.userName,
    displayName: response.displayName,
    role: response.role,
    plantRole: response.plantRole,
    tenantId: response.tenantId,
    tenantCode: response.tenantCode,
    expiresAtUtc: response.expiresAtUtc,
    scopes: response.scopes ?? response.entitlements?.permissions ?? [],
    entitlements: response.entitlements,
  });
}

async function rawRequestJson<T>(
  method: string,
  path: string,
  body?: unknown,
  options: RequestOptions = {},
): Promise<T> {
  const effectiveTimeout = options.timeoutMs ?? DEFAULT_TIMEOUTS[method] ?? 10_000;
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
      credentials: "include",
      signal: controller.signal,
      body: body === undefined ? undefined : JSON.stringify(body),
    });

    const text = await response.text();

    if (!response.ok) {
      throw new ApiError(
        text || `API request failed: ${method} ${path} returned ${response.status}`,
        response.status,
        text,
        path,
        method,
      );
    }

    if (!text) return undefined as T;
    return JSON.parse(text) as T;
  } finally {
    window.clearTimeout(timer);
  }
}

async function requestJson<T>(
  method: string,
  path: string,
  body?: unknown,
  options: RequestOptions = {},
): Promise<T> {
  const allowRetry = options.retry ?? IDEMPOTENT_METHODS.has(method);
  const maxAttempts = allowRetry ? 2 : 1;
  let lastError: unknown = null;

  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    try {
      return await rawRequestJson<T>(method, path, body, options);
    } catch (err) {
      lastError = err;

      if (
        err instanceof ApiError &&
        err.status === 401 &&
        !options.skipAuthRefresh
      ) {
        const refreshed = await refreshAccessToken();
        if (refreshed) {
          return await rawRequestJson<T>(
            method,
            path,
            body,
            { ...options, skipAuthRefresh: true },
          );
        }
      }

      if (err instanceof ApiError) {
        dispatchAuthFailure(err);

        if (!options.suppressToast) {
          const friendly = mapErrorToFriendly({
            status: err.status,
            responseText: err.responseText,
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

        throw err;
      }

      if (!(err instanceof ApiError)) {
        const isAbort = err instanceof DOMException && err.name === "AbortError";
        const isTypeError = err instanceof TypeError;
        const isRetryable = isAbort || isTypeError;

        if (attempt < maxAttempts && isRetryable && allowRetry) {
          await sleep(150 * attempt);
          continue;
        }

        if (!options.suppressToast) {
          const friendly = mapErrorToFriendly({ status: 0, method, path });
          toast.warning(friendly.headline, {
            id: `network-${method}-${path}`,
            description: friendly.description,
          });
        }

        throw err;
      }
    }
  }

  throw lastError ?? new Error(`Request failed: ${method} ${path}`);
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => window.setTimeout(resolve, ms));
}

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
      { suppressToast: true },
    );

    applyLoginResponse(response);
    return response;
  },

  async refresh() {
    return refreshAccessToken();
  },

  async logout() {
    try {
      await requestJson<void>(
        "POST",
        "/auth/logout",
        undefined,
        { suppressToast: true, skipAuthRefresh: true },
      );
    } finally {
      clearAuthentication();
    }
  },

  setAccessToken,
  getAccessToken,
  setAuthenticatedUser,
  getAuthenticatedUser,
  clearAuthentication,
};
