// ============================================================
// FILE: Frontend/PlantProcess.Web/src/state/AuthContext.tsx
//
// Auto-bootstrap auth: checks localStorage for a valid token,
// re-logs in automatically with demo credentials when expired
// or absent, and listens for 401 events to re-authenticate.
// ============================================================

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import {
  apiClient,
  type AuthenticatedUser,
} from "../api/http/apiClient";

// Demo credentials — match playwright.config.ts bootstrap values.
// Override via .env.local: VITE_SMOKE_USERNAME / VITE_SMOKE_PASSWORD
const DEMO_USER =
  (import.meta.env.VITE_SMOKE_USERNAME as string | undefined) ?? "admin";
const DEMO_PASS =
  (import.meta.env.VITE_SMOKE_PASSWORD as string | undefined) ??
  "ChangeMe123!";

// Token is considered expired when < 60 s remain.
const EXPIRY_BUFFER_MS = 60_000;

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

// ── Context shape ─────────────────────────────────────────────
interface AuthContextValue {
  user: AuthenticatedUser | null;
  isAuthenticated: boolean;
  isBootstrapping: boolean;
  bootstrapError: string | null;
  logout: () => void;
  retryBootstrap: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

// ── Provider ──────────────────────────────────────────────────
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [isBootstrapping, setIsBootstrapping] = useState(true);
  const [bootstrapError, setBootstrapError] = useState<string | null>(null);

  const bootstrap = useCallback(async () => {
    setIsBootstrapping(true);
    setBootstrapError(null);

    // 1 — Reuse existing valid session
    const existing = apiClient.getAuthenticatedUser();
    if (isTokenStillValid(existing)) {
      setUser(existing);
      setIsBootstrapping(false);
      return;
    }

    // 2 — Auto-login with demo credentials
    try {
      const response = await apiClient.login(DEMO_USER, DEMO_PASS);
      setUser({
        userName: response.userName,
        displayName: response.displayName,
        role: response.role,
        expiresAtUtc: response.expiresAtUtc,
        scopes: response.scopes ?? [],
      });
    } catch (err) {
      const msg =
        err instanceof Error ? err.message : "Unknown connection error";
      setBootstrapError(
        `Cannot reach the backend API. ` +
          `Ensure PlantProcess.Api is running on port 5063. (${msg})`
      );
      setUser(null);
    } finally {
      setIsBootstrapping(false);
    }
  }, []);

  // Run once on mount
  useEffect(() => {
    void bootstrap();
  }, [bootstrap]);

  // Re-authenticate whenever any API call returns 401 or 403
  useEffect(() => {
    function handleAuthFailure() {
      void bootstrap();
    }
    window.addEventListener("plantprocess:auth-failure", handleAuthFailure);
    return () =>
      window.removeEventListener(
        "plantprocess:auth-failure",
        handleAuthFailure
      );
  }, [bootstrap]);

  // Proactive token refresh — re-login 5 min before expiry
  useEffect(() => {
    if (!user) return;
    const msUntilExpiry =
      new Date(user.expiresAtUtc).getTime() - Date.now() - 5 * 60_000;
    if (msUntilExpiry <= 0) return;
    const timer = setTimeout(() => void bootstrap(), msUntilExpiry);
    return () => clearTimeout(timer);
  }, [user, bootstrap]);

  const logout = useCallback(() => {
    apiClient.logout();
    setUser(null);
    void bootstrap();
  }, [bootstrap]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: !!user,
      isBootstrapping,
      bootstrapError,
      logout,
      retryBootstrap: bootstrap,
    }),
    [user, isBootstrapping, bootstrapError, logout, bootstrap]
  );

  return (
    <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
  );
}

// ── Hook ──────────────────────────────────────────────────────
export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside AuthProvider");
  return ctx;
}
