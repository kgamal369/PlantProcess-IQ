
import { useState } from "react";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { StandardButton } from "./StandardButton";
import { StandardCard, StandardModal, StandardToastProvider, useStandardToast } from "./StandardSurface";
import "./standard-components.css";

const meta: Meta = {
  title: "PlantProcess IQ/Standard/Surface",
  parameters: { layout: "fullscreen" },
};

export default meta;

type Story = StoryObj;

function ToastDemo() {
  const toast = useStandardToast();

  return (
    <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
      <StandardButton onClick={() => toast.notify({ variant: "info", title: "Info", description: "Investigation started." })}>Info</StandardButton>
      <StandardButton variant="success" onClick={() => toast.notify({ variant: "success", title: "Saved", description: "Configuration saved." })}>Success</StandardButton>
      <StandardButton variant="secondary" onClick={() => toast.notify({ variant: "warning", title: "Warning", description: "Some rows need review." })}>Warning</StandardButton>
      <StandardButton variant="danger" onClick={() => toast.notify({ variant: "error", title: "Error", description: "Refresh failed." })}>Error</StandardButton>
      <StandardButton variant="ghost" onClick={() => toast.notify({ variant: "loading", title: "Loading", description: "Operation in progress." })}>Loading</StandardButton>
    </div>
  );
}

export const Cards: Story = {
  render: () => (
    <div className="ppiq-std-standards-page">
      <div className="ppiq-std-standards-grid ppiq-std-standards-grid--two">
        <StandardCard elevation="flat" title="Flat card">Flat surface.</StandardCard>
        <StandardCard elevation="raised" title="Raised card">Raised surface.</StandardCard>
        <StandardCard elevation="floating" title="Floating card">Floating surface.</StandardCard>
      </div>
    </div>
  ),
};

export const Modal: Story = {
  render: () => {
    const [open, setOpen] = useState(false);
    const [dirtyOpen, setDirtyOpen] = useState(false);

    return (
      <div className="ppiq-std-standards-page">
        <div style={{ display: "flex", gap: 12 }}>
          <StandardButton onClick={() => setOpen(true)}>Open modal</StandardButton>
          <StandardButton variant="secondary" onClick={() => setDirtyOpen(true)}>Open dirty modal</StandardButton>
        </div>

        <StandardModal open={open} title="Standard modal" description="Focus-trapped dialog." onClose={() => setOpen(false)} footer={<StandardButton onClick={() => setOpen(false)}>Confirm</StandardButton>}>
          This modal closes on Escape and click outside.
        </StandardModal>

        <StandardModal open={dirtyOpen} isDirty title="Dirty modal" description="Click-outside is disabled when isDirty=true." onClose={() => setDirtyOpen(false)} footer={<StandardButton onClick={() => setDirtyOpen(false)}>Save</StandardButton>}>
          Click outside is blocked to prevent data loss.
        </StandardModal>
      </div>
    );
  },
};

export const Toasts: Story = {
  render: () => (
    <StandardToastProvider>
      <div className="ppiq-std-standards-page">
        <ToastDemo />
      </div>
    </StandardToastProvider>
  ),
};
