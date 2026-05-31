import { useMemo, useReducer } from "react";
import { StandardButton } from "@/components/standard/StandardButton";
import { StandardInput, StandardSelect, StandardTextArea } from "@/components/standard/StandardFields";
import { StandardCard } from "@/components/standard/StandardSurface";
import {
  createInitialPageBuilderState,
  createPageBuilderPayload,
  normalizePageVisibility,
  pageBuilderReducer,
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

export function PageBuilderPage() {
  const [state, dispatch] = useReducer(
    pageBuilderReducer,
    createInitialPageBuilderState(),
  );

  const payload = useMemo(() => createPageBuilderPayload(state), [state]);

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
                patch: { slug: value },
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
            <article key={widget.id} className="page-builder-page__widget">
              <div>
                <strong>{widget.title}</strong>
                <small>
                  {widget.kind} · {widget.source}
                </small>
              </div>

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
            </article>
          ))}
        </div>
      </StandardCard>
    </main>
  );
}

export default PageBuilderPage;
