import { useEffect, useMemo, useReducer, useState } from "react";

import { dashboardingApi } from "@/api/dashboarding/dashboarding.api";
import { SharedAuthoringShell } from "@/authoring/SharedAuthoringShell";
import { notifyWorkspaceLinksChanged } from "@/state/workspaceLinksSignal";

import { pageBuilderApi, type PageDefinitionDto } from "@/api/pageBuilder";
import { ApiError } from "@/api/http/apiClient";
import { useAuth } from "@/state/AuthContext";
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
  const { user } = useAuth();
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
  const [authoring, setAuthoring] = useState<{ kind: string; title: string; dashboardId: string } | null>(null);

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

  // PPIQ T-042 S2. Bounded, and single-flight. While the bridge is being
  // ensured nothing may be activated twice: two clicks would be two attempts to
  // create the same backing workspace.
  const [bridge, setBridge] = useState<"idle" | "preparing" | "failed">("idle");
  const [bridgeMessage, setBridgeMessage] = useState("");

  // The recovery key, derived from the page's IMMUTABLE id - never its title and
  // never its slug, both of which an author may change afterwards.
  function backingCodeFor(pageId: string): string {
    return "PAGE_" + pageId.replace(/-/g, "").slice(0, 12).toUpperCase();
  }

  function asList(body: unknown): Array<Record<string, unknown>> {
    const container = body as Record<string, unknown> | unknown[] | null;
    const list = Array.isArray(container)
      ? container
      : ((container?.["items"] ?? container?.["definitions"] ?? container?.["dashboards"] ?? []) as unknown[]);

    return list as Array<Record<string, unknown>>;
  }

  async function findDashboardIdByCode(code: string): Promise<string | null> {
    const match = asList(await dashboardingApi.getDashboardDefinitions())
      .find((entry) => String(entry.dashboardCode ?? entry.code ?? "") === code);
    const id = match?.id ?? match?.dashboardDefinitionId;

    return typeof id === "string" ? id : null;
  }

  // PPIQ T-042 S2. IDEMPOTENT ACROSS PARTIAL FAILURE.
  //
  // The sequence that must never produce two workspaces: the dashboard is
  // created, the page patch that stores its id fails, and a retry sees a null
  // link. So a retry LOOKS FOR the deterministic code before it creates
  // anything, and the stored id is confirmed by re-reading the page before the
  // shell is opened. Once the link exists the ID is the authority; the code is
  // only ever a recovery key.
  async function ensureBackingDashboard(): Promise<string> {
    const request = {
      slug: state.slug.trim(),
      title: state.title.trim(),
      visibility: state.visibility,
      audienceRoles: [...state.audienceRoles],
      layoutJson: payload.layoutJson,
      widgetBindingsJson: payload.widgetBindingsJson,
    };

    const saved = loadedPage
      ? await pageBuilderApi.update(request.slug, request)
      : await pageBuilderApi.create(request);
    setLoadedPage(saved);

    if (saved.backingDashboardDefinitionId) {
      return saved.backingDashboardDefinitionId;
    }

    const code = backingCodeFor(saved.id);
    let dashboardId = await findDashboardIdByCode(code);

    if (!dashboardId) {
      const created = (await dashboardingApi.createDashboardDefinition({
        dashboardCode: code,
        name: saved.title,
        description: "Backing workspace for the authored page " + saved.slug + ".",
        isDefault: false,
        isSystemTemplate: false,
        isSynthetic: false,
      })) as { id?: unknown } | null;

      dashboardId = typeof created?.id === "string" ? created.id : await findDashboardIdByCode(code);
    }

    if (!dashboardId) {
      throw new Error("The backing workspace could not be created or found.");
    }

    await pageBuilderApi.update(saved.slug, { ...request, backingDashboardDefinitionId: dashboardId });

    // Re-read and CONFIRM. A patch that reported success but stored nothing must
    // not reach the shell wearing a real-looking id.
    const confirmed = await pageBuilderApi.getBySlug(saved.slug);
    setLoadedPage(confirmed);

    if (confirmed.backingDashboardDefinitionId !== dashboardId) {
      throw new Error("The page did not keep its link to the backing workspace.");
    }

    return dashboardId;
  }

  // PPIQ T-042 S2. THE SERVER'S WIDGET LIST IS THE AUTHORITY.
  //
  // NO persisted DashboardWidgetDefinition, NO grid widget. A widget the server
  // did not keep cannot appear on the page, however successfully the shell
  // closed. Geometry already authored for a known widget is preserved; a
  // genuinely new one takes the next deterministic free slot; the page is never
  // repacked merely because the list was refreshed.
  async function reconcileFromServer(dashboardId: string) {
    const record = (await dashboardingApi.getDashboardDefinition(dashboardId)) as Record<string, unknown>;
    const persisted = asList(
      record?.["widgets"] ?? record?.["widgetDefinitions"] ?? record?.["dashboardWidgetDefinitions"] ?? [],
    );

    const known = new Map(state.widgets.map((widget) => [widget.id, widget]));
    let placed = state.widgets.length;

    const rebuilt = persisted
      .map((entry) => {
        const id = String(entry.id ?? entry.widgetDefinitionId ?? "");

        if (id.length === 0) {
          return null;
        }

        const existing = known.get(id);

        if (existing) {
          return existing;
        }

        const index = placed;
        placed += 1;

        return {
          id,
          kind: String(entry.widgetKind ?? entry.kind ?? "chart"),
          title: String(entry.widgetTitle ?? entry.title ?? entry.name ?? id),
          x: (index * 4) % pageBuilderGrid.columns,
          y: Math.floor(index / 3) * 3,
          w: 4,
          h: 3,
          source: String(entry.widgetCode ?? entry.code ?? ""),
        } as BuilderWidget;
      })
      .filter((widget): widget is BuilderWidget => widget !== null);

    dispatch({ type: "reset", state: { ...state, widgets: rebuilt } });
  }

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

      // PPIQ T-042 S4. A page whose layout will not parse is NOT loaded. The
      // author keeps what is on screen and is told which widget failed; the
      // alternative is showing them a rearranged page and calling it theirs.
      try {
        toPageBuilderState(loaded);
      } catch (parseError) {
        setStatus({
          kind: "error",
          message:
            parseError instanceof LayoutParseError
              ? parseError.message
              : "The saved layout could not be read, so the page was not loaded.",
        });

        return;
      }
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

  // PPIQ T-042 S6. PUBLICATION IS ITS OWN ACTION, AND ITS OWN SENTENCE.
  //
  // The signal fires only AFTER the server has confirmed, never on intent: a
  // navigation entry for a publication the server refused would be a lie the
  // author could click on.
  //
  // And when a page is published to an audience the author's own role is not
  // in, it is said out loud rather than papered over with a nav entry created
  // just for them.
  async function setPublication(publish: boolean) {
    try {
      setStatus({ kind: "saving", message: publish ? "Publishing..." : "Unpublishing..." });

      const result = publish
        ? await pageBuilderApi.publish(state.slug)
        : await pageBuilderApi.unpublish(state.slug);

      setLoadedPage(result);
      notifyWorkspaceLinksChanged();

      const visibleToAuthor = state.audienceRoles.includes(user?.role ?? "");

      setStatus({
        kind: "saved",
        message: publish
          ? visibleToAuthor
            ? "Published. It is now in Workspaces for " + state.audienceRoles.join(", ") + "."
            : "Published. This page is not visible to your current role."
          : "Unpublished. It is a draft again and has left Workspaces.",
      });
    } catch (error) {
      setStatus({
        kind: "error",
        message: error instanceof ApiError ? error.message : "The publication state was not changed.",
      });
    }
  }

  async function deletePageDefinition() {
    try {
      setStatus({ kind: "saving", message: "Deleting PageDefinition..." });

      const deleted = await pageBuilderApi.delete(state.slug);

      if (deleted.deleted) {
        // A deleted page must leave navigation in the same session, not at the
        // next reload.
        notifyWorkspaceLinksChanged();
      }

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

          <StandardButton
            variant="secondary"
            data-testid="ctl-publish-page"
            disabled={!loadedPage?.backingDashboardDefinitionId}
            onClick={() => setPublication(true)}
          >
            Publish
          </StandardButton>

          <StandardButton
            variant="ghost"
            data-testid="ctl-unpublish-page"
            disabled={!loadedPage?.publishedAtUtc}
            onClick={() => setPublication(false)}
          >
            Unpublish
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

          <fieldset
            className="page-builder-page__audience"
            data-testid="page-audience"
          >
            <legend>Audience roles</legend>

            <p className="page-builder-page__hint">
              Who this page is authored for. Visibility above answers a different question:
              who may open it.
            </p>

            <StandardSelect
              label="Roles"
              multiple
              value={state.audienceRoles}
              options={audienceRoleOptions}
              onChange={(value) =>
                dispatch({
                  type: "updateMeta",
                  patch: {
                    audienceRoles: Array.isArray(value) ? value : [value],
                  },
                })
              }
            />

            {state.audienceRoles.length === 0 ? (
              <p data-testid="page-audience-required" role="status">
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
                  disabled={!canCreatePage || bridge === "preparing"}
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

              {bridge === "preparing" ? (
                <p role="status" data-testid="bridge-preparing">{bridgeMessage}</p>
              ) : null}

              {bridge === "failed" ? (
                <p role="alert" data-testid="bridge-failed">{bridgeMessage}</p>
              ) : null}

              <StandardButton
                variant="primary"
                data-testid="ctl-open-authoring"
                disabled={newWidgetName.trim().length === 0 || bridge === "preparing"}
                onClick={async () => {
                  if (bridge === "preparing") {
                    return;
                  }

                  const title = newWidgetName.trim();
                  const kind = chosenKind.code;

                  setBridge("preparing");
                  setBridgeMessage("Preparing the page and its backing workspace.");

                  try {
                    // NO widget is placed here. The grid only ever shows what the
                    // server persisted, and nothing is persisted until the author
                    // saves inside the shell.
                    const dashboardId = await ensureBackingDashboard();

                    setBridge("idle");
                    setBridgeMessage("");
                    setAuthoring({ kind, title, dashboardId });
                    setChosenKind(null);
                    setNewWidgetName("");
                  } catch (error) {
                    setBridge("failed");
                    setBridgeMessage(
                      error instanceof ApiError
                        ? "The page could not be prepared: " + error.message
                        : "The page could not be prepared, so the authoring surface was not opened.",
                    );
                  }
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
          dashboardDefinitionId={authoring.dashboardId}
          onClose={() => setAuthoring(null)}
          onSaved={async () => {
            const dashboardId = authoring.dashboardId;
            setAuthoring(null);
            await reconcileFromServer(dashboardId);
          }}
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

/// PPIQ T-042 S4. A LAYOUT THAT CANNOT BE READ IS A FAILED STATE.
///
/// The reader below used to invent whatever it could not parse: a position from
/// the widget's index, a source of schema_view:unknown, a kind coerced into a
/// five-value union that no longer exists. Each of those SILENTLY MOVES OR
/// RENAMES a page the customer arranged, and it looks like a successful load.
/// Refusing is the honest answer, and it names the widget that could not be
/// read so the author knows which one.
class LayoutParseError extends Error {}

function toBuilderWidget(value: unknown, index: number): BuilderWidget {
  const raw = readObject(value);
  const where = "widget " + (index + 1);

  return {
    id: requireString(raw.id, where, "id"),
    kind: requireString(raw.kind, where, "kind"),
    title: readString(raw.title, requireString(raw.id, where, "id")),
    x: requireNumber(raw.x, where, "x"),
    y: requireNumber(raw.y, where, "y"),
    w: requireNumber(raw.w, where, "w"),
    h: requireNumber(raw.h, where, "h"),
    // A binding the author never made is an empty string, not a fabricated
    // schema view. Empty is a fact; schema_view:unknown is a claim.
    source: typeof raw.source === "string" ? raw.source : "",
  };
}

function requireString(value: unknown, where: string, field: string): string {
  if (typeof value === "string" && value.trim().length > 0) {
    return value;
  }

  throw new LayoutParseError(
    "The saved layout could not be read: " + where + " has no usable " + field
    + ". The page was left exactly as it was saved rather than rearranged.",
  );
}

function requireNumber(value: unknown, where: string, field: string): number {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }

  throw new LayoutParseError(
    "The saved layout could not be read: " + where + " has no usable " + field
    + ". The page was left exactly as it was saved rather than rearranged.",
  );
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


export default PageBuilderPage;
