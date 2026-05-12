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
  const saved = localStorage.getItem(STORAGE_KEY);

  if (saved === "dark" || saved === "light") {
    return saved;
  }

  // Default must be dark.
  return "dark";
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<PlantProcessTheme>(() =>
    getInitialTheme()
  );

  useEffect(() => {
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