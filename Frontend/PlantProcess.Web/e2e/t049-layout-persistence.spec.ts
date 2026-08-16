// T-049. LAYOUT DRAG, RESIZE, SAVE, HARD RELOAD - ISOLATED CERTIFICATION.
//
// THE THREE ACCEPTANCE LAWS
//   1. saved canonical        != original canonical
//   2. rendered-after-save    == saved canonical
//   3. rendered-after-reload  == saved canonical
//
// "Changed" means the stable post-save state, never the pixels that happened to
// exist immediately after mouse-up.
//
// ISOLATION. All three viewport cases mutate the same persisted dashboard
// definition, so each one snapshots the definition's layoutJson on entry and
// PATCHes it back in a finally block. A viewport cannot contaminate the next,
// and a failed run cannot poison the following one. Run with --workers=1.
//
// RELATIVE MUTATION. The interaction is computed from the widget's own current
// canonical geometry, so the target is guaranteed different from the start.
// A fixed destination can already be the persisted state, which is what made
// earlier runs report "the interaction did not take".
//
// CANONICAL IS PRIMARY. The persistence authority is the layout document:
// PATCH body against the definition GET after the reload, per breakpoint, per
// widget, per field. Pixels are the secondary browser proof, reached by polling
// until the DOM converges on the canonical geometry. No post-reload sleeps.

import { test, expect, type Page, type Response } from "@playwright/test";
import { prepareAuthenticatedPage } from "./helpers/hardening";
import { apiBaseUrl } from "./helpers/auth";

const VIEWPORTS = [
  { name: "1920x1080", width: 1920, height: 1080 },
  { name: "1440x900", width: 1440, height: 900 },
  { name: "1280x800", width: 1280, height: 800 },
];

// DashboardGridLayout.tsx: rowHeight={42} margin={[18, 18]} containerPadding={[0, 0]}
const GRID_MARGIN = 18;
const GRID_ROW_HEIGHT = 42;
const GRID_TOLERANCE = 4;
const CONVERGENCE_TIMEOUT_MS = 10000;

type Geometry = { i: string; x: number; y: number; w: number; h: number };
type LayoutDocument = Record<string, Geometry[]>;

function columnsForBreakpoint(breakpoint: string): number {
  if (breakpoint === "lg") return 12;
  if (breakpoint === "md") return 10;
  if (breakpoint === "sm") return 6;
  if (breakpoint === "xs") return 4;
  return 2;
}

/** breakpoints={{ lg: 1400, md: 1100, sm: 800, xs: 560, xxs: 0 }}, selected by
 *  react-grid-layout from the CONTAINER width, not the viewport. */
function breakpointForWidth(width: number): string {
  if (width >= 1400) return "lg";
  if (width >= 1100) return "md";
  if (width >= 800) return "sm";
  if (width >= 560) return "xs";
  return "xxs";
}

function layoutsFrom(layoutJson: string): LayoutDocument {
  const parsed = JSON.parse(layoutJson) as Record<string, unknown>;
  const document_: LayoutDocument = {};

  for (const key of Object.keys(parsed)) {
    const value = parsed[key];
    if (!Array.isArray(value)) continue;

    const entries = value
      .filter((entry) => entry !== null && typeof entry === "object" && typeof (entry as Geometry).i === "string")
      .map((entry) => {
        const item = entry as Record<string, unknown>;
        return {
          i: String(item.i),
          x: Number(item.x),
          y: Number(item.y),
          w: Number(item.w),
          h: Number(item.h),
        };
      })
      .sort((a, b) => a.i.localeCompare(b.i));

    if (entries.length > 0) document_[key] = entries;
  }

  return document_;
}

/** The definition id, its layoutJson, and widget id -> widget code.
 *  The canonical layout keys entries by widget.id; the DOM identity is
 *  data-widget-code. The map is the join between them. */
type Definition = { id: string; layoutJson: string; codes: Map<string, string> };

