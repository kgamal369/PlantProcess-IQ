// ============================================================
// FILE: Frontend/PlantProcess.Web/src/state/AuthContext.tsx
//
// V7 Phase 1 hardening:
// - PPIQ-T280: auth-failure retry storm capped with backoff.
// - PPIQ-T281: distinct invalid-credentials / forbidden / network errors.
// - PPIQ-T285: VITE_SMOKE_* env contract is explicit and safe.
// ============================================================

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import {
  ApiError,
  apiClient,
  type AuthenticatedUser,
} from "../api/http/apiClient";

const DEMO_USER =
  (import.meta.env.VITE_SMOKE_USERNAME as string | undefined)?.trim() || "admin";

const DEMO_PASS =
  (import.meta.env.VITE_SMOKE_PASSWORD as string | undefined) ?? "";

const EXPIRY_BUFFER_MS = 60_000;
const MAX_AUTO_BOOTSTRAP_ATTEMPTS = 3;
const AUTH_RETRY_BACKOFF_MS = [0, 500, 1500];

type AuthBootstrapReason =
  | "initial"
  | "manual"
  | "token-refresh"
  | "auth-failure";

type AuthErrorKind =
  | "missing-smoke-password"
  | "invalid-credentials"
  | "forbidden"
  | "backend-unreachable"
  | "server-error"
  | "unknown";

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => window.setTimeout(resolve, ms));
}

function isTokenStillValid(user: AuthenticatedUser | null): boolean {
  if (!user) return false;

  try {
    return (
      new Date(user.expiresAtUtc).getTime() - Date.now() > EXPIRY_BUFFER_MS
    );
  } catch {
    return false;
  }
}

function classifyAuthError(err: unknown): AuthErrorKind {
  if (err instanceof ApiError) {
    if (err.status === 401) return "invalid-credentials";
    if (err.status === 403) return "forbidden";
    if (err.status >= 500) return "server-error";
    if (err.status === 0) return "backend-unreachable";
  }

  if (err instanceof DOMException && err.name === "AbortError") {
    return "backend-unreachable";
  }

  if (err instanceof TypeError) {
    return "backend-unreachable";
  }

  return "unknown";
}

function buildAuthMessage(kind: AuthErrorKind, err?: unknown): string {
  const detail = err instanceof Error ? err.message : undefined;

  switch (kind) {
    case "missing-smoke-password":
      return (
        "Demo login is not configured. Set VITE_SMOKE_USERNAME and " +
        "VITE_SMOKE_PASSWORD in Frontend/PlantProcess.Web/.env.local. " +
        "The frontend no longer falls back to an insecure default password."
      );

    case "invalid-credentials":
      return (
        "Invalid demo login credentials. Check that VITE_SMOKE_PASSWORD " +
        "matches the backend admin or seeded demo-user password."
      );

    case "forbidden":
      return (
        "Your account is authenticated but not allowed to access this view. " +
        "Use an Admin/DataManager account for administrative pages."
      );

    case "backend-unreachable":
      return (
        "Backend API is unreachable. Confirm PlantProcess.Api is running on " +
        "http://localhost:5063 and VITE_API_BASE_URL points to the same URL." +
        (detail ? ` Details: ${detail}` : "")
      );

    case "server-error":
      return (
        "Backend API returned a server error during login. Check the API console " +
        "and database connectivity, then retry."
      );

    default:
      return (
        "Authentication failed for an unexpected reason." +
        (detail ? ` Details: ${detail}` : "")
      );
  }
}

interface AuthContextValue {
  user: AuthenticatedUser | null;
  isAuthenticated: boolean;
  isBootstrapping: boolean;
  bootstrapError: string | null;
  bootstrapAttemptCount: number;
  logout: () => void;
  retryBootstrap: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [isBootstrapping, setIsBootstrapping] = useState(true);
  const [bootstrapError, setBootstrapError] = useState<string | null>(null);
  const [bootstrapAttemptCount, setBootstrapAttemptCount] = useState(0);

  const inFlightRef = useRef<Promise<void> | null>(null);
  const automaticAttemptsRef = useRef(0);
  const lastErrorKindRef = useRef<AuthErrorKind | null>(null);

  const applyAuthenticatedUser = useCallback((response: {
    userName: string;
    displayName?: string | null;
    role: string;
    expiresAtUtc: string;
    scopes?: string[];
  }) => {
    setUser({
      userName: response.userName,
      displayName: response.displayName,
      role: response.role,
      expiresAtUtc: response.expiresAtUtc,
      scopes: response.scopes ?? [],
    });
    setBootstrapError(null);
    automaticAttemptsRef.current = 0;
    setBootstrapAttemptCount(0);
    lastErrorKindRef.current = null;
  }, []);

