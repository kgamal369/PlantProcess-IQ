import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";

import { pageBuilderApi, type PageDefinitionDto } from "@/api/pageBuilder";

import "./dynamic-page.css";

type RuntimeWidget = {
  id: string;
  kind: string;
  title: string;
  source: string;
  x: number;
  y: number;
  w: number;
  h: number;
};

type RuntimeLayout = {
  grid?: {
    columns?: number;
    rowHeight?: number;
  };
  widgets?: unknown[];
};

export function DynamicPage() {
  const { slug = "" } = useParams();

  const [page, setPage] = useState<PageDefinitionDto | null>(null);
  const [status, setStatus] = useState<"loading" | "loaded" | "not-found" | "error">("loading");
  const [message, setMessage] = useState("Loading dynamic page definition...");

  useEffect(() => {
    let cancelled = false;

    async function load() {
      if (!slug.trim()) {
        setStatus("not-found");
        setMessage("Missing page slug.");
        return;
      }

      try {
        setStatus("loading");
        setMessage("Loading dynamic page definition...");

        const loaded = await pageBuilderApi.getBySlug(slug);

        if (!cancelled) {
          setPage(loaded);
          setStatus("loaded");
          setMessage("Loaded backend PageDefinition '" + loaded.slug + "'.");
        }
      } catch (error) {
        if (!cancelled) {
          const text = error instanceof Error ? error.message : "This page is currently unavailable.";
          setPage(null);
          setStatus(text.includes("404") ? "not-found" : "error");
          setMessage(text);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [slug]);

  const widgets = useMemo(() => readWidgets(page?.layoutJson), [page]);

  if (status === "loading") {
    return (
      <main className="dynamic-page-shell" data-dynamic-page-status="loading">
        <p className="dynamic-page-eyebrow">Dynamic Page</p>
        <h1>Loading page...</h1>
        <p>{message}</p>
      </main>
    );
  }

  if (!page || status === "not-found") {
    return (
      <main className="dynamic-page-shell" data-dynamic-page-status="not-found">
        <p className="dynamic-page-eyebrow">Dynamic Page</p>
        <h1>Page not found</h1>
        <p>
          No backend PageDefinition exists for slug <strong>{slug}</strong>.
        </p>
        <Link className="dynamic-page-link" to="/page-builder">
          Create it in Page Builder
        </Link>
      </main>
    );
  }

  return (
    <main className="dynamic-page-shell" data-dynamic-page-status="loaded" data-page-slug={page.slug}>
      <section className="dynamic-page-header">
        <div>
          <p className="dynamic-page-eyebrow">Metadata-rendered page</p>
          <h1>{page.title}</h1>
          <p>
            This page is rendered from backend <code>page_definitions.layout_json</code> and
            <code> widget_bindings_json</code>. It is not a coded dashboard route.
          </p>
        </div>

        <div className="dynamic-page-meta-card">
          <span>Slug</span>
          <strong>{page.slug}</strong>
          <small>
            {page.visibility} &middot; v{page.version}
          </small>
        </div>
      </section>

      <section className="dynamic-page-status">
        <strong>{message}</strong>
        <span>{widgets.length} widgets rendered from stored metadata</span>
      </section>

      <section className="dynamic-page-canvas" aria-label="Dynamic page canvas">
        {widgets.map((widget) => (
          <article
            key={widget.id}
            className="dynamic-page-widget"
            data-runtime-widget-id={widget.id}
            data-runtime-widget-kind={widget.kind}
            style={{ gridColumn: "span " + Math.min(Math.max(widget.w, 1), 12) }}
          >
            <header>
              <strong>{widget.title}</strong>
              <span>{widget.kind}</span>
            </header>

            <p>
              Bound source: <code>{widget.source}</code>
            </p>

            <footer>
              x:{widget.x} y:{widget.y} w:{widget.w} h:{widget.h}
            </footer>
          </article>
        ))}
      </section>
    </main>
  );
}

function readWidgets(layoutJson: unknown): RuntimeWidget[] {
  const layout = readObject(layoutJson) as RuntimeLayout;
  const rawWidgets = Array.isArray(layout.widgets) ? layout.widgets : [];

  return rawWidgets.map((value, index) => {
    const raw = readObject(value);

    return {
      id: readString(raw.id, "widget-" + index),
      kind: readString(raw.kind, "unknown"),
      title: readString(raw.title, "Untitled widget"),
      source: readString(raw.source, "unbound"),
      x: readNumber(raw.x, 0),
      y: readNumber(raw.y, index),
      w: readNumber(raw.w, 4),
      h: readNumber(raw.h, 2),
    };
  });
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

export default DynamicPage;