function parseDefinition(body: string): Definition | null {
  let root: unknown;
  try {
    root = JSON.parse(body);
  } catch {
    return null;
  }

  const codes = new Map<string, string>();
  let found: Definition | null = null;

  const visit = (node: unknown) => {
    if (Array.isArray(node)) {
      node.forEach(visit);
      return;
    }
    if (node === null || typeof node !== "object") return;

    const record = node as Record<string, unknown>;

    if (typeof record.id === "string" && typeof record.widgetCode === "string" && record.widgetCode !== "") {
      codes.set(record.id, record.widgetCode);
    }
    if (found === null && typeof record.id === "string" && typeof record.layoutJson === "string") {
      found = { id: record.id, layoutJson: record.layoutJson, codes };
    }

    Object.keys(record).forEach((key) => visit(record[key]));
  };

  visit(root);
  return found;
}

function latestDefinition(bodies: string[], fromIndex: number): Definition | null {
  for (let index = bodies.length - 1; index >= fromIndex; index--) {
    const definition = parseDefinition(bodies[index]);
    if (definition !== null && definition.layoutJson.trim() !== "") return definition;
  }
  return null;
}

/** Returns the first per-field difference between two layout documents, or null. */
function firstDocumentMismatch(left: LayoutDocument, right: LayoutDocument): string | null {
  const leftKeys = Object.keys(left).sort();
  const rightKeys = Object.keys(right).sort();

  if (leftKeys.join(",") !== rightKeys.join(",")) {
    return "breakpoint set differs: [" + leftKeys.join(",") + "] vs [" + rightKeys.join(",") + "]";
  }

  for (const key of leftKeys) {
    if (left[key].length !== right[key].length) {
      return key + ": " + left[key].length + " entries vs " + right[key].length;
    }

    for (let index = 0; index < left[key].length; index++) {
      const a = left[key][index];
      const b = right[key][index];
      if (a.i !== b.i) return key + " position " + index + ": " + a.i + " vs " + b.i;

      for (const field of ["x", "y", "w", "h"] as const) {
        if (a[field] !== b[field]) {
          return key + " " + a.i + "." + field + ": " + a[field] + " vs " + b[field];
        }
      }
    }
  }

  return null;
}

/** DOM geometry relative to the grid shell, so page scroll and page chrome
 *  cannot masquerade as a layout difference. */
async function captureDom(page: Page): Promise<{ shellWidth: number; items: Geometry[] }> {
  return page.evaluate(() => {
    const shell = document.querySelector<HTMLElement>(".dashboard-grid-layout-shell");
    const shellBox = shell === null ? null : shell.getBoundingClientRect();
    const originLeft = shellBox === null ? 0 : shellBox.left;
    const originTop = shellBox === null ? 0 : shellBox.top;

    const items = Array.from(document.querySelectorAll<HTMLElement>(".react-grid-item"))
      .map((element) => {
        const box = element.getBoundingClientRect();
        return {
          i: element.getAttribute("data-widget-code") ?? "",
          x: Math.round(box.left - originLeft),
          y: Math.round(box.top - originTop),
          w: Math.round(box.width),
          h: Math.round(box.height),
        };
      })
      .filter((item) => item.i !== "")
      .sort((a, b) => a.i.localeCompare(b.i));

    return { shellWidth: shellBox === null ? -1 : Math.round(shellBox.width), items };
  });
}

/** react-grid-layout's calcGridItemPosition, from the product's constants. */
function expectedGeometry(
  entries: Geometry[],
  codes: Map<string, string>,
  renderedCodes: string[],
  shellWidth: number,
  breakpoint: string
): Geometry[] {
  const columns = columnsForBreakpoint(breakpoint);
  const columnWidth = (shellWidth - GRID_MARGIN * (columns - 1)) / columns;
  const expected: Geometry[] = [];

  for (const entry of entries) {
    const code = codes.get(entry.i);
    if (code === undefined) continue;
    if (renderedCodes.indexOf(code) < 0) continue;

    expected.push({
      i: code,
      x: Math.round((columnWidth + GRID_MARGIN) * entry.x),
      y: Math.round((GRID_ROW_HEIGHT + GRID_MARGIN) * entry.y),
      w: Math.round(columnWidth * entry.w + Math.max(0, entry.w - 1) * GRID_MARGIN),
      h: Math.round(GRID_ROW_HEIGHT * entry.h + Math.max(0, entry.h - 1) * GRID_MARGIN),
    });
  }

  return expected.sort((a, b) => a.i.localeCompare(b.i));
}

