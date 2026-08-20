// ============================================================
// T-043 slice 2. THE PAGE HEADER of Chapter 4 section 5.1.2:
// sheet selector, as-of, edit toggle, save and reset layout.
//
// The header is its own component with plain props and no hooks, so it can be
// proved without mounting the workspace, its API clients and a grid that needs
// a ResizeObserver the test environment does not carry.
//
// Save layout and Reset layout have MOVED here from the selections bar. 5.1.2
// puts them in the header, and a layout control inside the selections bar
// contradicts the anatomy.
//
// RECORDED, NOT BUILT HERE, because slice 2 was scoped to these five controls:
//   - Chapter 3's D1 control table enables save and reset "when layout dirty".
//     No dirty signal exists: DashboardGridLayoutContext exposes none and
//     useDashboardLayoutPersistence exposes only isSavingLayout and
//     lastSavedAtUtc. The controls are therefore always enabled rather than
//     gated on a flag invented for the occasion.
//   - 5.1.7 also reveals the ADD WIDGET control with edit mode. Slice 2 gates
//     drag and resize only, as ruled.
//   - persistence.layoutError is still surfaced nowhere, so a failed save is
//     silent. That belongs with the widget states task.
// ============================================================
import { Plus, RotateCcw } from "lucide-react";
import { StandardButton, StandardSelect } from "@/components/standard";
import type { WorkspaceSheet } from "./workspaceSheets";

/**
 * The as-of instant, rendered in UTC to the minute.
 *
 * Deliberately not locale-formatted. This value states WHEN THE DATA ON THIS
 * PAGE WAS READ, and a reader in another timezone silently seeing their own
 * clock against a plant's UTC record is exactly the class of confusion the
 * evidence claim cannot afford.
 */
export function formatAsOf(asOfUtc: string | null): string {
  if (!asOfUtc) return "not read yet";
  return asOfUtc.slice(0, 16).replace("T", " ") + " UTC";
}

export interface WorkspaceHeaderProps {
  title: string;
  description: string;
  sheets: WorkspaceSheet[];
  activeSheetId: string;
  onSheetChange: (sheetId: string) => void;
  asOfUtc: string | null;
  isEditing: boolean;
  onToggleEdit: () => void;
  onSaveLayout: () => void;
  isSavingLayout: boolean;
  onResetLayout: () => void;
  onRefresh: () => void;
  onAddWidget: () => void;
  onCreateSheet: () => void;
}

export function WorkspaceHeader({
  title,
  description,
  sheets,
  activeSheetId,
  onSheetChange,
  asOfUtc,
  isEditing,
  onToggleEdit,
  onSaveLayout,
  isSavingLayout,
  onResetLayout,
  onRefresh,
  onAddWidget,
  onCreateSheet,
}: WorkspaceHeaderProps) {
  // Chapter 3 D1 control table: the sheet selector is enabled when there is
  // more than one sheet. One sheet is a true state, not a broken control.
  const hasOneSheet = sheets.length < 2;

  return (
    <header className="ppiq-std-card__header" data-testid="workspace-header">
      <div>
        <h2>{title}</h2>
        <p>{description}</p>
      </div>

      <div className="workspace-header__controls">
        <div className="workspace-header__sheets" data-testid="workspace-sheet-selector">
          <StandardSelect
            label="Sheet"
            size="sm"
            value={activeSheetId}
            disabled={hasOneSheet}
            options={sheets.map((sheet) => ({ value: sheet.id, label: sheet.name }))}
            onChange={(value) => onSheetChange(Array.isArray(value) ? value[0] : value)}
          />
        </div>

        {/* T-043 S3. Creating a sheet is authoring, so it appears with the
            other authoring affordances rather than beside a navigator a
            reader is only using to look. */}
        {isEditing ? (
          <StandardButton
            variant="ghost"
            onClick={onCreateSheet}
            data-testid="workspace-new-sheet"
          >
            <Plus size={15} />
            New sheet
          </StandardButton>
        ) : null}

        <p className="workspace-header__asof" data-testid="workspace-as-of">
          Data as of {formatAsOf(asOfUtc)}
        </p>

        <div className="ppiq-journey-actions">
          {/* DEMO-008. PRESENTATION MODE IS THE DEFAULT.
              Save layout and Reset layout are authoring controls, and a reader
              who is being shown the product should not be one mis-click away
              from rewriting the page they are looking at. They appear with the
              other authoring affordances once Edit layout is pressed, which is
              the same rule 5.1.7 already applies to drag handles, resize
              corners and add-widget. Nothing is removed: every control is one
              click away and behaves exactly as before.
              Refresh stays, because reading again is not authoring. */}
          <StandardButton
            variant={isEditing ? "primary" : "ghost"}
            aria-pressed={isEditing}
            data-testid="workspace-edit-toggle"
            onClick={onToggleEdit}
          >
            {isEditing ? "Done editing" : "Edit layout"}
          </StandardButton>

          {isEditing ? (
            <StandardButton
              variant="ghost"
              onClick={onSaveLayout}
              isDisabled={isSavingLayout}
              data-testid="workspace-save-layout"
            >
              {isSavingLayout ? "Saving layout..." : "Save layout"}
            </StandardButton>
          ) : null}

          {isEditing ? (
            <StandardButton
              variant="ghost"
              onClick={onResetLayout}
              data-testid="workspace-reset-layout"
            >
              <RotateCcw size={15} />
              Reset layout
            </StandardButton>
          ) : null}

          <StandardButton variant="ghost" onClick={onRefresh}>
            Refresh widgets
          </StandardButton>

          {/* T-043 S2c. Chapter 4 5.1.7: "Toggling edit mode reveals drag
              handles, resize corners and the add-widget control." Absent
              rather than disabled, because a control that is present and
              refuses makes a different statement from a control this mode
              does not offer. */}
          {isEditing ? (
            <StandardButton
              variant="primary"
              data-testid="workspace-add-widget"
              onClick={onAddWidget}
            >
              Add widget
            </StandardButton>
          ) : null}
        </div>
      </div>
    </header>
  );
}