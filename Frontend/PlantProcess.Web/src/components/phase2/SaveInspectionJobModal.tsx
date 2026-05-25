import { useState } from "react";
import { Save, X } from "lucide-react";
import toast from "@/notifications/toast";
import { phase2WorkflowApi } from "@/api/phase2WorkflowApi";

type Props = {
  isOpen: boolean;
  onClose: () => void;
  parameterCode?: string | null;
  defectType?: string | null;
  sourceCorrelationRunId?: string | null;
  onSaved?: () => void | Promise<void>;
};

export function SaveInspectionJobModal({
  isOpen,
  onClose,
  parameterCode,
  defectType,
  sourceCorrelationRunId,
  onSaved,
}: Props) {
  const [form, setForm] = useState({
    inspectionJobCode: `INSPECT_${parameterCode ?? "PARAM"}_${defectType ?? "DEFECT"}`.replace(
      /[^a-zA-Z0-9_]/g,
      "_"
    ),
    inspectionJobName: `Watch ${parameterCode ?? "parameter"} vs ${defectType ?? "defect"}`,
    scheduleExpression: "Daily",
    description:
      "Saved from correlation analysis. Rule-based monitoring only; not guaranteed root cause.",
  });

  const [isSaving, setIsSaving] = useState(false);

  if (!isOpen) return null;

  async function save() {
    setIsSaving(true);

    const toastId = "save-inspection-job";

    toast.loading("Saving inspection job...", { id: toastId });

    try {
      await phase2WorkflowApi.saveInspectionJobFromCorrelation({
        inspectionJobCode: form.inspectionJobCode,
        inspectionJobName: form.inspectionJobName,
        inspectionType: "RuleBasedQualityWatch",
        sourceCorrelationRunId,
        parameterCode,
        defectType,
        ruleJson: JSON.stringify({
          mode: "ruleBasedCorrelation",
          parameterCode,
          defectType,
          statement:
            "Evidence-based suspected contributor monitoring, not guaranteed root cause.",
        }),
        scheduleExpression: form.scheduleExpression,
        isEnabled: true,
        isSynthetic: false,
        description: form.description,
      });

      toast.success("Inspection job saved", {
        id: toastId,
        description: "The correlation can now be monitored as a recurring rule-based job.",
      });

      await onSaved?.();
      onClose();
    } catch (error) {
      toast.error("Could not save inspection job", {
        id: toastId,
        description: error instanceof Error ? error.message : String(error),
      });
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <div className="wizard-backdrop" role="presentation">
      <section className="widget-builder-modal" role="dialog" aria-modal="true">
        <header className="widget-builder-header">
          <div>
            <p className="eyebrow">PPIQ-WF-016</p>
            <h2>Save as Inspection Job</h2>
            <p>
              Convert this one-time correlation into recurring rule-based monitoring.
              This is not a production ML prediction and not guaranteed root cause.
            </p>
          </div>

          <button className="icon-button" type="button" onClick={onClose}>
            <X size={18} />
          </button>
        </header>

        <div className="admin-form-grid">
          <label className="admin-form-label">
            Job code
            <input
              className="admin-input"
              value={form.inspectionJobCode}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  inspectionJobCode: event.target.value,
                }))
              }
            />
          </label>

          <label className="admin-form-label">
            Job name
            <input
              className="admin-input"
              value={form.inspectionJobName}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  inspectionJobName: event.target.value,
                }))
              }
            />
          </label>

          <label className="admin-form-label">
            Schedule
            <select
              className="admin-select"
              value={form.scheduleExpression}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  scheduleExpression: event.target.value,
                }))
              }
            >
              <option value="Manual">Manual</option>
              <option value="Daily">Daily</option>
              <option value="Every 12 hours">Every 12 hours</option>
              <option value="Weekly">Weekly</option>
            </select>
          </label>

          <label className="admin-form-label" style={{ gridColumn: "1 / -1" }}>
            Description
            <textarea
              className="admin-input"
              rows={4}
              value={form.description}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  description: event.target.value,
                }))
              }
            />
          </label>
        </div>

        <footer className="widget-builder-footer">
          <button className="secondary-button" type="button" onClick={onClose}>
            Cancel
          </button>

          <button
            className="primary-button"
            type="button"
            disabled={isSaving}
            onClick={() => void save()}
          >
            <Save size={16} />
            {isSaving ? "Saving..." : "Save inspection job"}
          </button>
        </footer>
      </section>
    </div>
  );
}

export default SaveInspectionJobModal;