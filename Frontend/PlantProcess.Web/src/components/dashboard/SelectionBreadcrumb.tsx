import { RotateCcw, Trash2, Undo2 } from "lucide-react";
import { useDashboardSelections } from "../../state/DashboardSelectionContext";
import { useDashboardGridLayout } from "../../state/DashboardGridLayoutContext";

export function SelectionBreadcrumb() {
  const { selections, undoSelection, clearSelections, showAllWidgets, resetLayout } =
    useDashboardSelections();
    const { resetGridLayout } = useDashboardGridLayout();
  
    return (
    <section className="selection-breadcrumb">
      <div>
        <strong>Visual selections</strong>
        <span>
          {selections.length === 0
            ? "Click any chart, card, or table row to filter the workspace."
            : `${selections.length} active visual selection(s).`}
        </span>
      </div>

      <div className="selection-breadcrumb__actions">
        <button
          className="secondary-button"
          onClick={undoSelection}
          disabled={selections.length === 0}
          type="button"
        >
          <Undo2 size={15} />
          Undo
        </button>

        <button
          className="secondary-button"
          onClick={clearSelections}
          disabled={selections.length === 0}
          type="button"
        >
          <Trash2 size={15} />
          Clear visual selections
        </button>

        <button className="secondary-button" onClick={showAllWidgets} type="button">
          Show widgets
        </button>

        <button className="secondary-button" onClick={resetLayout} type="button">
          <RotateCcw size={15} />
          Reset layout
        </button>

        <button className="secondary-button" onClick={resetGridLayout} type="button">
            Reset grid
        </button>
      </div>

      {selections.length > 0 ? (
        <div className="visual-selection-row">
          {selections.map((selection) => (
            <span key={selection.id} className="visual-selection-chip">
              <strong>{selection.sourceWidget}:</strong> {selection.label}
            </span>
          ))}
        </div>
      ) : null}
    </section>
  );
}