import { phase56ThemeStorageKey, type Phase56Theme } from '../design-system/phase56/phase56Tokens';

const THEME_VALUES: readonly Phase56Theme[] = ['dark', 'light'] as const;

function getPreferredTheme(): Phase56Theme {
  const stored = window.localStorage.getItem(phase56ThemeStorageKey);
  if (stored === 'dark' || stored === 'light') return stored;
  return window.matchMedia?.('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
}

function applyTheme(theme: Phase56Theme, announce = false): void {
  document.documentElement.dataset.theme = theme;
  document.documentElement.style.colorScheme = theme;
  window.localStorage.setItem(phase56ThemeStorageKey, theme);
  const live = document.getElementById('ppiq-phase56-live-region');
  if (announce && live) live.textContent = 'Theme changed to ' + theme + ' mode.';
  const button = document.getElementById('ppiq-phase56-theme-toggle');
  if (button) button.setAttribute('aria-pressed', String(theme === 'light'));
  const state = document.getElementById('ppiq-phase56-theme-state');
  if (state) state.textContent = theme;
}

function ensureSkipLink(): void {
  if (document.querySelector('.ppiq-skip-link')) return;
  const link = document.createElement('a');
  link.className = 'ppiq-skip-link';
  link.href = '#main-content';
  link.textContent = 'Skip to main content';
  document.body.prepend(link);
}

function ensureMainLandmark(): void {
  const existingMain = document.querySelector('main');
  if (existingMain && !existingMain.id) existingMain.id = 'main-content';
  if (document.getElementById('main-content')) return;
  const root = document.getElementById('root');
  if (root) {
    root.setAttribute('role', 'main');
    root.setAttribute('id', 'main-content');
  }
}

function ensureLiveRegion(): void {
  if (document.getElementById('ppiq-phase56-live-region')) return;
  const live = document.createElement('div');
  live.id = 'ppiq-phase56-live-region';
  live.className = 'ppiq-sr-only';
  live.setAttribute('aria-live', 'polite');
  live.setAttribute('aria-atomic', 'true');
  document.body.appendChild(live);
}

function ensureThemeToggle(): void {
  if (document.getElementById('ppiq-phase56-theme-toggle')) return;
  const button = document.createElement('button');
  button.id = 'ppiq-phase56-theme-toggle';
  button.type = 'button';
  button.className = 'phase56-theme-toggle';
  button.setAttribute('aria-label', 'Toggle light and dark theme');
  button.innerHTML = '<span class="phase56-theme-toggle__icon" aria-hidden="true">◐</span><span>Theme</span><span id="ppiq-phase56-theme-state" class="phase56-theme-toggle__state"></span>';
  button.addEventListener('click', () => {
    const current = document.documentElement.dataset.theme === 'light' ? 'light' : 'dark';
    const next = current === 'light' ? 'dark' : 'light';
    applyTheme(next, true);
  });
  document.body.appendChild(button);
}

export function initializePhase56AccessibilityRuntime(): void {
  if (typeof window === 'undefined') return;
  applyTheme(getPreferredTheme());

  window.matchMedia?.('(prefers-color-scheme: light)').addEventListener?.('change', () => {
    const stored = window.localStorage.getItem(phase56ThemeStorageKey);
    if (!THEME_VALUES.includes(stored as Phase56Theme)) applyTheme(getPreferredTheme(), true);
  });

  window.addEventListener('DOMContentLoaded', () => {
    ensureSkipLink();
    ensureMainLandmark();
    ensureLiveRegion();
    ensureThemeToggle();
    applyTheme(getPreferredTheme());
  });

  window.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      document.querySelectorAll('[data-phase56-dismiss-on-escape="true"]').forEach((node) => {
        (node as HTMLElement).dispatchEvent(new CustomEvent('phase56-escape-dismiss', { bubbles: true }));
      });
    }
  });
}

initializePhase56AccessibilityRuntime();
