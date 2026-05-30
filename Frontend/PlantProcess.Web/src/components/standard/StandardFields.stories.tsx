
import { useState } from "react";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { Database } from "lucide-react";
import { StandardInput, StandardSelect, StandardTextArea } from "./StandardFields";
import "./standard-components.css";

const meta: Meta = {
  title: "PlantProcess IQ/Standard/Fields",
  parameters: { layout: "fullscreen" },
};

export default meta;

type Story = StoryObj;

export const Inputs: Story = {
  render: () => {
    const [search, setSearch] = useState("COIL-1001");

    return (
      <div className="ppiq-std-standards-page">
        <div style={{ display: "grid", gap: 16, maxWidth: 520 }}>
          <StandardInput label="Connector name" required placeholder="Production source" leadingIcon={<Database size={16} />} />
          <StandardInput label="Search material code" type="search" value={search} onChange={setSearch} helperText="Canonical migration target for PPIQ-T025." />
          <StandardInput label="Error example" error="Connector name is required." />
          <StandardInput label="Loading example" isLoading placeholder="Refreshing..." />
        </div>
      </div>
    );
  },
};

export const Selects: Story = {
  render: () => {
    const [single, setSingle] = useState<string | string[]>("thermal");
    const [multi, setMulti] = useState<string | string[]>(["thermal"]);

    return (
      <div className="ppiq-std-standards-page">
        <div style={{ display: "grid", gap: 16, maxWidth: 520 }}>
          <StandardSelect
            label="Process domain"
            value={single}
            onChange={setSingle}
            searchable
            options={[
              { value: "thermal", label: "Thermal process" },
              { value: "mechanical", label: "Mechanical process" },
              { value: "inspection", label: "Inspection / quality" },
            ]}
          />
          <StandardSelect
            label="Multi-select domains"
            multiple
            value={multi}
            onChange={setMulti}
            searchable
            options={[
              { value: "thermal", label: "Thermal process" },
              { value: "mechanical", label: "Mechanical process" },
              { value: "inspection", label: "Inspection / quality" },
            ]}
          />
        </div>
      </div>
    );
  },
};

export const TextArea: Story = {
  render: () => (
    <div className="ppiq-std-standards-page">
      <div style={{ maxWidth: 520 }}>
        <StandardTextArea label="Investigation note" helperText="Keep notes factual and avoid guaranteed root-cause wording." />
      </div>
    </div>
  ),
};
