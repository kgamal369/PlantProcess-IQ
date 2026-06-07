import { expect, test } from '@playwright/test';

const phase56Routes = [
  '/',
  '/dashboard',
  '/materials',
  '/data-quality',
  '/correlations',
  '/ml-readiness',
  '/demo-lifecycle',
  '/admin-preview',
  '/brand',
  '/commercial/license',
  '/mapping-health'
];

// Phase 5/6 accessibility: keyboard shell + landmarks, plus a live-DOM audit of
// labelled controls. missingButtonNames / missingInputs are derived from the page
// so any Serious/Critical labelling regression fails the suite.
test.describe('Phase 5/6 accessibility shell and labelled controls', () => {
  for (const theme of ['dark', 'light'] as const) {
    for (const route of phase56Routes) {
      test(theme + ' theme is accessible on ' + route, async ({ page }) => {
        await page.addInitScript((themeName) => {
          window.localStorage.setItem('plantprocess.theme.v1', themeName as string);
        }, theme);
        await page.goto(route);

        await expect(page.locator('html')).toHaveAttribute('data-theme', theme);
        await expect(page.locator('.ppiq-skip-link')).toBeAttached();
        await expect(page.locator('#ppiq-phase56-theme-toggle')).toBeAttached();
        await page.keyboard.press('Tab');
        const focused = await page.evaluate(() => document.activeElement?.tagName ?? '');
        expect(focused.length).toBeGreaterThan(0);

        const audit = await page.evaluate(() => {
          const isVisible = (el: Element): boolean => {
            const s = window.getComputedStyle(el as HTMLElement);
            if (s.display === 'none' || s.visibility === 'hidden' || Number(s.opacity) === 0) {
              return false;
            }
            const r = (el as HTMLElement).getBoundingClientRect();
            return r.width > 0 && r.height > 0;
          };
          const textFromIds = (ids: string): string =>
            ids
              .split(/\s+/)
              .map((id) => {
                const node = document.getElementById(id);
                return node ? (node.textContent || '').trim() : '';
              })
              .join(' ')
              .trim();
          const accessibleName = (el: Element): string => {
            const ariaLabel = (el.getAttribute('aria-label') || '').trim();
            if (ariaLabel) return ariaLabel;
            const labelledBy = el.getAttribute('aria-labelledby');
            if (labelledBy && textFromIds(labelledBy)) return textFromIds(labelledBy);
            const title = (el.getAttribute('title') || '').trim();
            if (title) return title;
            const text = (el.textContent || '').trim();
            if (text) return text;
            const altImg = el.querySelector('img[alt]');
            return altImg ? (altImg.getAttribute('alt') || '').trim() : '';
          };
          const hasLabel = (el: Element): boolean => {
            if ((el.getAttribute('aria-label') || '').trim()) return true;
            if (el.getAttribute('aria-labelledby')) return true;
            if ((el.getAttribute('title') || '').trim()) return true;
            const id = el.getAttribute('id');
            if (id) {
              const safe = window.CSS && CSS.escape ? CSS.escape(id) : id;
              if (document.querySelector('label[for="' + safe + '"]')) return true;
            }
            return Boolean(el.closest('label'));
          };

          const buttons = Array.from(
            document.querySelectorAll('button, [role="button"]')
          ).filter(isVisible);
          const missingButtonNames = buttons.filter((b) => accessibleName(b) === '').length;

          const controls = Array.from(
            document.querySelectorAll('input, select, textarea')
          ).filter((el) => {
            if (el.tagName === 'INPUT') {
              const type = (el.getAttribute('type') || 'text').toLowerCase();
              if (['hidden', 'button', 'submit', 'reset', 'image'].includes(type)) return false;
            }
            return isVisible(el);
          });
          const missingInputs = controls.filter((el) => !hasLabel(el)).length;

          const region =
            document.querySelector('main, [role="main"], #main-content, .piq-shell') ||
            document.body;
          const visibleTextLength = ((region as HTMLElement).innerText || '').trim().length;

          return { missingButtonNames, missingInputs, visibleTextLength };
        });

        expect(audit.missingButtonNames, 'buttons missing an accessible name on ' + route).toBe(0);
        expect(audit.missingInputs, 'form controls missing a label on ' + route).toBe(0);
        expect(audit.visibleTextLength, 'route renders visible content on ' + route).toBeGreaterThan(0);
      });
    }
  }
});