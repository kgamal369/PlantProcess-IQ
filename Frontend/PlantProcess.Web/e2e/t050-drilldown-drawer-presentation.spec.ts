// T-050 (presentation half). DRAWER TOKENS, LOGICAL POSITIONING, RTL, MOTION.
//
// SCOPE AND ITS LIMIT, STATED UP FRONT.
// This certifies the drawer's PRESENTATION contract only: that its colours
// resolve from the design tokens, that it sits and slides from the inline end
// in both writing directions, and that the existing reduced-motion suppression
// actually works in a browser.
//
// It does NOT certify the drill-down chain - clicked point, population,
// provenance handle, source evidence. That chain is blocked on PR-050-01,
// which must produce a ProvenanceHandleRef for an aggregated query row. T-050
// stays OPEN until that lands and is integrated.
//
// WHY A PROBE ELEMENT RATHER THAN THE REAL DRAWER.
// Opening the real drawer requires a chart point that calls openDrilldown, and
// the reachability of that path from /dashboard is exactly what PR-050-01
// touches. A probe carrying the drawer's class is evaluated by the real
// browser against the real built stylesheet, so the cascade, the tokens and
// the direction flip are genuinely proven. What is NOT proven is that the
// drawer opens - and this file does not claim it.
//
// TWO ORACLE CORRECTIONS, BOTH LEARNED FROM A FAILING RUN:
//
//   1. GEOMETRY IS READ FROM THE LAYOUT BOX, NOT THE PAINTED RECT.
//      The first version measured getBoundingClientRect on an element that had
//      just been told to animate in, and so measured the keyframe's opening
//      frame: LTR reported right = 1920 instead of 1440, exactly one width of
//      translateX(+100%), and RTL reported left = -480, exactly one width of
//      translateX(-100%). Both numbers were the CSS working. offsetLeft and
//      offsetWidth describe the laid-out box and ignore transforms, so the
//      settled position is read without racing the animation.
//
//   2. COLOURS ARE COMPARED THROUGH THE SAME SERIALISATION.
//      Chromium serialises color-mix output as color(srgb 0.00784314 ...),
//      never as rgb(2, 132, 199), so comparing integer channels could not
//      match however correct the colour was. The expected value is now
//      produced by asking the browser to compute the identical color-mix
//      expression, and the two computed strings are compared.

import { test, expect, type Page } from "@playwright/test";
import { prepareAuthenticatedPage } from "./helpers/hardening";

type Probe = {
  animationName: string;
  borderInlineStartColor: string;
  borderLeftWidth: string;
  borderRightWidth: string;
  offsetLeft: number;
  offsetWidth: number;
  viewportWidth: number;
  backgroundImage: string;
  headerBorderColor: string;
  tokenBorder: string;
  tokenBorderSoft: string;
  tokenBg0: string;
  tokenBg1: string;
  expectedGlow: string;
};

/** Mounts a probe carrying the drawer's own classes, reads what the real
 *  stylesheet computes for it, then removes it. Expected colours are resolved
 *  by asking the browser to compute the same expressions, so every comparison
 *  is between two browser-produced strings rather than between a computed
 *  value and a hand-written guess at its serialisation. */
async function probeDrawer(page: Page, direction: "ltr" | "rtl"): Promise<Probe> {
  return page.evaluate((dir) => {
    const previous = document.documentElement.getAttribute("dir");
    document.documentElement.setAttribute("dir", dir);

    const computeColour = (expression: string) => {
      const swatch = document.createElement("span");
      swatch.style.position = "fixed";
      swatch.style.backgroundColor = expression;
      document.body.appendChild(swatch);
      const value = getComputedStyle(swatch).backgroundColor;
      swatch.remove();
      return value;
    };

    const drawer = document.createElement("aside");
    drawer.className = "drilldown-drawer";
    drawer.setAttribute("data-t050-probe", "true");

    const header = document.createElement("div");
    header.className = "drilldown-drawer__header";
    drawer.appendChild(header);
    document.body.appendChild(drawer);

    const style = getComputedStyle(drawer);
    const headerStyle = getComputedStyle(header);

    const result = {
      animationName: style.animationName,
      borderInlineStartColor: style.borderInlineStartColor,
      borderLeftWidth: style.borderLeftWidth,
      borderRightWidth: style.borderRightWidth,
      // Layout box, not painted rect: unaffected by the enter transform.
      offsetLeft: Math.round(drawer.offsetLeft),
      offsetWidth: Math.round(drawer.offsetWidth),
      viewportWidth: window.innerWidth,
      backgroundImage: style.backgroundImage,
      headerBorderColor: headerStyle.borderBlockEndColor,
      tokenBorder: computeColour("var(--pp-border)"),
      tokenBorderSoft: computeColour("var(--pp-border-soft)"),
      tokenBg0: computeColour("var(--pp-bg-0)"),
      tokenBg1: computeColour("var(--pp-bg-1)"),
      expectedGlow: computeColour("color-mix(in srgb, var(--pp-cyan) 13%, transparent)"),
    };

    drawer.remove();
    if (previous === null) document.documentElement.removeAttribute("dir");
    else document.documentElement.setAttribute("dir", previous);

    return result;
  }, direction);
}

