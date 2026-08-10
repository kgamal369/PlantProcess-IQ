// ============================================================
// T-043. THE PERMANENT SELECTIONS BAR.
//
// Chapter 4 section 5.1.2: "The selections bar is never hidden. It reads
// 'No selections applied' when empty."
// Chapter 4 section 5.1.13: "Chip x in the selections bar removes that one
// selection."
//
// Slice 1 established the contract: the bar is always present, carries the
// exact empty sentence of 5.1.2, and gives every selection its own remove
// control. Undo reaches the LAST selection only, so with three chips applied
// the first two could not be removed at all.
//
// Slice 2 removed the layout reset control from this bar. 5.1.2 puts save and
// reset layout in the PAGE HEADER, and a layout control inside the selections
// bar contradicts the anatomy. It now lives in WorkspaceHeader and still runs
// both resets behind one labelled control, which is the PPIQ-SCENE5678 rule:
// two near-identically labelled buttons could not be told apart.
//
// RECORDED, NOT MOVED: "Show widgets" restores hidden widgets and is also not
// a selection control. It has no home in the 5.1.2 anatomy yet, and inventing
// one inside this task would be scope this task was explicitly denied.
// ============================================================
import { Trash2, Undo2, X } from "lucide-react";
import { useDashboardSelections } from "../../state/DashboardSelectionContext";
import { StandardButton } from "@/components/standard";

export const SELECTIONS_BAR_EMPTY_TEXT = "No selections applied";

export function SelectionBreadcrumb() {
  const { selections, undoSelection, clearSelections, removeSelection, showAllWidgets } =
    useDashboardSelections();

  const hasSelections = selections.length > 0;

  return (
    <section
      className="selection-breadcrumb"
      aria-label="Selections"
      data-testid="selections-bar"
    >
      <div>
        <strong>Selections</strong>
        <span data-testid="selections-bar-state">
          {hasSelections
            ? selections.length + " applied"
            : SELECTIONS_BAR_EMPTY_TEXT}
        </span>
      </div>

      <div className="selection-breadcrumb__actions">
        <StandardButton
          className="secondary-button"
          onClick={undoSelection}
          isDisabled={!hasSelections}
          type="button"
        >
          <Undo2 size={15} />
          Undo
        </StandardButton>

        <StandardButton
          className="secondary-button"
          onClick={clearSelections}
          isDisabled={!hasSelections}
          type="button"
        >
          <Trash2 size={15} />
          Clear all
        </StandardButton>

        <StandardButton className="secondary-button" onClick={showAllWidgets} type="button">
          Show widgets
        </StandardButton>
      </div>

      {hasSelections ? (
        <div className="visual-selection-row" data-testid="selection-chips">
          {selections.map((selection) => (
            <span
              key={selection.id}
              className="visual-selection-chip"
              data-testid="selection-chip"
            >
              <strong>{selection.sourceWidget}:</strong> {selection.label}
              <StandardButton
                className="visual-selection-chip__remove"
                variant="ghost"
                iconOnly
                type="button"
                ariaLabel={
                  "Remove selection " + selection.sourceWidget + ": " + selection.label
                }
                onClick={() => removeSelection(selection.id)}
              >
                <X size={13} />
              </StandardButton>
            </span>
          ))}
        </div>
      ) : null}
    </section>
  );
}