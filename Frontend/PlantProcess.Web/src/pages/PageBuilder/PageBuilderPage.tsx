import { useMemo, useState } from "react";
import { StandardButton } from "@/components/standard/StandardButton";
import { StandardInput, StandardSelect, StandardTextArea } from "@/components/standard/StandardFields";
import { StandardCard } from "@/components/standard/StandardSurface";
import "./page-builder.css";

type PageVisibility = "Private" | "Shared" | "Public";
type WidgetKind = "kpi" | "bar" | "line" | "filter-date" | "filter-list";

type BuilderWidget = {
  id: string;
  kind: WidgetKind;
  title: string;
  x: number;
  y: number;
  w: number;
  h: number;
  source: string;
};

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

function normalizeVisibility(value: string | string[]): PageVisibility {
  const raw = Array.isArray(value) ? value[0] : value;

  if (raw === "Private" || raw === "Shared" || raw === "Public") {
    return raw;
  }

  return "Shared";
}

export function PageBuilderPage() {
  const [title, setTitle] = useState("Demo Quality Investigation");
  const [slug, setSlug] = useState("demo-quality-investigation");
  const [visibility, setVisibility] = useState<PageVisibility>("Shared");

  const [widgets, setWidgets] = useState<BuilderWidget[]>([
    {
      id: "w-risk",
      kind: "kpi",
      title: "Risk KPI",
      x: 0,
      y: 0,
      w: 3,
      h: 2,
      source: "schema_view:risk_summary",
    },
    {
      id: "w-defects",
      kind: "bar",
      title: "Defect breakdown",
      x: 3,
      y: 0,
      w: 5,
      h: 3,
      source: "schema_view:defect_breakdown",
    },
    {
      id: "w-trend",
      kind: "line",
      title: "Defect trend",
      x: 8,
      y: 0,
      w: 4,
      h: 3,
      source: "schema_view:quality_daily",
    },
  ]);

  const payload = useMemo(
    () => ({
      slug,
      title,
      visibility,
      layoutJson: {
        grid: { columns: 12, rowHeight: 80 },
        widgets,
      },
      widgetBindingsJson: {
        bindings: widgets.map((widget) => ({
          widgetId: widget.id,
          source: widget.source,
        })),
      },
    }),
    [slug, title, visibility, widgets],
  );

  function addWidget(kind: WidgetKind, widgetTitle: string, source: string) {
    setWidgets((current) => [
      ...current,
      {
        id: "w-" + Date.now(),
        kind,
        title: widgetTitle,
        x: (current.length * 3) % 12,
        y: Math.floor(current.length / 3) * 3,
        w: kind.startsWith("filter") ? 3 : 4,
        h: kind === "kpi" ? 2 : 3,
        source,
      },
    ]);
  }

  function removeWidget(id: string) {
    setWidgets((current) => current.filter((widget) => widget.id !== id));
  }

  return (
    <main className="page-builder-page">
      <section className="page-builder-page__header">
        <div>
          <p className="eyebrow">Page Builder</p>
          <h1>User-created pages, not coded pages</h1>
          <p>
            Build a configurable page layout, bind widgets to canonical schema sources,
            and save the definition as JSON-backed metadata.
          </p>
        </div>

        <StandardButton variant="primary">Save page definition</StandardButton>
      </section>

      <section className="page-builder-page__grid">
        <StandardCard className="page-builder-page__panel" title="Page properties">
          <StandardInput label="Title" value={title} onChange={setTitle} />
          <StandardInput label="Slug" value={slug} onChange={setSlug} />

          <StandardSelect
            label="Visibility"
            value={visibility}
            options={visibilityOptions}
            onChange={(value) => setVisibility(normalizeVisibility(value))}
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
                onClick={() => addWidget(item.kind, item.title, item.source)}
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
          <span>{widgets.length} widgets</span>
        </div>

        <div className="page-builder-page__widgets">
          {widgets.map((widget) => (
            <article key={widget.id} className="page-builder-page__widget">
              <div>
                <strong>{widget.title}</strong>
                <small>
                  {widget.kind} · {widget.source}
                </small>
              </div>

              <StandardButton variant="ghost" onClick={() => removeWidget(widget.id)}>
                Remove
              </StandardButton>
            </article>
          ))}
        </div>
      </StandardCard>
    </main>
  );
}

export default PageBuilderPage;
