const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

function writeFile(relativePath, content) {
  const file = path.join(root, relativePath);
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, content.replace(/\r\n/g, "\n"), "utf8");
  console.log("Wrote " + relativePath);
}

function appendIfMissing(relativePath, marker, content) {
  const file = path.join(root, relativePath);
  let text = fs.existsSync(file) ? fs.readFileSync(file, "utf8") : "";
  if (!text.includes(marker)) {
    text = text.trimEnd() + "\n\n" + content.trimStart();
    writeFile(relativePath, text);
  } else {
    console.log("No change needed " + relativePath);
  }
}

// ============================================================================
// 1) PageBuilderPage — backend save/load/delete + move/resize UI proof.
// ============================================================================

writeFile(
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.tsx",
  String.raw`import { useMemo, useReducer, useState } from "react";

import { pageBuilderApi, type PageDefinitionDto } from "@/api/pageBuilder";
import { StandardButton } from "@/components/standard/StandardButton";
import {
  StandardInput,
  StandardSelect,
  StandardTextArea,
} from "@/components/standard/StandardFields";
import { StandardCard } from "@/components/standard/StandardSurface";

import {
  createInitialPageBuilderState,
  createPageBuilderPayload,
  normalizePageVisibility,
  pageBuilderReducer,
  type BuilderWidget,
  type PageBuilderState,
  type WidgetKind,
} from "./pageBuilderReducer";

import "./page-builder.css";

const visibilityOptions = [
  { value: "Private", label: "Private" },
  { value: "Shared", label: "Shared" },
  { value: "Public", label: "Public" },
] as const;

const library: Array<{ kind: WidgetKind; title: string; source: string }> = [
  { kind: "kpi", title: "Risk KPI", source: "schema_view:risk_summary" },
  { kind: "bar", title: "Defect breakdown", source: "schema_view:defect_breakdown" },
  { kind: "line", title: "Defect trend", source: "schema_view:quality_daily" },
  { kind: "filter-date", title: "Date range filter", source: "filter:date-range" },
  { kind: "filter-list", title: "List-of-values filter", source: "filter:list-of-values" },
];

type SaveStatusKind = "idle" | "saving" | "saved" | "loading" | "loaded" | "deleted" | "error";

type SaveStatus = {
  kind: SaveStatusKind;
  message: string;
};

const initialStatus: SaveStatus = {
  kind: "idle",
  message: "Not saved yet",
};

export function PageBuilderPage() {
  const [state, dispatch] = useReducer(
    pageBuilderReducer,
    createInitialPageBuilderState(),
  );

  const [status, setStatus] = useState<SaveStatus>(initialStatus);

  const payload = useMemo(() => createPageBuilderPayload(state), [state]);

  async function savePageDefinition() {
    try {
      setStatus({ kind: "saving", message: "Saving PageDefinition..." });

      const saved = await pageBuilderApi.create(payload);

      setStatus({
        kind: "saved",
        message: "Saved PageDefinition '" + saved.slug + "' v" + saved.version,
      });
    } catch (error) {
      setStatus({
        kind: "error",
        message: error instanceof Error ? error.message : "Save failed",
      });
    }
  }

  async function loadPageDefinition() {
    try {
      setStatus({ kind: "loading", message: "Loading PageDefinition..." });

      const loaded = await pageBuilderApi.getBySlug(state.slug);

      dispatch({
        type: "reset",
        state: toPageBuilderState(loaded),
      });

      setStatus({
        kind: "loaded",
        message:
          "Loaded PageDefinition '" +
          loaded.slug +
          "' with " +
          countWidgets(loaded) +
          " widgets",
      });
    } catch (error) {
      setStatus({
        kind: "error",
        message: error instanceof Error ? error.message : "Load failed",
      });
    }
  }

  async function deletePageDefinition() {
    try {
      setStatus({ kind: "saving", message: "Deleting PageDefinition..." });

      const deleted = await pageBuilderApi.delete(state.slug);

      setStatus({
        kind: deleted.deleted ? "deleted" : "loaded",
        message: deleted.deleted
          ? "Deleted PageDefinition '" + state.slug + "'"
          : "No owned PageDefinition was deleted for '" + state.slug + "'",
      });
    } catch (error) {
      setStatus({
        kind: "error",
        message: error instanceof Error ? error.message : "Delete failed",
      });
    }
  }

  return (
    <main className="page-builder-page" data-phase23-page="page-builder">
      <section className="page-builder-page__header">
        <div>
          <p className="eyebrow">Page Builder</p>
          <h1>User-created pages, not coded pages</h1>
          <p>
            Build a configurable page layout, bind widgets to canonical schema sources,
            save it as backend metadata, and reload the same PageDefinition by slug.
          </p>
        </div>

        <div className="page-builder-page__actions" aria-label="Page builder actions">
          <StandardButton variant="primary" onClick={savePageDefinition}>
            Save page definition
          </StandardButton>

          <StandardButton variant="secondary" onClick={loadPageDefinition}>
            Load by slug
          </StandardButton>

          <StandardButton variant="ghost" onClick={deletePageDefinition}>
            Delete owned page
          </StandardButton>
        </div>
      </section>

      <p
        className={"page-builder-page__save-status page-builder-page__save-status--" + status.kind}
        role="status"
        aria-live="polite"
      >
        {status.message}
      </p>

      <section className="page-builder-page__grid">
        <StandardCard className="page-builder-page__panel" title="Page properties">
          <StandardInput
            label="Title"
            value={state.title}
            onChange={(value) =>
              dispatch({
                type: "updateMeta",
                patch: { title: value },
              })
            }
          />

          <StandardInput
            label="Slug"
            value={state.slug}
            onChange={(value) =>
              dispatch({
                type: "updateMeta",
                patch: { slug: normalizeSlugInput(value) },
              })
            }
          />

          <StandardSelect
            label="Visibility"
            value={state.visibility}
            options={visibilityOptions}
            onChange={(value) =>
              dispatch({
                type: "updateMeta",
                patch: { visibility: normalizePageVisibility(value) },
              })
            }
          />

          <StandardTextArea
            label="Generated PageDefinition payload"
            value={JSON.stringify(payload, null, 2)}
            readOnly
            rows={12}
          />
        </StandardCard>

        <StandardCard className="page-builder-page__panel" title="Widget library">
          <div className="page-builder-page__library">
            {library.map((item) => (
              <StandardButton
                key={item.kind}
                variant="secondary"
                onClick={() =>
                  dispatch({
                    type: "addWidget",
                    kind: item.kind,
                    title: item.title,
                    source: item.source,
                    idSeed: Date.now(),
                  })
                }
              >
                Add {item.title}
              </StandardButton>
            ))}
          </div>
        </StandardCard>
      </section>

      <StandardCard className="page-builder-page__canvas" title="Canvas">
        <div className="page-builder-page__canvas-header">
          <span>Metadata-driven canvas</span>
          <span>{state.widgets.length} widgets</span>
        </div>

        <div className="page-builder-page__widgets">
          {state.widgets.map((widget) => (
            <article
              key={widget.id}
              className="page-builder-page__widget"
              data-widget-id={widget.id}
              style={{
                gridColumn: "span " + Math.min(Math.max(widget.w, 1), 12),
              }}
            >
              <div>
                <strong>{widget.title}</strong>
                <small>
                  {widget.kind} · {widget.source}
                </small>
                <small>
                  x:{widget.x} y:{widget.y} w:{widget.w} h:{widget.h}
                </small>
              </div>

              <div className="page-builder-page__widget-actions">
                <StandardButton
                  variant="ghost"
                  onClick={() =>
                    dispatch({
                      type: "moveWidget",
                      id: widget.id,
                      x: widget.x + 1,
                      y: widget.y,
                    })
                  }
                >
                  Move right
                </StandardButton>

                <StandardButton
                  variant="ghost"
                  onClick={() =>
                    dispatch({
                      type: "resizeWidget",
                      id: widget.id,
                      w: widget.w + 1,
                      h: widget.h,
                    })
                  }
                >
                  Resize wider
                </StandardButton>

                <StandardButton
                  variant="ghost"
                  onClick={() =>
                    dispatch({
                      type: "removeWidget",
                      id: widget.id,
                    })
                  }
                >
                  Remove
                </StandardButton>
              </div>
            </article>
          ))}
        </div>
      </StandardCard>
    </main>
  );
}

function normalizeSlugInput(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9-]+/g, "-")
    .replace(/-+/g, "-")
    .replace(/^-|-$/g, "");
}

function countWidgets(page: PageDefinitionDto): number {
  const layout = readObject(page.layoutJson);
  const widgets = Array.isArray(layout.widgets) ? layout.widgets : [];
  return widgets.length;
}

function toPageBuilderState(page: PageDefinitionDto): PageBuilderState {
  const layout = readObject(page.layoutJson);
  const widgets = Array.isArray(layout.widgets)
    ? layout.widgets.map((widget, index) => toBuilderWidget(widget, index))
    : [];

  return {
    title: page.title,
    slug: page.slug,
    visibility: normalizePageVisibility(page.visibility),
    widgets: widgets.length > 0 ? widgets : createInitialPageBuilderState().widgets,
  };
}

function toBuilderWidget(value: unknown, index: number): BuilderWidget {
  const raw = readObject(value);

  return {
    id: readString(raw.id, "w-loaded-" + index),
    kind: toWidgetKind(raw.kind),
    title: readString(raw.title, "Loaded widget"),
    x: readNumber(raw.x, (index * 3) % 12),
    y: readNumber(raw.y, Math.floor(index / 3) * 3),
    w: readNumber(raw.w, 4),
    h: readNumber(raw.h, 3),
    source: readString(raw.source, "schema_view:unknown"),
  };
}

function toWidgetKind(value: unknown): WidgetKind {
  if (
    value === "kpi" ||
    value === "bar" ||
    value === "line" ||
    value === "filter-date" ||
    value === "filter-list"
  ) {
    return value;
  }

  return "kpi";
}

function readObject(value: unknown): Record<string, unknown> {
  if (value && typeof value === "object" && !Array.isArray(value)) {
    return value as Record<string, unknown>;
  }

  return {};
}

function readString(value: unknown, fallback: string): string {
  return typeof value === "string" && value.trim() ? value : fallback;
}

function readNumber(value: unknown, fallback: number): number {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

export default PageBuilderPage;
`
);