/** "MATCHED", or a sentence naming the first widget not yet at its canonical
 *  position. Poll predicate, so an arrived HTTP response is never mistaken for
 *  an applied layout. */
async function convergenceDiff(page: Page, document_: LayoutDocument, codes: Map<string, string>): Promise<string> {
  const dom = await captureDom(page);
  if (dom.shellWidth <= 0) return "the grid shell is not in the DOM yet";
  if (dom.items.length === 0) return "no grid item carries an identity yet";

  const breakpoint = breakpointForWidth(dom.shellWidth);
  const entries = document_[breakpoint];
  if (entries === undefined) return "the document carries no " + breakpoint + " layout";

  const renderedCodes = dom.items.map((item) => item.i);
  const expected = expectedGeometry(entries, codes, renderedCodes, dom.shellWidth, breakpoint);
  if (expected.length === 0) return "no canonical entry resolves to a rendered widget at " + breakpoint;

  for (const target of expected) {
    const actual = dom.items.find((item) => item.i === target.i);
    if (actual === undefined) return target.i + " is not rendered";

    for (const field of ["x", "y", "w", "h"] as const) {
      const drift = Math.abs(actual[field] - target[field]);
      if (drift > GRID_TOLERANCE) {
        return target.i + "." + field + " is " + actual[field] + " but the layout says " + target[field]
          + " (drift " + drift + "px, " + breakpoint + ", shell " + dom.shellWidth + ")";
      }
    }
  }

  return "MATCHED";
}

async function waitForConvergence(
  page: Page,
  document_: LayoutDocument,
  codes: Map<string, string>,
  moment: string
) {
  await expect
    .poll(() => convergenceDiff(page, document_, codes), {
      timeout: CONVERGENCE_TIMEOUT_MS,
      intervals: [100, 200, 300, 500, 500, 1000],
      message: "the rendered grid never reached the canonical layout " + moment,
    })
    .toBe("MATCHED");
}

/** Asserts the press point actually lands on the handle.
 *
 *  A one-row widget renders 42px tall while the drag handle is 36px inside a
 *  16px-padded header, so the handle overflows a card with overflow:hidden and
 *  the press reaches something else. react-grid-layout then ignores the press
 *  and the interaction silently does nothing - which is exactly how three runs
 *  reported "the interaction changed nothing" with no other symptom. */
async function assertPressLands(page: Page, x: number, y: number, code: string, handleClass: string) {
  const landedOn = await page.evaluate((probe) => {
    const element = document.elementFromPoint(probe.x, probe.y);
    if (element === null) return "NOTHING (the point is outside the viewport)";
    if (element.closest("." + probe.handleClass) !== null) return "HANDLE";

    const card = element.closest("[data-widget-code]");
    const owner = card === null ? "no widget" : card.getAttribute("data-widget-code");
    const description = element instanceof HTMLElement && element.className !== ""
      ? String(element.className)
      : element.tagName;

    return description + " inside " + owner;
  }, { x, y, handleClass });

  expect(
    landedOn,
    "the press for " + code + " at (" + Math.round(x) + ", " + Math.round(y) + ") landed on "
    + landedOn + " rather than its " + handleClass
    + ". react-grid-layout ignores a press outside the declared handle, so the interaction would do nothing."
  ).toBe("HANDLE");
}

/** The three widgets to mutate: tallest first, then top-most.
 *
 *  Alphabetical selection picked the one-row widgets at the bottom of the
 *  board, whose handles are unusable and which sit thousands of pixels below
 *  the fold. Size and position are read from the widget's own canonical entry,
 *  so the choice stays relative to whatever is actually persisted. */
function chooseTargets(entryByCode: Map<string, Geometry>, renderedCodes: string[]): string[] {
  return renderedCodes
    .filter((code) => entryByCode.has(code))
    .map((code) => ({ code, entry: entryByCode.get(code) as Geometry }))
    .sort((a, b) =>
      (b.entry.h - a.entry.h)
      || (b.entry.w - a.entry.w)
      || (a.entry.y - b.entry.y)
      || (a.entry.x - b.entry.x)
      || a.code.localeCompare(b.code))
    .slice(0, 3)
    .map((candidate) => candidate.code);
}

