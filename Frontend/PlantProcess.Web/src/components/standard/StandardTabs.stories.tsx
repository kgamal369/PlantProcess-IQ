
import { useState } from "react";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { Activity, Database, Gauge } from "lucide-react";
import { StandardTabs } from "./StandardTabs";
import { StandardCard } from "./StandardSurface";
import "./standard-components.css";

const meta: Meta<typeof StandardTabs> = {
  title: "PlantProcess IQ/Standard/Tabs",
  component: StandardTabs,
  parameters: { layout: "fullscreen" },
};

export default meta;

type Story = StoryObj<typeof StandardTabs>;

function Demo({ orientation = "horizontal", url = false }: { orientation?: "horizontal" | "vertical"; url?: boolean }) {
  const [value, setValue] = useState("genealogy");

  return (
    <div className="ppiq-std-standards-page">
      <StandardTabs
        ariaLabel="Material investigation tabs"
        value={value}
        onChange={setValue}
        orientation={orientation}
        searchParam={url ? "tab" : undefined}
        items={[
          { id: "genealogy", label: "Genealogy", icon: <Database size={16} />, badge: "4", content: <StandardCard title="Genealogy">Lineage content</StandardCard> },
          { id: "process", label: "Process history", icon: <Activity size={16} />, content: <StandardCard title="Process">Process content</StandardCard> },
          { id: "risk", label: "Risk", icon: <Gauge size={16} />, content: <StandardCard title="Risk">Risk content</StandardCard> },
          { id: "disabled", label: "Disabled", disabled: true, content: null },
        ]}
      />
    </div>
  );
}

export const Horizontal: Story = { render: () => <Demo /> };
export const Vertical: Story = { render: () => <Demo orientation="vertical" /> };
export const WithBadgesAndIcons: Story = { render: () => <Demo /> };
export const DisabledTab: Story = { render: () => <Demo /> };
export const UrlSynced: Story = { render: () => <Demo url /> };
