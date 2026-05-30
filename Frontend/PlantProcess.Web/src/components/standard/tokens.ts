
export const ppiqTokens = {
  color: {
    navy900: "#050B18",
    navy800: "#081426",
    navy700: "#0B1730",
    navy600: "#10213D",
    surface1: "rgba(11, 23, 48, 0.88)",
    surface2: "rgba(16, 33, 61, 0.82)",
    borderSubtle: "rgba(0, 212, 255, 0.12)",
    borderStrong: "rgba(0, 212, 255, 0.34)",
    brandBlue: "#0A84FF",
    brandCyan: "#00D4FF",
    success: "#2CE6A2",
    warning: "#FFD166",
    danger: "#FF4D6D",
    info: "#5AC8FA",
    text: "#EAF6FF",
    textMuted: "#A0BDD8",
    textSoft: "#6D8EAE",
    textDisabled: "#48647F",
  },
  radius: {
    sm: "6px",
    md: "8px",
    lg: "12px",
    xl: "18px",
  },
  spacing: {
    xs: "4px",
    sm: "8px",
    md: "12px",
    lg: "16px",
    xl: "24px",
    xxl: "32px",
  },
  elevation: {
    flat: "none",
    raised: "0 12px 32px rgba(0, 0, 0, 0.28)",
    floating: "0 22px 60px rgba(0, 0, 0, 0.42)",
    glow: "0 0 28px rgba(0, 212, 255, 0.2)",
  },
  motion: {
    fast: "120ms ease",
    normal: "200ms ease-out",
  },
} as const;

export type PpiqTokenMap = typeof ppiqTokens;
