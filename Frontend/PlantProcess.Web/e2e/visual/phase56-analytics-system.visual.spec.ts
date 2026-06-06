import { expect, test } from '@playwright/test';

const visualRoutes = ['/dashboard', '/ml-readiness', '/demo-lifecycle', '/admin-preview', '/brand', '/mapping-health'];

test.describe('Phase 5/6 visual smoke snapshots', () => {
  for (const theme of ['dark', 'light'] as const) {
    for (const route of visualRoutes) {
      test(theme + ' ' + route, async ({ page }) => {
        await page.addInitScript((themeName) => window.localStorage.setItem('plantprocess.theme.v1', themeName as string), theme);
        await page.goto(route);
        await expect(page.locator('html')).toHaveAttribute('data-theme', theme);
        await expect(page).toHaveScreenshot('phase56-' + theme + '-' + route.replace(/[^a-z0-9]+/gi, '-') + '.png', { fullPage: true });
      });
    }
  }
});