// ============================================================================
// 2) PageBuilder CSS additions.
// ============================================================================

appendIfMissing(
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/page-builder.css",
  "page-builder-page__save-status",
  String.raw`
.page-builder-page__actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 0.5rem;
}

.page-builder-page__save-status {
  margin: 0;
  padding: 0.75rem 1rem;
  border: 1px solid var(--ppiq-border-subtle, #d8dee8);
  border-radius: 12px;
  background: var(--ppiq-surface-subtle, rgba(15, 23, 42, 0.04));
  font-size: 0.92rem;
}

.page-builder-page__save-status--saved,
.page-builder-page__save-status--loaded {
  border-color: rgba(34, 197, 94, 0.45);
}

.page-builder-page__save-status--error {
  border-color: rgba(239, 68, 68, 0.55);
}

.page-builder-page__widget-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 0.4rem;
}
`
);

// ============================================================================
// 3) P03 API + UI E2E acceptance.
// ============================================================================

writeFile(
  "Frontend/PlantProcess.Web/e2e/phase23-pagebuilder-persistence.spec.ts",
  String.raw`import { expect, test } from "@playwright/test";
import { apiBaseUrl, login } from "./helpers/auth";

const slug = "e2e-pagebuilder-persistence";

const payload = {
  slug,
  title: "E2E Page Builder Persistence",
  visibility: "Shared",
  layoutJson: {
    grid: { columns: 12, rowHeight: 80 },
    widgets: [
      { id: "w-risk", kind: "kpi", title: "Risk KPI", x: 0, y: 0, w: 3, h: 2, source: "schema_view:risk_summary" },
      { id: "w-defects", kind: "bar", title: "Defect breakdown", x: 3, y: 0, w: 5, h: 3, source: "schema_view:defect_breakdown" },
      { id: "w-filter", kind: "filter-list", title: "Plant filter", x: 8, y: 0, w: 4, h: 2, source: "filter:plant" },
    ],
  },
  widgetBindingsJson: {
    bindings: [
      { widgetId: "w-risk", source: "schema_view:risk_summary" },
      { widgetId: "w-defects", source: "schema_view:defect_breakdown" },
      { widgetId: "w-filter", source: "filter:plant" },
    ],
  },
};

function headers(token: string) {
  return {
    Authorization: "Bearer " + token,
    Accept: "application/json",
    "Content-Type": "application/json",
  };
}

test.describe("P03 PageDefinition persistence acceptance", () => {
  test("API creates, reloads, updates, lists, validates and deletes a user-created page", async ({ request }) => {
    const token = await login(request);
    const authHeaders = headers(token);

    await request.delete(apiBaseUrl + "/pages/" + slug, {
      headers: authHeaders,
    }).catch(() => undefined);

    const create = await request.post(apiBaseUrl + "/pages", {
      headers: authHeaders,
      data: payload,
    });

    expect(create.ok(), "create failed with " + create.status()).toBeTruthy();

    const created = await create.json();
    expect(created.slug).toBe(slug);
    expect(created.title).toBe(payload.title);
    expect(created.visibility).toBe("Shared");
    expect(created.layoutJson.widgets).toHaveLength(3);
    expect(created.widgetBindingsJson.bindings).toHaveLength(3);

    const loadedResponse = await request.get(apiBaseUrl + "/pages/" + slug, {
      headers: authHeaders,
    });

    expect(loadedResponse.ok(), "load failed with " + loadedResponse.status()).toBeTruthy();

    const loaded = await loadedResponse.json();
    expect(loaded.slug).toBe(slug);
    expect(loaded.layoutJson.widgets.map((widget: { id: string }) => widget.id)).toEqual([
      "w-risk",
      "w-defects",
      "w-filter",
    ]);

    const updatedResponse = await request.put(apiBaseUrl + "/pages/" + slug, {
      headers: authHeaders,
      data: {
        ...payload,
        title: "E2E Page Builder Persistence Updated",
      },
    });

    expect(updatedResponse.ok(), "update failed with " + updatedResponse.status()).toBeTruthy();

    const updated = await updatedResponse.json();
    expect(updated.title).toBe("E2E Page Builder Persistence Updated");
    expect(updated.version).toBeGreaterThanOrEqual(created.version);

    const listResponse = await request.get(apiBaseUrl + "/pages", {
      headers: authHeaders,
    });

    expect(listResponse.ok(), "list failed with " + listResponse.status()).toBeTruthy();

    const pages = await listResponse.json();
    expect(pages.some((page: { slug: string }) => page.slug === slug)).toBeTruthy();

    const invalid = await request.post(apiBaseUrl + "/pages", {
      headers: authHeaders,
      data: {
        ...payload,
        slug: "Invalid Slug With Spaces",
      },
    });

    expect(invalid.status()).toBe(400);
    expect(await invalid.text()).toMatch(/slug|url-safe/i);

    const deleteResponse = await request.delete(apiBaseUrl + "/pages/" + slug, {
      headers: authHeaders,
    });

    expect(deleteResponse.ok(), "delete failed with " + deleteResponse.status()).toBeTruthy();

    const deleted = await deleteResponse.json();
    expect(deleted.deleted).toBeTruthy();
  });

  test("UI saves and reloads the metadata page definition through the backend", async ({ page, request }) => {
    const token = await login(request);

    await page.addInitScript((accessToken) => {
      window.localStorage.setItem("plantprocess.auth.accessToken", accessToken);
    }, token);

    await page.goto("/page-builder");

    await expect(
      page.getByRole("heading", { name: /User-created pages, not coded pages/i }),
    ).toBeVisible({ timeout: 15_000 });

    await page.getByLabel("Slug", { exact: true }).fill("e2e-pagebuilder-ui");
    await page.getByLabel("Title", { exact: true }).fill("E2E PageBuilder UI");

    await page.getByRole("button", { name: /Add List-of-values filter/i }).click();

    await expect(page.locator("[data-widget-id]")).toHaveCount(4);

    await page.getByRole("button", { name: /^Save page definition$/i }).click();

    await expect(page.getByRole("status")).toContainText(/Saved PageDefinition 'e2e-pagebuilder-ui'/i, {
      timeout: 15_000,
    });

    await page.getByRole("button", { name: /^Load by slug$/i }).click();

    await expect(page.getByRole("status")).toContainText(/Loaded PageDefinition 'e2e-pagebuilder-ui'/i, {
      timeout: 15_000,
    });

    await expect(page.locator("[data-widget-id]")).toHaveCount(4);
  });
});
`
);

