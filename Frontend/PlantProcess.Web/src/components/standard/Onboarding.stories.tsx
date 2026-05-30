
import type { Meta, StoryObj } from "@storybook/react-vite";
import { StandardCard } from "./StandardSurface";
import "./standard-components.css";

const meta: Meta = {
  title: "PlantProcess IQ/Standards/Onboarding",
  parameters: { layout: "fullscreen" },
};

export default meta;

type Story = StoryObj;

export const ContributorOnboarding: Story = {
  render: () => (
    <main className="ppiq-std-standards-page">
      <StandardCard title="New contributor onboarding" subtitle="How to add or migrate UI safely in under 30 minutes.">
        <h3>Import path</h3>
        <pre>{"import { StandardButton, StandardTable, StandardInput } from '@/components/standard';"}</pre>

        <h3>Rules</h3>
        <ol>
          <li>Use Standard* components for all new buttons, tables, tabs, fields, cards, modals, and toasts.</li>
          <li>Do not hard-code steel-only terminology into base UI components.</li>
          <li>Keep manufacturing-specific wording in page content, metadata, demo data, or configuration.</li>
          <li>Run npm run validate:phase2:ui-standards-full before submitting.</li>
        </ol>

        <h3>Checklist</h3>
        <ul>
          <li>Story added or updated.</li>
          <li>Keyboard behavior verified.</li>
          <li>Loading, empty, and error states included.</li>
          <li>CSV inventory updated.</li>
          <li>No one-off button/table/form styling added.</li>
        </ul>
      </StandardCard>
    </main>
  ),
};