  const bootstrap = useCallback(
    async (reason: AuthBootstrapReason = "initial", force = false) => {
      if (inFlightRef.current) {
        await inFlightRef.current;
        return;
      }

      const run = async () => {
        setIsBootstrapping(true);

        try {
          const existing = apiClient.getAuthenticatedUser();
          if (!force && isTokenStillValid(existing)) {
            setUser(existing);
            setBootstrapError(null);
            return;
          }

          if (!DEMO_PASS.trim()) {
            const message = buildAuthMessage("missing-smoke-password");
            apiClient.clearAuthentication();
            setUser(null);
            setBootstrapError(message);
            lastErrorKindRef.current = "missing-smoke-password";
            return;
          }

          if (reason !== "manual") {
            if (automaticAttemptsRef.current >= MAX_AUTO_BOOTSTRAP_ATTEMPTS) {
              setBootstrapError(
                "Automatic login stopped after 3 failed attempts. " +
                "Fix the credentials or backend connection, then press Retry."
              );
              return;
            }

            const attemptIndex = automaticAttemptsRef.current;
            automaticAttemptsRef.current += 1;
            setBootstrapAttemptCount(automaticAttemptsRef.current);

            const delay = AUTH_RETRY_BACKOFF_MS[attemptIndex] ?? 1500;
            if (delay > 0) await sleep(delay);
          } else {
            automaticAttemptsRef.current = 0;
            setBootstrapAttemptCount(0);
            setBootstrapError(null);
          }

          const response = await apiClient.login(DEMO_USER, DEMO_PASS);
          applyAuthenticatedUser(response);
        } catch (err) {
          const kind = classifyAuthError(err);
          lastErrorKindRef.current = kind;

          if (kind === "invalid-credentials" || kind === "forbidden") {
            automaticAttemptsRef.current = MAX_AUTO_BOOTSTRAP_ATTEMPTS;
            setBootstrapAttemptCount(MAX_AUTO_BOOTSTRAP_ATTEMPTS);
          }

          apiClient.clearAuthentication();
          setUser(null);
          setBootstrapError(buildAuthMessage(kind, err));
        } finally {
          setIsBootstrapping(false);
        }
      };

      inFlightRef.current = run();
      try {
        await inFlightRef.current;
      } finally {
        inFlightRef.current = null;
      }
    },
    [applyAuthenticatedUser],
  );

  useEffect(() => {
    void bootstrap("initial");
  }, [bootstrap]);

  useEffect(() => {
    function handleAuthFailure(event: Event) {
      const customEvent = event as CustomEvent<{
        status?: number;
        path?: string;
        responseText?: string;
      }>;

      const status = customEvent.detail?.status;

      if (status === 401) {
        apiClient.clearAuthentication();
        setUser(null);
        setBootstrapError(buildAuthMessage("invalid-credentials"));
        automaticAttemptsRef.current = MAX_AUTO_BOOTSTRAP_ATTEMPTS;
        setBootstrapAttemptCount(MAX_AUTO_BOOTSTRAP_ATTEMPTS);
        return;
      }

      if (status === 403) {
        apiClient.clearAuthentication();
        setUser(null);
        setBootstrapError(buildAuthMessage("forbidden"));
        automaticAttemptsRef.current = MAX_AUTO_BOOTSTRAP_ATTEMPTS;
        setBootstrapAttemptCount(MAX_AUTO_BOOTSTRAP_ATTEMPTS);
        return;
      }

      if (lastErrorKindRef.current !== "invalid-credentials") {
        void bootstrap("auth-failure", true);
      }
    }

    window.addEventListener("plantprocess:auth-failure", handleAuthFailure);
    return () =>
      window.removeEventListener("plantprocess:auth-failure", handleAuthFailure);
  }, [bootstrap]);

  useEffect(() => {
    if (!user) return;

    const msUntilRefresh =
      new Date(user.expiresAtUtc).getTime() - Date.now() - 5 * 60_000;

    if (msUntilRefresh <= 0) return;

    const timer = window.setTimeout(
      () => void bootstrap("token-refresh", true),
      msUntilRefresh,
    );

    return () => window.clearTimeout(timer);
  }, [user, bootstrap]);

  const logout = useCallback(() => {
    apiClient.logout();
    setUser(null);
    setBootstrapError("Signed out.");
    automaticAttemptsRef.current = 0;
    setBootstrapAttemptCount(0);
  }, []);

  const retryBootstrap = useCallback(() => {
    automaticAttemptsRef.current = 0;
    setBootstrapAttemptCount(0);
    void bootstrap("manual", true);
  }, [bootstrap]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: !!user,
      isBootstrapping,
      bootstrapError,
      bootstrapAttemptCount,
      logout,
      retryBootstrap,
    }),
    [
      user,
      isBootstrapping,
      bootstrapError,
      bootstrapAttemptCount,
      logout,
      retryBootstrap,
    ],
  );

  return (
    <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside AuthProvider");
  return ctx;
}