async function enterEditMode(page: Page) {
  const toggle = page.getByTestId("workspace-edit-toggle");
  await expect(toggle, "the layout edit toggle is not on this page").toBeVisible({ timeout: 20000 });

  if ((await toggle.getAttribute("aria-pressed")) !== "true") {
    await toggle.click();
  }

  await expect(page.locator("[data-edit-mode='on']")).toBeVisible({ timeout: 10000 });
}

async function dragFrom(page: Page, code: string, dx: number, dy: number) {
  // The grid declares draggableHandle=".dashboard-widget__drag-handle"; a press
  // anywhere else is ignored by react-grid-layout.
  const handle = page.locator("[data-widget-code='" + code + "'] .dashboard-widget__drag-handle").first();
  await expect(handle, code + " exposes no drag handle; edit mode did not reveal it").toBeVisible();

  // page.mouse works in VIEWPORT coordinates and does not scroll. A widget
  // below the fold would otherwise be "dragged" in empty space.
  await handle.scrollIntoViewIfNeeded();

  const box = await handle.boundingBox();
  if (!box) throw new Error(code + " drag handle has no bounding box");

  const startX = box.x + box.width / 2;
  const startY = box.y + box.height / 2;

  await assertPressLands(page, startX, startY, code, "dashboard-widget__drag-handle");

  await page.mouse.move(startX, startY);
  await page.mouse.down();
  await page.mouse.move(startX + dx / 2, startY + dy / 2, { steps: 8 });
  await page.mouse.move(startX + dx, startY + dy, { steps: 12 });
  await page.mouse.up();
  await page.waitForTimeout(400);
}

async function resizeFrom(page: Page, code: string, dx: number, dy: number) {
  const handle = page.locator("[data-widget-code='" + code + "'] .react-resizable-handle").first();
  await expect(handle, code + " exposes no resize handle; edit mode did not reveal it").toBeVisible();

  await handle.scrollIntoViewIfNeeded();

  const box = await handle.boundingBox();
  if (!box) throw new Error(code + " resize handle has no bounding box");

  const startX = box.x + box.width / 2;
  const startY = box.y + box.height / 2;

  await assertPressLands(page, startX, startY, code, "react-resizable-handle");

  await page.mouse.move(startX, startY);
  await page.mouse.down();
  await page.mouse.move(startX + dx / 2, startY + dy / 2, { steps: 8 });
  await page.mouse.move(startX + dx, startY + dy, { steps: 12 });
  await page.mouse.up();
  await page.waitForTimeout(400);
}

/** +1 column if there is room to the right, -1 if there is room to the left,
 *  0 when the widget spans the whole grid. Computed from the widget's own
 *  canonical geometry, so the target cannot already be the persisted state. */
function horizontalDirection(entry: Geometry, columns: number): number {
  if (entry.x + entry.w < columns) return 1;
  if (entry.x > 0) return -1;
  return 0;
}

