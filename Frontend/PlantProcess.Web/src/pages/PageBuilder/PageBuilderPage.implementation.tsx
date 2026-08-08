import { useEffect, useMemo, useReducer, useState } from "react";

import { dashboardingApi } from "@/api/dashboarding/dashboarding.api";
import { SharedAuthoringShell } from "@/authoring/SharedAuthoringShell";

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
  pageBuilderGrid,
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

// PPIQ T-041. THE SECOND COPY OF THE DEMO LIBRARY IS DELETED, NOT REPLACED.
//
// Two of its five entries were never widget kinds - a date filter and a
// list-of-values filter are variants under the Filter kind - and three bound a
// new widget to a schema_view of one reference plant before the author had
// chosen anything. The structural kinds now come from the endpoint, and there
// is deliberately NO local fallback: a product that cannot reach its own
// grammar says so, rather than offering a guess that may disagree with it.

type StructuralKind = {
  code: string;
  label: string;
  usesChartType: boolean;
  usesQuery: boolean;
  description: string;
};

// The four roles the API authorises against. The page states its audience in
// the same vocabulary the server validates, so a rejection can never be a
// surprise about spelling.
const audienceRoleOptions = ["Admin", "DataManager", "Engineer", "Viewer"] as const;

function readStructuralKinds(payload: unknown): StructuralKind[] {
  const kinds = (payload as { widgetKinds?: unknown })?.widgetKinds;

  if (!Array.isArray(kinds)) {
    return [];
  }

  return kinds
    .map((entry) => entry as Record<string, unknown>)
    .filter((entry) => typeof entry.code === "string" && typeof entry.label === "string")
    .map((entry) => ({
      code: String(entry.code),
      label: String(entry.label),
      usesChartType: entry.usesChartType === true,
      usesQuery: entry.usesQuery === true,
      description: typeof entry.description === "string" ? entry.description : "",
    }));
}

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

  // PPIQ T-041. The structural grammar is fetched, never compiled. Three states,
  // because "we could not reach the endpoint" and "the endpoint published
  // nothing" are different sentences and an author deserves the right one.
  const [kinds, setKinds] = useState<StructuralKind[]>([]);
  const [kindsStatus, setKindsStatus] = useState<"loading" | "ready" | "failed">("loading");
  const [chosenKind, setChosenKind] = useState<StructuralKind | null>(null);
  const [newWidgetName, setNewWidgetName] = useState("");
  const [authoring, setAuthoring] = useState<{ kind: string; title: string } | null>(null);

  useEffect(() => {
    let cancelled = false;

    dashboardingApi
      .getDashboardMetadata()
      .then((payload: unknown) => {
        if (cancelled) { return; }
        const published = readStructuralKinds(payload);
        setKinds(published);
        setKindsStatus(published.length > 0 ? "ready" : "failed");
      })
      .catch(() => {
        if (cancelled) { return; }
        setKinds([]);
        setKindsStatus("failed");
      });

    return () => { cancelled = true; };
  }, []);

  const canCreatePage =
    state.title.trim().length > 0
    && state.slug.trim().length > 0
    && state.audienceRoles.length > 0;

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

          <fieldset className="page-builder-page__audience" data-testid="page-audience">
            <legend>Audience roles</legend>
            <p className="page-builder-page__hint">
              Who this page is authored for. Visibility above answers a different
              question: who may open it.
            </p>

            {audienceRoleOptions.map((role) => (
              <label key={role} className="page-builder-page__audience-role">
                <input
                  type="checkbox"
                  checked={state.audienceRoles.includes(role)}
                  onChange={(event) =>
                    dispatch({
                      type: "updateMeta",
                      patch: {
                        audienceRoles: event.target.checked
                          ? [...state.audienceRoles, role]
                          : state.audienceRoles.filter((chosen) => chosen !== role),
                      },
                    })
                  }
                />
                {role}
              </label>
            ))}

            {state.audienceRoles.length === 0 ? (
              <p role="status" data-testid="page-audience-required">
                Choose at least one audience role before adding widgets.
              </p>
            ) : null}
          </fieldset>

          <StandardTextArea
            label="Generated PageDefinition payload"
            value={JSON.stringify(payload, null, 2)}
            readOnly
            rows={12}
          />
        </StandardCard>

        <StandardCard className="page-builder-page__panel" title="Add widget">
          {kindsStatus === "loading" ? (
            <p role="status" data-testid="widget-kinds-loading">
              Reading the widget grammar from the server.
            </p>
          ) : null}

          {kindsStatus === "failed" ? (
            <p role="alert" data-testid="widget-kinds-failed">
              The widget grammar could not be read from the server, so no kind can be
              offered. Check that the API is running, then reload this page.
            </p>
          ) : null}

          {kindsStatus === "ready" ? (
            <div className="page-builder-page__library" data-testid="widget-kind-picker">
              {kinds.map((kind) => (
                <StandardButton
                  key={kind.code}
                  variant={chosenKind?.code === kind.code ? "primary" : "secondary"}
                  data-testid={"widget-kind-" + kind.code}
                  disabled={!canCreatePage}
                  onClick={() => {
                    setChosenKind(kind);
                    setNewWidgetName("");
                  }}
                >
                  {kind.label}
                </StandardButton>
              ))}
            </div>
          ) : null}

          {chosenKind ? (
            <div className="page-builder-page__name-widget" data-testid="widget-name-step">
              <p>{chosenKind.description}</p>

              <StandardInput
                label="Widget name"
                value={newWidgetName}
                onChange={(value) => setNewWidgetName(value)}
              />

              <StandardButton
                variant="primary"
                data-testid="ctl-open-authoring"
                disabled={newWidgetName.trim().length === 0}
                onClick={() => {
                  const title = newWidgetName.trim();

                  // The widget is placed on the page and then authored. NOTHING
                  // is bound here: a source invented to make the shell open would
                  // be a demo binding by another name.
                  dispatch({
                    type: "addWidget",
                    kind: chosenKind.code,
                    title,
                    source: "",
                    idSeed: Date.now(),
                  });

                  setAuthoring({ kind: chosenKind.code, title });
                  setChosenKind(null);
                  setNewWidgetName("");
                }}
              >
                Author this widget
              </StandardButton>
            </div>
          ) : null}
        </StandardCard>
      </section>

      <StandardCard className="page-builder-page__canvas" title="Canvas">
        <div className="page-builder-page__canvas-header">
          <span>{pageBuilderGrid.columns}-column grid</span>
          <span>{state.widgets.length} widgets</span>
        </div>

        {state.widgets.length === 0 ? (
          <p className="page-builder-page__empty" data-testid="page-empty">
            This page has no widgets yet
          </p>
        ) : null}

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

      {authoring ? (
        <SharedAuthoringShell
          purpose="S2"
          onClose={() => setAuthoring(null)}
          onSaved={() => setAuthoring(null)}
        />
      ) : null}

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
    // Carried through the round trip. Dropping it here would let a load
    // followed by a save erase an audience somebody authored - the same defect
    // the server-side omission semantics exist to prevent.
    audienceRoles: Array.isArray(page.audienceRoles) ? [...page.audienceRoles] : [],
    // A loaded page shows what it actually holds. The fallback that stood here
    // put three demo widgets on an empty page and called it a load.
    widgets,
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
