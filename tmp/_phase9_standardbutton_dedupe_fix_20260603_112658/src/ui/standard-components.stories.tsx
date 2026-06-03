import { useState } from "react";
import type { Meta, StoryObj } from "@storybook/react-vite";
import {
  StandardButton,
  StandardCard,
  StandardInput,
  StandardModal,
  StandardSelect,
  StandardTable,
  StandardTabs,
  StandardTextArea,
  StandardToastProvider,
  useStandardToast,
  type StandardTableColumn,
} from "./standard-components";

type DemoSignal = {
  id: string;
  asset: string;
  area: string;
  riskScore: number;
  status: "Stable" | "Watch" | "Critical";
};

const rows: DemoSignal[] = [
  { id: "SIG-1001", asset: "Production line A", area: "Thermal process", riskScore: 18, status: "Stable" },
  { id: "SIG-1002", asset: "Production line B", area: "Mechanical process", riskScore: 56, status: "Watch" },
  { id: "SIG-1003", asset: "Production line C", area: "Quality inspection", riskScore: 84, status: "Critical" },
];

const columns: StandardTableColumn<DemoSignal>[] = [
  { key: "asset", header: "Asset", cell: (row) => row.asset },
  { key: "area", header: "Process area", cell: (row) => row.area },
  { key: "risk", header: "Risk score", align: "right", cell: (row) => row.riskScore },
  { key: "status", header: "Status", cell: (row) => row.status },
];

const meta: Meta = {
  title: "PlantProcess IQ/UI Standards",
  parameters: {
    layout: "fullscreen",
  },
};

export default meta;

type Story = StoryObj;

function ToastButton() {
  const toast = useStandardToast();

  return (
    <StandardButton
      onClick={() =>
        toast.notify({
          intent: "success",
          title: "Configuration saved",
          description: "The manufacturing intelligence settings were updated.",
        })
      }
    >
      Show standard toast
    </StandardButton>
  );
}

function Token({ name, value }: { name: string; value: string }) {
  return (
    <div className="ppiq-token">
      <div className="ppiq-token__swatch" style={{ background: value }} />
      <div className="ppiq-token__meta">
        <div className="ppiq-token__name">{name}</div>
        <div className="ppiq-token__value">{value}</div>
      </div>
    </div>
  );
}

function StandardsPage() {
  const [tab, setTab] = useState("components");
  const [open, setOpen] = useState(false);

  return (
    <StandardToastProvider>
      <main className="ppiq-standards-page">
        <div className="ppiq-standards-grid">
          <StandardCard
            eyebrow="PPIQ-T018"
            title="PlantProcess IQ UI Standards"
            subtitle="Canonical dark industrial command-center UI primitives for generic manufacturing intelligence."
          >
            <StandardTabs
              ariaLabel="UI standards"
              activeId={tab}
              onChange={setTab}
              items={[
                { id: "components", label: "Components" },
                { id: "tokens", label: "Brand tokens" },
              ]}
            />
          </StandardCard>

          {tab === "tokens" ? (
            <StandardCard title="Brand tokens" subtitle="Use tokens instead of one-off colors.">
              <div className="ppiq-token-grid">
                <Token name="Brand" value="#00d4ff" />
                <Token name="Background" value="#050b18" />
                <Token name="Surface" value="#0b1730" />
                <Token name="Success" value="#30d158" />
                <Token name="Warning" value="#ffd166" />
                <Token name="Danger" value="#ff4d6d" />
              </div>
            </StandardCard>
          ) : null}

          {tab === "components" ? (
            <>
              <div className="ppiq-standards-grid ppiq-standards-grid--two">
                <StandardCard title="StandardButton" subtitle="One component for direct actions.">
                  <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
                    <StandardButton>Primary</StandardButton>
                    <StandardButton variant="secondary">Secondary</StandardButton>
                    <StandardButton variant="ghost">Ghost</StandardButton>
                    <StandardButton variant="danger">Danger</StandardButton>
                    <StandardButton loading>Refreshing</StandardButton>
                  </div>
                </StandardCard>

                <StandardCard title="Standard fields" subtitle="Labels, helper text, errors, and accessibility.">
                  <div style={{ display: "grid", gap: 14 }}>
                    <StandardInput label="Connector name" placeholder="Production data source" required />
                    <StandardSelect
                      label="Process domain"
                      placeholder="Select domain"
                      options={[
                        { value: "thermal", label: "Thermal process" },
                        { value: "mechanical", label: "Mechanical process" },
                        { value: "inspection", label: "Inspection / quality" },
                      ]}
                    />
                    <StandardTextArea label="Investigation note" helperText="Keep notes factual and investigation-first." />
                  </div>
                </StandardCard>
              </div>

              <StandardCard title="StandardTable" subtitle="Centralized analytical table behavior.">
                <StandardTable
                  caption="Manufacturing intelligence risk signals"
                  columns={columns}
                  data={rows}
                  getRowKey={(row) => row.id}
                  getRowTone={(row) =>
                    row.status === "Critical" ? "danger" : row.status === "Watch" ? "warning" : "success"
                  }
                />
              </StandardCard>

              <StandardCard title="StandardModal and StandardToast">
                <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
                  <StandardButton onClick={() => setOpen(true)}>Open modal</StandardButton>
                  <ToastButton />
                </div>

                <StandardModal
                  open={open}
                  title="Confirm investigation action"
                  description="Use modals only for focused decisions."
                  onClose={() => setOpen(false)}
                  footer={
                    <>
                      <StandardButton variant="ghost" onClick={() => setOpen(false)}>
                        Cancel
                      </StandardButton>
                      <StandardButton onClick={() => setOpen(false)}>Confirm</StandardButton>
                    </>
                  }
                >
                  This modal follows the canonical PlantProcess IQ visual and accessibility pattern.
                </StandardModal>
              </StandardCard>
            </>
          ) : null}
        </div>
      </main>
    </StandardToastProvider>
  );
}

export const UIStandards: Story = {
  render: () => <StandardsPage />,
};