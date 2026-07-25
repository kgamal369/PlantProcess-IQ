import { RotateCcw, Trash2, Undo2 } from "lucide-react";
import { useDashboardSelections } from "../../state/DashboardSelectionContext";
import { useDashboardGridLayout } from "../../state/DashboardGridLayoutContext";
import { StandardButton } from "@/components/standard";

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
        <StandardButton
          className="secondary-button"
          onClick={undoSelection}
          isDisabled={selections.length === 0}
          type="button"
        >
          <Undo2 size={15} />
          Undo
        </StandardButton>

        <StandardButton
          className="secondary-button"
          onClick={clearSelections}
          isDisabled={selections.length === 0}
          type="button"
        >
          <Trash2 size={15} />
          Clear visual selections
        </StandardButton>

        <StandardButton className="secondary-button" onClick={showAllWidgets} type="button">
          Show widgets
        </StandardButton>

        {/* PPIQ-SCENE5678: one control. Two near-identically labelled reset
            buttons used to sit side by side doing different things, and no
            customer could tell them apart - one even had an icon and the other
            did not. Both resets now run behind a single labelled button. */}
        <StandardButton
          className="secondary-button"
          onClick={() => {
            resetLayout();
            resetGridLayout();
          }}
          type="button"
        >
          <RotateCcw size={15} />
          Reset layout
        </StandardButton>
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