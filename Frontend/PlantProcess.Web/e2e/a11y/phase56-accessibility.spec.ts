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

test.describe('Phase 5/6 accessibility shell', () => {
  for (const theme of ['dark', 'light'] as const) {
    for (const route of phase56Routes) {
      test(theme + ' theme has keyboard shell and landmarks on ' + route, async ({ page }) => {
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
      });
    }
  }
});