/** rgb(4, 16, 31) and rgb(4,16,31) are the same colour. */
function squash(value: string) {
  return value.replace(/\s+/g, "");
}

async function openDashboard(page: Page, request: Parameters<typeof prepareAuthenticatedPage>[1]) {
  await page.setViewportSize({ width: 1440, height: 900 });
  await prepareAuthenticatedPage(page, request);
  await page.goto("/dashboard", { waitUntil: "domcontentloaded", timeout: 30000 });
  await expect(page.locator(".dashboard-grid-layout-shell")).toBeVisible({ timeout: 30000 });
}

test.describe("T-050 drill-down drawer presentation", () => {
  test("every drawer colour resolves from the design token set", async ({ page, request }) => {
    await openDashboard(page, request);
    const probe = await probeDrawer(page, "ltr");

    // The border is the token, not rgba(90, 194, 255, 0.22).
    expect(
      squash(probe.borderInlineStartColor),
      "the drawer's inline-start border is not --pp-border"
    ).toBe(squash(probe.tokenBorder));

    // The header divider is the token, not rgba(90, 194, 255, 0.14).
    expect(
      squash(probe.headerBorderColor),
      "the drawer header's block-end border is not --pp-border-soft"
    ).toBe(squash(probe.tokenBorderSoft));

    // The gradient stops are the background tokens, not #06111f and #030813.
    const background = squash(probe.backgroundImage);
    expect(background, "the drawer gradient does not carry --pp-bg-1").toContain(squash(probe.tokenBg1));
    expect(background, "the drawer gradient does not carry --pp-bg-0").toContain(squash(probe.tokenBg0));

    // The glow is the same color-mix the browser computes from --pp-cyan,
    // compared as the browser serialises it.
    expect(
      background,
      "the drawer glow is not the color-mix of --pp-cyan; expected " + probe.expectedGlow
    ).toContain(squash(probe.expectedGlow));
  });

  test("the drawer sits and slides from the inline end in LTR", async ({ page, request }) => {
    await openDashboard(page, request);
    const probe = await probeDrawer(page, "ltr");

    expect(
      probe.offsetLeft + probe.offsetWidth,
      "the drawer's laid-out box is not flush with the inline end in LTR"
    ).toBe(probe.viewportWidth);
    expect(probe.borderLeftWidth, "the inline-start border did not resolve to the left edge in LTR").toBe("1px");
    expect(probe.borderRightWidth, "a border appeared on the wrong edge in LTR").toBe("0px");
    expect(probe.animationName, "the existing enter transition is not applied").toBe("ppiq-drawer-in");
  });

  test("the drawer mirrors to the inline end in RTL", async ({ page, request }) => {
    await openDashboard(page, request);
    const probe = await probeDrawer(page, "rtl");

    expect(probe.offsetLeft, "the drawer's laid-out box did not mirror to the inline end in RTL").toBe(0);
    expect(probe.borderRightWidth, "the inline-start border did not resolve to the right edge in RTL").toBe("1px");
    expect(probe.borderLeftWidth, "a border remained on the wrong edge in RTL").toBe("0px");
    expect(probe.animationName, "the enter transition is missing in RTL").toBe("ppiq-drawer-in");
  });

  test("prefers-reduced-motion suppresses the transition", async ({ page, request }) => {
    await openDashboard(page, request);

    const withMotion = await probeDrawer(page, "ltr");
    expect(withMotion.animationName, "no transition to suppress; the guard would prove nothing").toBe("ppiq-drawer-in");

    await page.emulateMedia({ reducedMotion: "reduce" });
    const reduced = await probeDrawer(page, "ltr");

    expect(
      reduced.animationName,
      "prefers-reduced-motion did not suppress the drawer transition"
    ).toBe("none");
  });
});
