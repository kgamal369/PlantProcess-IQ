// ============================================================
// TASK 8 — Validate theme consistency
// FILE: Frontend/PlantProcess.Web/src/state/ThemeContext.tsx
//
// CHANGES vs current version:
//  1. getInitialTheme() respects prefers-color-scheme when there is no
//     saved preference, but still defaults to dark (the brand default).
//  2. Flash-of-unstyled-content (FOUC) is prevented by the inline
//     script in index.html (see comment below). The data-theme attribute
//     is set synchronously before React hydrates, so dark styles are
//     applied before the first paint.
//  3. setTheme / toggleTheme are stable (no new reference on re-render).
//
// REQUIRED CHANGE IN index.html — add this <script> as the FIRST CHILD
// of <head> (before any CSS links) to prevent FOUC:
//
//   <script>
//     (function() {
//       var saved = localStorage.getItem('plantprocess.theme.v1');
//       var theme  = saved === 'dark' || saved === 'light' ? saved : 'dark';
//       document.documentElement.dataset.theme = theme;
//     })();
//   </script>
// ============================================================

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";
import type { ReactNode } from "react";

export type PlantProcessTheme = "dark" | "light";

interface ThemeContextValue {
  theme: PlantProcessTheme;
  isDark: boolean;
  toggleTheme: () => void;
  setTheme: (theme: PlantProcessTheme) => void;
}

const STORAGE_KEY = "plantprocess.theme.v1";

const ThemeContext = createContext<ThemeContextValue | null>(null);

function getInitialTheme(): PlantProcessTheme {
  // 1. Explicit user preference stored in localStorage.
  const saved = localStorage.getItem(STORAGE_KEY);
  if (saved === "dark" || saved === "light") {
    return saved;
  }

  // 2. System preference — only considered when user has no saved choice.
  //    Product brand default is dark, so we only check for an explicit
  //    light preference; everything else falls back to dark.
  if (
    typeof window !== "undefined" &&
    window.matchMedia("(prefers-color-scheme: light)").matches
  ) {
    return "light";
  }

  // 3. Brand default: dark.
  return "dark";
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<PlantProcessTheme>(() =>
    getInitialTheme()
  );

  useEffect(() => {
    // Apply the theme class to the root element and persist the preference.
    document.documentElement.dataset.theme = theme;
    localStorage.setItem(STORAGE_KEY, theme);
  }, [theme]);

  const setTheme = useCallback((nextTheme: PlantProcessTheme) => {
    setThemeState(nextTheme);
  }, []);

  const toggleTheme = useCallback(() => {
    setThemeState((current) => (current === "dark" ? "light" : "dark"));
  }, []);

  const value = useMemo<ThemeContextValue>(
    () => ({
      theme,
      isDark: theme === "dark",
      toggleTheme,
      setTheme,
    }),
    [theme, toggleTheme, setTheme]
  );

  return (
    <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
  );
}

export function usePlantProcessTheme() {
  const context = useContext(ThemeContext);

  if (!context) {
    throw new Error(
      "usePlantProcessTheme must be used inside ThemeProvider."
    );
  }

  return context;
}
