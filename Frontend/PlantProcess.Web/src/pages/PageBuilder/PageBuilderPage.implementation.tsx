import { useMemo, useReducer, useState } from "react";

import { pageBuilderApi, type PageDefinitionDto } from "@/api/pageBuilder";
import { ApiError } from "@/api/http/apiClient";
import { ConflictDialog } from "@/components/conflict/ConflictDialog";
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

import { P2T08_STANDARD_ROLLOUT_MARKER } from "@/components/standard/StandardP2Controls";
import { StandardButton } from "@/components/standard";
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
  const [loadedPage, setLoadedPage] = useState<PageDefinitionDto | null>(null);
  const [conflict, setConflict] = useState<{
    editor: string;
    currentVersion: number;
    updatedAtUtc?: string;
  } | null>(null);

  const payload = useMemo(() => createPageBuilderPayload(state), [state]);

  async function persistPageDefinition(overwrite = false) {
    setStatus({ kind: "saving", message: "Saving PageDefinition..." });
    const request = {
      ...payload,
      expectedVersion: overwrite ? null : loadedPage?.version ?? null,
    };
    const saved = loadedPage
      ? await pageBuilderApi.update(state.slug, request)
      : await pageBuilderApi.create(request);
    setLoadedPage(saved);
    setConflict(null);
    setStatus({
      kind: "saved",
      message: "Saved PageDefinition '" + saved.slug + "' v" + saved.version,
    });
  }

  async function savePageDefinition() {
    try {
      await persistPageDefinition(false);
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        try {
          const body = JSON.parse(error.responseText) as {
            code?: string;
            editor?: string;
            currentVersion?: number;
            updatedAtUtc?: string;
          };
          if (body.code === "page_version_conflict" && typeof body.currentVersion === "number") {
            setConflict({
              editor: body.editor || "another editor",
              currentVersion: body.currentVersion,
              updatedAtUtc: body.updatedAtUtc,
            });
            setStatus({ kind: "error", message: "Save blocked: this page changed in another session." });
            return;
          }
        } catch {
          // Fall through to the normal error presentation.
        }
      }
      setStatus({ kind: "error", message: error instanceof Error ? error.message : "Save failed" });
    }
  }

  async function loadPageDefinition() {
    try {
      setStatus({ kind: "loading", message: "Loading PageDefinition..." });

      const loaded = await pageBuilderApi.getBySlug(state.slug);
      setLoadedPage(loaded);
      setConflict(null);

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


  async function reloadAfterConflict() {
    setConflict(null);
    await loadPageDefinition();
  }

  async function overwriteAfterConflict() {
    try {
      await persistPageDefinition(true);
    } catch (error) {
      setStatus({ kind: "error", message: error instanceof Error ? error.message : "Overwrite failed" });
    }
  }
  return (
    <main className="page-builder-page" data-inspection3-page="page-builder">
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
          <StandardButton variant="primary" onClick={savePageDefinition} data-testid="ctl-save-page">
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
            >
              <div>
                <strong>{widget.title}</strong>
                <small>
                  {widget.kind} Â· {widget.source}
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

      <ConflictDialog
        open={conflict !== null}
        editor={conflict?.editor ?? "another editor"}
        currentVersion={conflict?.currentVersion ?? loadedPage?.version ?? 0}
        updatedAtUtc={conflict?.updatedAtUtc}
        onReload={reloadAfterConflict}
        onOverwrite={overwriteAfterConflict}
        onCancel={() => setConflict(null)}
      />    </main>
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