// Keep the existing smoke route but upgrade it to include Save/Load buttons.
writeFile(
  "Frontend/PlantProcess.Web/e2e/page-builder-v7.spec.ts",
  String.raw`import { expect, test } from "@playwright/test";
import { login } from "./helpers/auth";

test.describe("P03 page builder smoke", () => {
  test("page builder route exposes persisted metadata actions", async ({ page, request }) => {
    const token = await login(request);

    await page.addInitScript((accessToken) => {
      window.localStorage.setItem("plantprocess.auth.accessToken", accessToken);
    }, token);

    await page.goto("/page-builder");

    await expect(
      page.getByRole("heading", { name: /User-created pages, not coded pages/i }),
    ).toBeVisible({ timeout: 10_000 });

    await expect(page.getByText("Page Builder", { exact: true })).toBeVisible();

    await expect(
      page.getByRole("heading", { name: /^Page properties$/i }),
    ).toBeVisible();

    await expect(
      page.getByRole("heading", { name: /^Widget library$/i }),
    ).toBeVisible();

    await expect(
      page.getByRole("heading", { name: /^Canvas$/i }),
    ).toBeVisible();

    await expect(page.getByLabel("Title", { exact: true })).toHaveValue(
      "Demo Quality Investigation",
    );

    await expect(page.getByLabel("Slug", { exact: true })).toHaveValue(
      "demo-quality-investigation",
    );

    await expect(page.getByRole("button", { name: /^Save page definition$/i })).toBeVisible();
    await expect(page.getByRole("button", { name: /^Load by slug$/i })).toBeVisible();
    await expect(page.getByRole("button", { name: /^Delete owned page$/i })).toBeVisible();
  });
});
`
);