test.describe("T-049 layout persistence", () => {
  for (const viewport of VIEWPORTS) {
    test("drag, resize, save and hard reload restore the layout at " + viewport.name, async ({ page, request }) => {
      test.setTimeout(180000);

      const bodies: string[] = [];
      const collect = (response: Response) => {
        if (response.request().method() !== "GET") return;
        const url = response.url();
        if (!/\/analytics\/dashboard\/definitions/.test(url)) return;
        if (/\/layout$/.test(url)) return;

        void response.text().then((text) => { bodies.push(text); }).catch(() => { /* unreadable body */ });
      };

      let definitionId = "";
      let originalLayoutJson = "";
      let token = "";

      try {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });

        token = await prepareAuthenticatedPage(page, request);
        page.on("response", collect);

        await page.goto("/dashboard", { waitUntil: "domcontentloaded", timeout: 30000 });
        await expect(page.locator(".react-grid-item").first()).toBeVisible({ timeout: 30000 });

        // ---------------------------------------------- ORIGINAL, and the snapshot
        await expect
          .poll(() => (latestDefinition(bodies, 0) === null ? 0 : 1), {
            timeout: CONVERGENCE_TIMEOUT_MS,
            message: "no dashboard-definition response carried a layout document",
          })
          .toBe(1);

        const original = latestDefinition(bodies, 0) as Definition;
        definitionId = original.id;
        originalLayoutJson = original.layoutJson;

        const originalDocument = layoutsFrom(originalLayoutJson);
        await waitForConvergence(page, originalDocument, original.codes, "on the initial load");

        const beforeDom = await captureDom(page);
        const breakpoint = breakpointForWidth(beforeDom.shellWidth);
        const columns = columnsForBreakpoint(breakpoint);
        const columnStep = Math.round((beforeDom.shellWidth - GRID_MARGIN * (columns - 1)) / columns) + GRID_MARGIN;
        const rowStep = GRID_ROW_HEIGHT + GRID_MARGIN;

        expect(
          beforeDom.items.length,
          "T-049 needs at least three widgets on /dashboard and this run rendered " + beforeDom.items.length +
          "; start the API and the web app on the presentation profile"
        ).toBeGreaterThanOrEqual(3);

        const identities = beforeDom.items.map((item) => item.i);
        expect(
          new Set(identities).size,
          "widget identities are not unique: " + identities.join(", ")
        ).toBe(identities.length);

        // Canonical entry per rendered code, for the active breakpoint.
        const entryByCode = new Map<string, Geometry>();
        for (const entry of originalDocument[breakpoint] ?? []) {
          const code = original.codes.get(entry.i);
          if (code !== undefined) entryByCode.set(code, entry);
        }

        const targets = chooseTargets(entryByCode, identities);
        expect(targets.length, "fewer than three rendered widgets carry a canonical entry").toBe(3);
        for (const code of targets) {
          expect(entryByCode.get(code), code + " has no canonical entry at " + breakpoint).toBeDefined();
        }

        const entryA = entryByCode.get(targets[0]) as Geometry;
        const entryB = entryByCode.get(targets[1]) as Geometry;
        const entryC = entryByCode.get(targets[2]) as Geometry;

        // -------------------------------------------------- RELATIVE MUTATION
        await enterEditMode(page);

        const directionA = horizontalDirection(entryA, columns);
        await dragFrom(page, targets[0], directionA * columnStep, directionA === 0 ? rowStep : 0);

        // Height has no upper bound, so +1 row always changes the geometry.
        const directionB = horizontalDirection(entryB, columns);
        await resizeFrom(page, targets[1], directionB * columnStep, rowStep);

        const directionC = horizontalDirection(entryC, columns);
        await dragFrom(page, targets[2], directionC * columnStep, directionC === 0 ? rowStep : 0);

        // The interaction must have moved something on screen before a save can
        // mean anything. Reported with each target's before and after geometry,
        // so a no-op names itself instead of surfacing later as "the saved
        // layout is identical to the original".
        const afterInteraction = await captureDom(page);
        const targetSummary = targets
          .map((code) => {
            const before = beforeDom.items.find((item) => item.i === code);
            const after = afterInteraction.items.find((item) => item.i === code);
            return code + " " + JSON.stringify(before) + " -> " + JSON.stringify(after);
          })
          .join(" | ");

        expect(
          JSON.stringify(afterInteraction.items),
          "the drag and resize moved nothing on screen: " + targetSummary
        ).not.toBe(JSON.stringify(beforeDom.items));

        // ------------------------------------------------------------- SAVE
        const savePromise = page.waitForResponse(
          (response) =>
            /\/analytics\/dashboard\/definitions\//.test(response.url()) &&
            ["PUT", "PATCH", "POST"].includes(response.request().method()),
          { timeout: 30000 }
        );

        await page.getByTestId("workspace-save-layout").click();
        const saveResponse = await savePromise;
        expect(saveResponse.status(), "the layout save must be accepted").toBeLessThan(400);

        const savePayload = saveResponse.request().postData();
        expect(savePayload, "the layout save carried no body").not.toBeNull();

        const savedLayoutJson = (JSON.parse(savePayload as string) as { layoutJson?: string }).layoutJson;
        expect(savedLayoutJson, "the save body carried no layoutJson").toBeDefined();

        const savedDocument = layoutsFrom(savedLayoutJson as string);

        // LAW 1. The interaction produced a genuinely new persisted state.
        expect(
          firstDocumentMismatch(originalDocument, savedDocument),
          "the saved layout is identical to the original; the interaction changed nothing"
        ).not.toBeNull();

        // LAW 2. The screen agrees with what was written. This, not the pixels
        // straight after mouse-up, is the authoritative changed state.
        await waitForConvergence(page, savedDocument, original.codes, "after the save");
        const changedDom = await captureDom(page);

        // ------------------------------------------------------ HARD RELOAD
        const reloadFrom = bodies.length;
        await page.reload({ waitUntil: "load" });
        await expect(page.locator(".react-grid-item").first()).toBeVisible({ timeout: 30000 });

        await expect
          .poll(() => (latestDefinition(bodies, reloadFrom) === null ? 0 : 1), {
            timeout: CONVERGENCE_TIMEOUT_MS,
            message: "the reload issued no dashboard-definition response carrying a layout",
          })
          .toBe(1);

        const reloaded = latestDefinition(bodies, reloadFrom) as Definition;
        const reloadedDocument = layoutsFrom(reloaded.layoutJson);

        // The persistence authority: what was written is what comes back.
        expect(
          firstDocumentMismatch(savedDocument, reloadedDocument),
          "PERSISTENCE DEFECT. The layout read back after the reload differs from the one the save wrote."
        ).toBeNull();

        // LAW 3.
        await waitForConvergence(page, savedDocument, reloaded.codes, "after the hard reload");
        const restoredDom = await captureDom(page);

        expect(
          restoredDom.items.map((item) => item.i),
          "a widget identity was lost or duplicated across the save and reload"
        ).toEqual(changedDom.items.map((item) => item.i));

        for (const item of changedDom.items) {
          const match = restoredDom.items.find((candidate) => candidate.i === item.i);
          expect(match, "widget " + item.i + " did not survive the reload").toBeDefined();

          expect(Math.abs(match!.x - item.x), "x drifted for " + item.i).toBeLessThanOrEqual(GRID_TOLERANCE);
          expect(Math.abs(match!.y - item.y), "y drifted for " + item.i).toBeLessThanOrEqual(GRID_TOLERANCE);
          expect(Math.abs(match!.w - item.w), "width drifted for " + item.i).toBeLessThanOrEqual(GRID_TOLERANCE);
          expect(Math.abs(match!.h - item.h), "height drifted for " + item.i).toBeLessThanOrEqual(GRID_TOLERANCE);
        }

        // The restored layout is not merely the original one, which is what
        // fails if the save wrote nothing and the reload fetched the old
        // document back.
        expect(
          JSON.stringify(restoredDom.items),
          "the reload restored the ORIGINAL layout; the save did not persist"
        ).not.toBe(JSON.stringify(beforeDom.items));

        for (const item of restoredDom.items) {
          expect(item.x, "widget " + item.i + " sits left of the grid").toBeGreaterThanOrEqual(-GRID_TOLERANCE);
          expect(item.w, "widget " + item.i + " has no width").toBeGreaterThan(0);
          expect(item.h, "widget " + item.i + " has no height").toBeGreaterThan(0);
        }
      } finally {
        page.off("response", collect);

        // Mandatory, pass or fail: every viewport leaves the dashboard exactly
        // as it found it, so no case can contaminate the next.
        if (definitionId !== "" && originalLayoutJson !== "") {
          const restore = await request.patch(
            apiBaseUrl + "/analytics/dashboard/definitions/" + definitionId + "/layout",
            {
              headers: { Authorization: "Bearer " + token, "Content-Type": "application/json" },
              data: { layoutJson: originalLayoutJson },
            }
          );

          expect(
            restore.status(),
            "the baseline layout was NOT restored (HTTP " + restore.status() + "); the next case would start contaminated"
          ).toBeLessThan(400);
        }
      }
    });
  }
});
