import { useState } from "react";
import { StandardButton } from "../standard";

// P4-T04: surfaced when a page/widget save returns 409 page_version_conflict.
// Shows who changed it, and gates "overwrite" behind an explicit confirm so there
// is no silent last-write-wins. Uses StandardButton per the design-system contract.
export interface ConflictDialogProps {
  open: boolean;
  editor: string;
  currentVersion: number;
  updatedAtUtc?: string;
  onReload: () => void;
  onOverwrite: () => void;
  onCancel: () => void;
}

export function ConflictDialog(props: ConflictDialogProps) {
  const { open, editor, currentVersion, updatedAtUtc, onReload, onOverwrite, onCancel } = props;
  const [confirmOverwrite, setConfirmOverwrite] = useState(false);
  if (!open) return null;

  const when = updatedAtUtc ? new Date(updatedAtUtc).toLocaleString() : null;

  return (
    <div className="conflict-dialog-overlay" role="dialog" aria-modal="true" data-testid="conflict-dialog">
      <div className="conflict-dialog">
        <h2 className="conflict-dialog__title">This page changed since you opened it</h2>
        <p className="conflict-dialog__body" data-testid="conflict-editor">
          Changed by <strong>{editor}</strong>
          {when ? <> on {when}</> : null}. The current version is v{currentVersion}.
        </p>
        <p className="conflict-dialog__hint">
          Reload to see the latest version, or overwrite it with your changes.
        </p>

        <label className="conflict-dialog__confirm">
          <input
            type="checkbox"
            checked={confirmOverwrite}
            onChange={(e) => setConfirmOverwrite(e.target.checked)}
            data-testid="conflict-overwrite-confirm"
          />
          I understand this will replace {editor}&apos;s changes.
        </label>

        <div className="conflict-dialog__actions">
          <StandardButton variant="secondary" onClick={onCancel} data-testid="conflict-cancel">
            Cancel
          </StandardButton>
          <StandardButton variant="secondary" onClick={onReload} data-testid="conflict-reload">
            Reload latest
          </StandardButton>
          <StandardButton
            variant="danger"
            isDisabled={!confirmOverwrite}
            data-disabled-reason={!confirmOverwrite ? "Tick the confirmation to enable overwrite." : undefined}
            onClick={onOverwrite}
            data-testid="conflict-overwrite"
          >
            Overwrite anyway
          </StandardButton>
        </div>
      </div>
    </div>
  );
}