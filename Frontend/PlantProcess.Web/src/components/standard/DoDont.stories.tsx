
import type { Meta, StoryObj } from "@storybook/react-vite";
import { StandardButton } from "./StandardButton";
import { StandardCard } from "./StandardSurface";
import { StandardInput } from "./StandardFields";
import "./standard-components.css";

const meta: Meta = {
  title: "PlantProcess IQ/Standards/Do and Do Not",
  parameters: { layout: "fullscreen" },
};

export default meta;

type Story = StoryObj;

export const DoAndDoNot: Story = {
  render: () => (
    <main className="ppiq-std-standards-page">
      <StandardCard title="Do / Do not examples" subtitle="Use this page as the review guide for future migration tasks.">
        <div className="ppiq-std-do-dont">
          <div className="ppiq-std-do">
            <h3>Do</h3>
            <p>Use StandardButton and StandardInput for consistent behavior, accessibility, and dark industrial styling.</p>
            <StandardButton>Run investigation</StandardButton>
            <div style={{ height: 12 }} />
            <StandardInput label="Search material code" type="search" placeholder="COIL-1001" />
          </div>

          <div className="ppiq-std-dont">
            <h3>Do not</h3>
            <p>Do not create one-off inline buttons, placeholder-only fields, or steel-only base UI primitives.</p>
            <button style={{ background: "blue", color: "white", padding: 6 }}>custom button</button>
            <div style={{ height: 12 }} />
            <input placeholder="Search material code..." />
          </div>
        </div>
      </StandardCard>
    </main>
  ),
};
