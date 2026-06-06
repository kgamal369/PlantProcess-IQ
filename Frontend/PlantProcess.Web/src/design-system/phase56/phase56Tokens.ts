export const phase56BrandTokens = {
  color: {
    navy: '#050B18',
    cyan: '#00D4FF',
    textDark: '#EAF6FF',
    textLight: '#0B1F33',
    surfaceDark: 'rgba(8, 20, 38, 0.86)',
    surfaceLight: 'rgba(255, 255, 255, 0.94)',
    focus: '#FFBF47',
    success: '#35D07F',
    warning: '#F7C948',
    danger: '#FF5F6D'
  },
  spacing: {
    xs: '0.25rem',
    sm: '0.5rem',
    md: '1rem',
    lg: '1.5rem',
    xl: '2rem'
  },
  radius: {
    control: '14px',
    card: '24px',
    pill: '999px'
  },
  typography: {
    ui: 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, Segoe UI, sans-serif',
    mono: 'JetBrains Mono, ui-monospace, SFMono-Regular, Consolas, monospace'
  }
} as const;

export type Phase56Theme = 'dark' | 'light';
export const phase56ThemeStorageKey = 'plantprocess.theme.v1';