// ============================================================================
// 4) Structural validator.
// ============================================================================

writeFile(
  "tools/phase25/validate-pack2-p03-frontend.cjs",
  String.raw`const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const failures = [];

function read(relativePath) {
  const file = path.join(root, relativePath);
  if (!fs.existsSync(file)) {
    failures.push("Missing file: " + relativePath);
    return "";
  }

  return fs.readFileSync(file, "utf8");
}

function mustContain(relativePath, regex, message) {
  const text = read(relativePath);
  if (text && !regex.test(text)) {
    failures.push(message + " in " + relativePath);
  }
}

const pageText = read("Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.tsx");

if (/\\nimport\s/.test(pageText)) {
  failures.push("PageBuilderPage contains escaped newline import corruption");
}

mustContain(
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.tsx",
  /pageBuilderApi\.create/,
  "PageBuilderPage must save through backend pageBuilderApi"
);

mustContain(
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.tsx",
  /pageBuilderApi\.getBySlug/,
  "PageBuilderPage must load through backend pageBuilderApi"
);

mustContain(
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.tsx",
  /pageBuilderApi\.delete/,
  "PageBuilderPage must delete through backend pageBuilderApi"
);

mustContain(
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.tsx",
  /type:\s*"moveWidget"/,
  "PageBuilderPage must expose move widget action"
);

mustContain(
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.tsx",
  /type:\s*"resizeWidget"/,
  "PageBuilderPage must expose resize widget action"
);

mustContain(
  "Frontend/PlantProcess.Web/e2e/phase23-pagebuilder-persistence.spec.ts",
  /creates, reloads, updates, lists, validates and deletes/,
  "P03 API persistence E2E must exist"
);

mustContain(
  "Frontend/PlantProcess.Web/e2e/phase23-pagebuilder-persistence.spec.ts",
  /UI saves and reloads/,
  "P03 UI save/load E2E must exist"
);

mustContain(
  "Frontend/PlantProcess.Web/e2e/page-builder-v7.spec.ts",
  /persisted metadata actions/,
  "Page builder smoke must cover persistence actions"
);

if (failures.length > 0) {
  console.error("Pack 2 validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("Pack 2 P03 frontend structural validation passed.");
`
);

console.log("");
console.log("Pack 2 P03 frontend persistence and E2E applied.");
