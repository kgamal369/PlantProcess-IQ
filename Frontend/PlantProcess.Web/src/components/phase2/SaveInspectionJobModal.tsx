import { useState } from "react";
import { Save } from "lucide-react";
import { StandardButton, StandardInput, StandardModal, StandardSelect, StandardTextArea } from "@/components/standard";
import { phase78Api, type SavedInvestigationRequest, type SavedInvestigationResponse } from "@/api/phase78/phase78.api";

type Props = {
  isOpen: boolean;
  onClose: () => void;
  materialUnitId?: string | null;
  materialCode?: string | null;
  filters?: Record<string, unknown>;
  defaultName?: string;
  onSaved?: (result: SavedInvestigationResponse) => void | Promise<void>;
};

export function SaveInspectionJobModal({
  isOpen,
  onClose,
  materialUnitId,
  materialCode,
  filters = {},
  defaultName,
  onSaved,
}: Props) {
  const [name, setName] = useState(defaultName ?? `Investigation ${materialCode ?? materialUnitId ?? "material"}`);
  const [description, setDescription] = useState("Saved investigation view. Evidence-based review only; engineering validation is required before process changes.");
  const [schedule, setSchedule] = useState<"none" | "daily" | "weekly">("none");
  const [notifyOnChange, setNotifyOnChange] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (!name.trim()) {
      setError("Name is required.");
      return;
    }

    const request: SavedInvestigationRequest = {
      name: name.trim(),
      description: description.trim() || null,
      schedule,
      notifyOnChange,
      materialUnitId,
      materialCode,
      filters,
    };

    setIsSaving(true);
    setError(null);

    try {
      const result = await phase78Api.saveInvestigation(request);
      await onSaved?.(result);
      onClose();
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "Saving investigation failed.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <StandardModal
      open={isOpen}
      title="Save investigation"
      description="Create a saved investigation visible in Load Investigation and optionally scheduled as a monitoring job."
      onClose={onClose}
      footer={
        <>
          <StandardButton variant="ghost" onClick={onClose}>Cancel</StandardButton>
          <StandardButton variant="primary" leadingIcon={<Save size={16} />} isLoading={isSaving} onClick={submit}>
            Save Investigation
          </StandardButton>
        </>
      }
    >
      <StandardInput label="Name" required value={name} onChange={setName} error={error} />
      <StandardTextArea label="Description" value={description} onChange={setDescription} rows={4} />
      <StandardSelect
        label="Schedule"
        value={schedule}
        onChange={(value) => setSchedule(value as "none" | "daily" | "weekly")}
        options={[
          { value: "none", label: "None" },
          { value: "daily", label: "Daily" },
          { value: "weekly", label: "Weekly" },
        ]}
      />
      <label className="phase78-checkbox">
        <input type="checkbox" checked={notifyOnChange} onChange={(event) => setNotifyOnChange(event.target.checked)} />
        <span>Notify on change</span>
      </label>
    </StandardModal>
  );
}

export default SaveInspectionJobModal;
